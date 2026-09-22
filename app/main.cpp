#include "yipe/audio.hpp"
#include "yipe/auxiliary_data.hpp"
#include "yipe/casino.hpp"
#include "yipe/encounter.hpp"
#include "yipe/item_names.hpp"
#include "yipe/legacy_save.hpp"
#include "yipe/map.hpp"
#include "yipe/random.hpp"
#include "yipe/quest.hpp"
#include "yipe/save.hpp"
#include "yipe/settings.hpp"
#include "yipe/shop.hpp"
#include "yipe/spell.hpp"
#include "generated/original_help.hpp"
#include "file_dialog.hpp"

#include <SDL.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <ctime>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <iostream>
#include <limits>
#include <map>
#include <memory>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace {

constexpr int startup_width = 288;
constexpr int startup_menu_height = 360;
constexpr int startup_height = 397;
constexpr int tile_size = 32;
constexpr int map_width = yipe::Map::width * tile_size;
constexpr int map_height = yipe::Map::height * tile_size;
constexpr yipe::MapPosition hell_gatekeeper_position{6, 0};

constexpr std::array<std::string_view, 21> location_messages{
    "Welcome to the town of Maynard.",
    "A princess has blessed these waters.",
    "Notice:     Anyone who can rescue all my daughters will be rewarded.      -The King",
    "In a language only you seem to be able to understand:    Skill shop moved secretly north.",
    "Welcome to Yipe Hell!                                          Gatekeeper ahead.",
    "Only experienced adventurers should travel beyond this sign.",
    "Beware:  Unstable floor ahead.",
    "Monster breeding grounds.",
    "I left everything behind the tree.  Make sure to know the co...  Ahhhhhhhh....",
    "Beware:   This lake has been known to have strange and random currents that leave adventurers stranded for days.",
    "Stone Cave",
    "Zephyr Cave",
    "Fire Cave",
    "Welcome to the bizarre final cave!",
    "Stay out of my lair!",
    "From the top:   2, 3, 1, 3, 2",
    "Turn back while you can!",
    "I'll be in Hell...",
    "Exit back to Yipe!",
    "The sign says 'Yipe!'.",
    "Princess Holding Cell:  West",
};

using Window = std::unique_ptr<SDL_Window, decltype(&SDL_DestroyWindow)>;
using Renderer = std::unique_ptr<SDL_Renderer, decltype(&SDL_DestroyRenderer)>;
using Texture = std::unique_ptr<SDL_Texture, decltype(&SDL_DestroyTexture)>;
using Cursor = std::unique_ptr<SDL_Cursor, decltype(&SDL_FreeCursor)>;

enum class StartupChoice { new_game, load_game, settings, high_scores, help, quit };

struct GameExit {
    StartupChoice choice;
    std::optional<yipe::GameState> opened_game = std::nullopt;
    std::filesystem::path opened_save_path{};
};

struct RuntimeAssets {
    std::filesystem::path bitmaps;
    std::filesystem::path audio;
    std::filesystem::path monster_sounds;
    std::filesystem::path item_names;
};

class SdlSession {
public:
    SdlSession() {
        if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO) != 0) {
            throw std::runtime_error(std::string("SDL initialization failed: ") + SDL_GetError());
        }
    }
    ~SdlSession() { SDL_Quit(); }

    SdlSession(const SdlSession&) = delete;
    SdlSession& operator=(const SdlSession&) = delete;
};

class AudioPlayer {
public:
    explicit AudioPlayer(const RuntimeAssets& assets)
        : audio_directory_(assets.audio),
          monster_sound_directory_(assets.monster_sounds) {
        SDL_AudioSpec desired{};
        desired.freq = 44'100;
        desired.format = AUDIO_S16SYS;
        desired.channels = 1;
        desired.samples = 4'096;
        device_ = SDL_OpenAudioDevice(nullptr, 0, &desired, &output_, 0);
        if (device_ == 0) {
            error_ = std::string("could not open the audio device: ") + SDL_GetError();
            return;
        }

        try {
            for (const yipe::SoundEffect& effect : yipe::sound_effects()) {
                effect_clips_.emplace(
                    effect.id, load_clip(audio_directory_ / effect.asset_name));
            }
        } catch (const std::runtime_error& error) {
            error_ = error.what();
            effect_clips_.clear();
            SDL_CloseAudioDevice(device_);
            device_ = 0;
        }
    }

    ~AudioPlayer() {
        if (device_ != 0) {
            SDL_CloseAudioDevice(device_);
        }
    }

    AudioPlayer(const AudioPlayer&) = delete;
    AudioPlayer& operator=(const AudioPlayer&) = delete;

    [[nodiscard]] bool available() const { return device_ != 0; }
    [[nodiscard]] const std::string& error() const { return error_; }

    void stop() const {
        if (device_ != 0) {
            SDL_ClearQueuedAudio(device_);
        }
    }

    void play(int sound_id, bool enabled) {
        if (!enabled || device_ == 0 || sound_id == 0) {
            return;
        }
        const auto found = effect_clips_.find(sound_id);
        if (found == effect_clips_.end()) {
            std::cerr << "Yipe! III: unknown sound effect " << sound_id << '\n';
            return;
        }
        queue(found->second);
    }

    void play_monster_death(int monster_type, bool enabled) {
        if (!enabled || device_ == 0) {
            return;
        }
        const int file_id = yipe::monster_sound_file_id(monster_type);
        const std::filesystem::path path =
            monster_sound_directory_ / (std::to_string(file_id) + ".wav");
        if (!std::filesystem::exists(path)) {
            play(12, true);
            return;
        }
        try {
            auto [position, inserted] = monster_clips_.try_emplace(file_id);
            if (inserted) {
                position->second = load_clip(path);
            }
            queue(position->second);
        } catch (const std::runtime_error& error) {
            std::cerr << "Yipe! III: " << error.what()
                      << "; using the generic monster-death sound\n";
            play(12, true);
        }
    }

    void validate_packaged_monster_sounds() {
        constexpr std::array file_ids{
            2, 3, 4, 5, 6, 8, 9, 13, 15, 16,
            19, 20, 21, 24, 25, 28, 31, 35, 37, 40,
        };
        for (const int file_id : file_ids) {
            const std::filesystem::path path =
                monster_sound_directory_ / (std::to_string(file_id) + ".wav");
            monster_clips_[file_id] = load_clip(path);
        }
    }

private:
    std::vector<Uint8> load_clip(const std::filesystem::path& path) const {
        SDL_AudioSpec source{};
        Uint8* raw_buffer = nullptr;
        Uint32 raw_length = 0;
        if (SDL_LoadWAV(path.string().c_str(), &source, &raw_buffer, &raw_length)
            == nullptr) {
            throw std::runtime_error("could not load " + path.string() + ": "
                                     + SDL_GetError());
        }
        std::unique_ptr<Uint8, decltype(&SDL_FreeWAV)> raw(raw_buffer, SDL_FreeWAV);
        if (raw_length > static_cast<Uint32>(std::numeric_limits<int>::max())) {
            throw std::runtime_error("audio asset is too large: " + path.string());
        }

        SDL_AudioCVT converter{};
        const int conversion = SDL_BuildAudioCVT(
            &converter, source.format, source.channels, source.freq,
            output_.format, output_.channels, output_.freq);
        if (conversion < 0) {
            throw std::runtime_error("could not prepare " + path.string() + ": "
                                     + SDL_GetError());
        }
        const std::size_t capacity = conversion == 1
            ? static_cast<std::size_t>(raw_length)
                * static_cast<std::size_t>(converter.len_mult)
            : static_cast<std::size_t>(raw_length);
        std::vector<Uint8> converted(capacity);
        std::memcpy(converted.data(), raw.get(), static_cast<std::size_t>(raw_length));
        if (conversion == 1) {
            converter.buf = converted.data();
            converter.len = static_cast<int>(raw_length);
            if (SDL_ConvertAudio(&converter) != 0) {
                throw std::runtime_error("could not convert " + path.string() + ": "
                                         + SDL_GetError());
            }
            converted.resize(static_cast<std::size_t>(converter.len_cvt));
        } else {
            converted.resize(static_cast<std::size_t>(raw_length));
        }
        return converted;
    }

    void queue(const std::vector<Uint8>& clip) const {
        SDL_ClearQueuedAudio(device_);
        if (SDL_QueueAudio(device_, clip.data(), static_cast<Uint32>(clip.size())) != 0) {
            std::cerr << "Yipe! III: could not queue audio: " << SDL_GetError() << '\n';
            return;
        }
        SDL_PauseAudioDevice(device_, 0);
    }

    std::filesystem::path audio_directory_;
    std::filesystem::path monster_sound_directory_;
    SDL_AudioDeviceID device_ = 0;
    SDL_AudioSpec output_{};
    std::map<int, std::vector<Uint8>> effect_clips_;
    std::map<int, std::vector<Uint8>> monster_clips_;
    std::string error_;
};

Texture load_bitmap(SDL_Renderer* renderer, const std::filesystem::path& path,
                    bool transparent = false) {
    std::unique_ptr<SDL_Surface, decltype(&SDL_FreeSurface)> surface(
        SDL_LoadBMP(path.string().c_str()), SDL_FreeSurface);
    if (!surface) {
        throw std::runtime_error("could not load " + path.string() + ": " + SDL_GetError());
    }
    if (transparent) {
        const Uint32 magenta = SDL_MapRGB(surface->format, 255, 0, 255);
        SDL_SetColorKey(surface.get(), SDL_TRUE, magenta);
    }
    Texture texture(SDL_CreateTextureFromSurface(renderer, surface.get()), SDL_DestroyTexture);
    if (!texture) {
        throw std::runtime_error("could not create texture for " + path.string() + ": "
                                 + SDL_GetError());
    }
    return texture;
}

RuntimeAssets runtime_assets(const char* executable) {
    std::unique_ptr<char, decltype(&SDL_free)> base_path(SDL_GetBasePath(), SDL_free);
    if (base_path) {
        const std::filesystem::path beside_executable =
            std::filesystem::path(base_path.get()) / "assets";
        if (std::filesystem::exists(beside_executable / "379-1033.bmp")) {
            return {
                beside_executable,
                beside_executable / "audio",
                beside_executable / "MonsterSounds",
                beside_executable / "Yipe3Items.txt",
            };
        }
    }

    const std::filesystem::path embedded = "assets/embedded";
    if (std::filesystem::exists(embedded / "bitmaps/379-1033.bmp")) {
        return {
            embedded / "bitmaps",
            embedded / "audio",
            "assets/original/MonsterSounds",
            "assets/original/Yipe3Items.txt",
        };
    }
    throw std::runtime_error(std::string("could not locate runtime assets for ") + executable);
}

void copy_texture(SDL_Renderer* renderer, SDL_Texture* texture, const SDL_Rect* source,
                  const SDL_Rect* destination) {
    if (SDL_RenderCopy(renderer, texture, source, destination) != 0) {
        throw std::runtime_error(std::string("could not render texture: ") + SDL_GetError());
    }
}

void render_startup(SDL_Renderer* renderer, SDL_Texture* menu, SDL_Texture* copyright) {
    SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
    SDL_RenderClear(renderer);
    const SDL_Rect menu_destination{0, 0, startup_width, startup_menu_height};
    const SDL_Rect copyright_destination{61, 368, 166, 21};
    copy_texture(renderer, menu, nullptr, &menu_destination);
    copy_texture(renderer, copyright, nullptr, &copyright_destination);
    SDL_RenderPresent(renderer);
}

StartupChoice option_at(float y) {
    if (y < 296.0F) {
        return StartupChoice::new_game;
    }
    if (y < 327.0F) {
        return StartupChoice::load_game;
    }
    return StartupChoice::quit;
}

StartupChoice run_startup_menu(SDL_Renderer* renderer, SDL_Texture* menu,
                               SDL_Texture* copyright) {
    const Cursor arrow(SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_ARROW), SDL_FreeCursor);
    const Cursor hand(SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_HAND), SDL_FreeCursor);
    render_startup(renderer, menu, copyright);

    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            throw std::runtime_error(std::string("SDL event wait failed: ") + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            return StartupChoice::quit;
        }
        if (event.type == SDL_KEYDOWN) {
            if (event.key.keysym.sym == SDLK_n) {
                return StartupChoice::new_game;
            }
            if (event.key.keysym.sym == SDLK_l) {
                return StartupChoice::load_game;
            }
            if (event.key.keysym.sym == SDLK_s) {
                return StartupChoice::settings;
            }
            if (event.key.keysym.sym == SDLK_h && event.key.repeat == 0) {
                return StartupChoice::high_scores;
            }
            if (event.key.keysym.sym == SDLK_F1 && event.key.repeat == 0) {
                return StartupChoice::help;
            }
            if (event.key.keysym.sym == SDLK_ESCAPE || event.key.keysym.sym == SDLK_q) {
                return StartupChoice::quit;
            }
        }
        if (event.type == SDL_MOUSEMOTION) {
            float x = 0.0F;
            float y = 0.0F;
            SDL_RenderWindowToLogical(renderer, event.motion.x, event.motion.y, &x, &y);
            const bool over_option = x >= 45.0F && x <= 250.0F && y >= 257.0F && y < 360.0F;
            SDL_SetCursor(over_option ? hand.get() : arrow.get());
        }
        if (event.type == SDL_MOUSEBUTTONUP && event.button.button == SDL_BUTTON_LEFT) {
            float x = 0.0F;
            float y = 0.0F;
            SDL_RenderWindowToLogical(renderer, event.button.x, event.button.y, &x, &y);
            if (x >= 45.0F && x <= 250.0F && y >= 257.0F && y < 360.0F) {
                return option_at(y);
            }
        }
        if (event.type == SDL_WINDOWEVENT && event.window.event == SDL_WINDOWEVENT_EXPOSED) {
            render_startup(renderer, menu, copyright);
        }
    }
}

struct GameTextures {
    std::vector<std::pair<int, Texture>> bitmaps;

    [[nodiscard]] SDL_Texture* find(int resource_id) const {
        for (const auto& [id, texture] : bitmaps) {
            if (id == resource_id) {
                return texture.get();
            }
        }
        throw std::runtime_error("missing bitmap resource "
                                 + std::to_string(resource_id));
    }
};

GameTextures load_game_textures(SDL_Renderer* renderer, const std::filesystem::path& assets) {
    constexpr std::array resource_ids{133, 134, 135, 136, 137, 138, 139, 140, 141, 142,
                                      143, 144, 145, 146, 147, 153, 377, 391, 392, 393,
                                      418, 460};
    std::vector<std::pair<int, Texture>> bitmaps;
    bitmaps.reserve(resource_ids.size() + 41 + 12);
    for (const int id : resource_ids) {
        bitmaps.emplace_back(
            id, load_bitmap(renderer, assets / (std::to_string(id) + "-1033.bmp"),
                            id == 153));
    }
    for (int id = 174; id <= 214; ++id) {
        bitmaps.emplace_back(
            id, load_bitmap(renderer, assets / (std::to_string(id) + "-1033.bmp"), true));
    }
    for (int id = 406; id <= 417; ++id) {
        bitmaps.emplace_back(
            id, load_bitmap(renderer, assets / (std::to_string(id) + "-1033.bmp"), true));
    }
    return {std::move(bitmaps)};
}

struct TileGraphic {
    SDL_Texture* texture;
    SDL_Rect source;
};

TileGraphic tile_graphic(std::uint16_t tile, const GameTextures& textures) {
    const auto graphic = [&textures](int resource_id, int x, int y) {
        return TileGraphic{textures.find(resource_id), {x, y, tile_size, tile_size}};
    };
    switch (tile) {
    case 1:
    case 4:
        return graphic(137, 0, 0);
    case 2:
    case 0x46:
        return graphic(133, 0, 0);
    case 3:
    case 0x45:
        return graphic(139, 0, 32);
    case 5:
        return graphic(137, 32, 0);
    case 6:
        return graphic(137, 0, 32);
    case 7:
        return graphic(137, 32, 32);
    case 8:
        return graphic(142, 32, 0);
    case 9:
        return graphic(143, 32, 32);
    case 10:
    case 0x41:
        return graphic(418, 0, 0);
    case 0x0b:
        return graphic(133, 0, 32);
    case 0x0c:
    case 0x0d:
        return graphic(134, 0, 0);
    case 0x0e:
    case 0x59:
        return graphic(134, 0, 32);
    case 0x0f:
        return graphic(141, 0, 32);
    case 0x10:
        return graphic(141, 32, 32);
    case 0x11:
    case 0x58:
        return graphic(144, 32, 0);
    case 0x12:
        return graphic(144, 0, 32);
    case 0x13:
        return graphic(144, 0, 0);
    case 0x14:
        return graphic(141, 0, 0);
    case 0x15:
        return graphic(141, 32, 0);
    case 0x16:
        return graphic(142, 0, 0);
    case 0x17:
        return graphic(142, 0, 32);
    case 0x18:
        return graphic(142, 32, 32);
    case 0x19:
        return graphic(134, 32, 0);
    case 0x1a:
        return graphic(134, 32, 32);
    case 0x1b:
        return graphic(138, 0, 0);
    case 0x1c:
        return graphic(138, 32, 0);
    case 0x1d:
        return graphic(138, 0, 32);
    case 0x1e:
        return graphic(138, 32, 32);
    case 0x1f:
    case 0x69:
        return graphic(143, 0, 0);
    case 0x20:
        return graphic(143, 32, 0);
    case 0x21:
        return graphic(143, 0, 32);
    case 0x22:
    case 0x43:
        return graphic(145, 0, 0);
    case 0x23:
    case 0x44:
        return graphic(145, 32, 0);
    case 0x24:
        return graphic(145, 0, 32);
    case 0x25:
    case 0x5a:
        return graphic(145, 32, 32);
    case 0x26:
        return graphic(139, 32, 0);
    case 0x27:
    case 0x47:
    case 0x54:
    case 0x55:
    case 0x5d:
        return graphic(136, 0, 0);
    case 0x28:
    case 0x48:
    case 0x52:
    case 0x5e:
    case 0x61:
        return graphic(136, 32, 0);
    case 0x29:
    case 0x2a:
        return graphic(136, 0, 32);
    case 0x2b:
        return graphic(136, 32, 32);
    case 0x2c:
        return graphic(135, 32, 32);
    case 0x2d:
    case 0x42:
        return graphic(135, 32, 0);
    case 0x2e:
    case 0x57:
        return graphic(135, 0, 0);
    case 0x2f:
        return graphic(135, 0, 32);
    case 0x30:
        return graphic(377, 0, 0);
    case 0x31:
    case 0x56:
        return graphic(133, 32, 32);
    case 0x32:
        return graphic(133, 32, 0);
    case 0x33:
        return graphic(144, 32, 32);
    case 0x34:
        return graphic(140, 32, 0);
    case 0x35:
        return graphic(140, 0, 32);
    case 0x36:
        return graphic(140, 0, 0);
    case 0x39:
        return graphic(139, 0, 0);
    case 0x3e:
    case 0x3f:
    case 0x40:
        return graphic(139, 32, 32);
    case 0x49:
    case 0x53:
    case 0x5f:
    case 0x62:
        return graphic(140, 32, 32);
    case 0x4a:
        return graphic(146, 0, 0);
    case 0x4b:
        return graphic(146, 32, 0);
    case 0x4c:
        return graphic(146, 0, 32);
    case 0x4d:
        return graphic(146, 32, 32);
    case 0x4e:
        return graphic(147, 0, 0);
    case 0x4f:
        return graphic(147, 32, 0);
    case 0x50:
        return graphic(147, 0, 32);
    case 0x51:
        return graphic(147, 32, 32);
    case 0x65:
        return graphic(460, 0, 0);
    case 0x66:
        return graphic(460, 32, 0);
    case 0x67:
        return graphic(460, 0, 32);
    case 0x68:
        return graphic(460, 32, 32);
    case 0x71:
        return graphic(153, 32, 0);
    default:
        return graphic(153, 32, 32);
    }
}

void prepare_game_view(SDL_Window* window, SDL_Renderer* renderer) {
    SDL_SetWindowTitle(window, "Yipe! III - F1 Help | F5 Save | F10 Settings");
    SDL_SetWindowSize(window, map_width * 2, map_height * 2);
    if (SDL_RenderSetLogicalSize(renderer, map_width, map_height) != 0) {
        throw std::runtime_error(std::string("could not set map render size: ") + SDL_GetError());
    }
}

void prepare_startup_view(SDL_Window* window, SDL_Renderer* renderer) {
    SDL_SetWindowTitle(window, "Yipe! III - F1 Help | S Settings | H High Scores");
    SDL_SetWindowSize(window, startup_width * 2, startup_height * 2);
    if (SDL_RenderSetLogicalSize(renderer, startup_width, startup_height) != 0) {
        throw std::runtime_error(std::string("could not set startup render size: ")
                                 + SDL_GetError());
    }
}

bool monster_placement_defeated(const yipe::GameState& game,
                                const yipe::MonsterPlacement& placement) {
    const auto contains = [index = placement.placement_index](const auto& placements) {
        return std::find(placements.begin(), placements.end(), index)
            != placements.end();
    };
    return contains(game.temporarily_defeated_monster_placements)
        || (!placement.respawns_after_map_reload
            && contains(game.defeated_monster_placements));
}

std::vector<yipe::MonsterPlacement> active_monster_placements(
    const yipe::GameState& game) {
    std::vector<yipe::MonsterPlacement> active;
    for (const yipe::MonsterPlacement& placement : yipe::original_monsters_for_map(
             game.location.map_id, game.player.difficulty, game.player.level)) {
        if (!monster_placement_defeated(game, placement)) {
            active.push_back(placement);
        }
    }
    return active;
}

void render_game(SDL_Renderer* renderer, const yipe::Map& map,
                 const GameTextures& textures, const yipe::GameState& game) {
    SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
    SDL_RenderClear(renderer);
    for (int x = 0; x < yipe::Map::width; ++x) {
        for (int y = 0; y < yipe::Map::height; ++y) {
            if (!game.map_visibility.contains({x, y})) {
                continue;
            }
            const TileGraphic graphic = tile_graphic(map.tile({x, y}), textures);
            const SDL_Rect destination{x * tile_size, y * tile_size, tile_size, tile_size};
            copy_texture(renderer, graphic.texture, &graphic.source, &destination);
        }
    }
    for (const yipe::MonsterPlacement& placement : active_monster_placements(game)) {
        if (!game.map_visibility.contains(placement.position)) {
            continue;
        }
        const std::optional<yipe::MonsterDefinition> monster =
            yipe::original_monster_definition(placement.type_id);
        if (!monster) {
            throw std::logic_error("monster placement has no recovered definition");
        }
        const int extent = placement.occupies_two_by_two_tiles
            ? tile_size * 2 : tile_size;
        const SDL_Rect destination{placement.position.x * tile_size,
                                   placement.position.y * tile_size,
                                   extent, extent};
        copy_texture(renderer, textures.find(monster->bitmap_resource_id), nullptr,
                     &destination);
    }
    if (game.roaming_monster
        && game.map_visibility.contains(game.roaming_monster->position)) {
        const std::optional<yipe::MonsterDefinition> monster =
            yipe::original_monster_definition(game.roaming_monster->type_id);
        if (!monster) {
            throw std::logic_error("roaming monster has no recovered definition");
        }
        const SDL_Rect destination{
            game.roaming_monster->position.x * tile_size,
            game.roaming_monster->position.y * tile_size,
            tile_size,
            tile_size,
        };
        copy_texture(renderer, textures.find(monster->bitmap_resource_id), nullptr,
                     &destination);
    }
    if (game.pursuing_monster
        && game.map_visibility.contains(game.pursuing_monster->position)) {
        const std::optional<yipe::MonsterDefinition> monster =
            yipe::original_monster_definition(game.pursuing_monster->type_id);
        if (!monster) {
            throw std::logic_error("pursuing monster has no recovered definition");
        }
        const SDL_Rect destination{
            game.pursuing_monster->position.x * tile_size,
            game.pursuing_monster->position.y * tile_size,
            tile_size,
            tile_size,
        };
        copy_texture(renderer, textures.find(monster->bitmap_resource_id), nullptr,
                     &destination);
    }
    if (map.id() == 0x75
        && game.player.hell_gatekeeper_state
            == yipe::HellGatekeeperState::waiting
        && game.map_visibility.contains(hell_gatekeeper_position)) {
        const SDL_Rect destination{hell_gatekeeper_position.x * tile_size,
                                   hell_gatekeeper_position.y * tile_size,
                                   tile_size, tile_size};
        copy_texture(renderer, textures.find(214), nullptr, &destination);
    }
    const yipe::PrincessEvent& princess = map.events().princess;
    if (princess.has_position()
        && game.map_visibility.contains(princess.position)
        && game.player.inventory_values[princess.rescue_index] == 0) {
        const SDL_Rect destination{princess.position.x * tile_size,
                                   princess.position.y * tile_size,
                                   tile_size, tile_size};
        copy_texture(renderer, textures.find(390 + princess.portrait_variant), nullptr,
                     &destination);
    }
    const yipe::MapEvent& overlay = map.events().overlay;
    if (overlay.value > 0 && overlay.has_position()
        && game.map_visibility.contains(overlay.position)) {
        const TileGraphic graphic = tile_graphic(0x71, textures);
        const SDL_Rect destination{overlay.position.x * tile_size,
                                   overlay.position.y * tile_size, tile_size, tile_size};
        copy_texture(renderer, graphic.texture, &graphic.source, &destination);
    }
    const SDL_Rect player_destination{game.location.position.x * tile_size,
                                      game.location.position.y * tile_size,
                                      tile_size, tile_size};
    const int portrait = game.player.inventory[28];
    copy_texture(renderer, textures.find(405 + (portrait >= 1 && portrait <= 12 ? portrait : 1)),
                 nullptr, &player_destination);
    SDL_RenderPresent(renderer);
}

void show_message(SDL_Window* window, const char* title, const std::string& message) {
    if (SDL_ShowSimpleMessageBox(SDL_MESSAGEBOX_INFORMATION, title, message.c_str(), window)
        != 0) {
        throw std::runtime_error(std::string("could not show message: ") + SDL_GetError());
    }
}

int show_choice(SDL_Window* window, const char* title, const std::string& message,
                const SDL_MessageBoxButtonData* buttons, int button_count) {
    const SDL_MessageBoxData data{SDL_MESSAGEBOX_INFORMATION, window, title, message.c_str(),
                                  button_count, buttons, nullptr};
    int selected = -1;
    if (SDL_ShowMessageBox(&data, &selected) != 0) {
        throw std::runtime_error(std::string("could not show choices: ") + SDL_GetError());
    }
    return selected;
}

std::string original_help_page(std::size_t page) {
    const auto& [title, body] = yipe::original_help_pages.at(page);
    std::ostringstream text;
    text << "Original 2003 Manual - " << page + 1 << "/"
         << yipe::original_help_pages.size() << "\n" << title << "\n\n";
    // Native message boxes do not consistently wrap long paragraphs. Keep the
    // original words and paragraph breaks while limiting each line to 72 columns.
    std::istringstream paragraphs{std::string(body)};
    std::string paragraph;
    while (std::getline(paragraphs, paragraph)) {
        std::istringstream words(paragraph);
        std::string word;
        std::size_t column = 0;
        while (words >> word) {
            if (column != 0) {
                if (column + 1 + word.size() > 72) {
                    text << '\n';
                    column = 0;
                } else {
                    text << ' ';
                    ++column;
                }
            }
            text << word;
            column += word.size();
        }
        text << '\n';
    }
    return text.str();
}

void show_original_help(SDL_Window* window, std::size_t page = 0) {
    while (true) {
        std::vector<SDL_MessageBoxButtonData> buttons{
            {SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT
                 | SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 0, "Done"},
        };
        if (page > 0) {
            buttons.push_back({0, 1, "Previous"});
            buttons.push_back({0, 3, "Start"});
        }
        if (page + 1 < yipe::original_help_pages.size()) {
            buttons.push_back({0, 2, "Next"});
            buttons.push_back({0, 4, "End"});
        }
        const int choice = show_choice(window, "Help and Info", original_help_page(page),
                                       buttons.data(), static_cast<int>(buttons.size()));
        switch (choice) {
        case 1: --page; break;
        case 2: ++page; break;
        case 3: page = 0; break;
        case 4: page = yipe::original_help_pages.size() - 1; break;
        default: return;
        }
    }
}

void show_princess(SDL_Window* window, yipe::Player& player,
                   const yipe::PrincessEvent& princess, yipe::MsvcRandom& random) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Gold"},
        SDL_MessageBoxButtonData{0, 2, "Experience"},
        SDL_MessageBoxButtonData{0, 3, "Kiss"},
        SDL_MessageBoxButtonData{0, 4, "Mystery"},
    };
    const int choice = show_choice(
        window, "Rescued Princess",
        "The Princess thanks you for rescuing her. As a small reward, she offers "
        "you one of the following gifts.",
        buttons.data(), static_cast<int>(buttons.size()));
    if (choice < 1 || choice > 4) {
        return;
    }
    const yipe::PrincessReward reward = yipe::rescue_princess(
        player, princess.rescue_index, static_cast<yipe::PrincessGift>(choice), random);
    if (reward.rescued) {
        show_message(window, "Princess Reward", reward.message);
    }
}

std::optional<std::string> request_text(SDL_Window* window, const char* title,
                                        const std::string& prompt,
                                        const char* field = "code") {
    show_message(window, title, prompt + "\n\nType the " + field + ", then press Enter. "
                                         "Press Escape to cancel. Your typing will appear "
                                         "in the game window's title bar.");
    const std::string original_title = SDL_GetWindowTitle(window);
    std::string input;
    const auto update_title = [&] {
        SDL_SetWindowTitle(window,
                           (std::string(title) + " " + field + ": " + input).c_str());
    };
    const auto finish = [&] {
        SDL_StopTextInput();
        SDL_SetWindowTitle(window, original_title.c_str());
    };

    update_title();
    SDL_FlushEvent(SDL_TEXTINPUT);
    SDL_StartTextInput();
    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            finish();
            throw std::runtime_error(std::string("SDL event wait failed: ")
                                     + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            SDL_PushEvent(&event);
            finish();
            return std::nullopt;
        }
        if (event.type == SDL_TEXTINPUT) {
            if (input.size() + std::char_traits<char>::length(event.text.text) <= 128) {
                input += event.text.text;
                update_title();
            }
            continue;
        }
        if (event.type != SDL_KEYDOWN) {
            continue;
        }
        if (event.key.keysym.sym == SDLK_RETURN
            || event.key.keysym.sym == SDLK_KP_ENTER) {
            finish();
            return input;
        }
        if (event.key.keysym.sym == SDLK_ESCAPE) {
            finish();
            return std::nullopt;
        }
        if (event.key.keysym.sym == SDLK_BACKSPACE && !input.empty()) {
            const bool multibyte =
                (static_cast<unsigned char>(input.back()) & 0xc0u) == 0x80u;
            input.pop_back();
            if (multibyte) {
                while (!input.empty()
                       && (static_cast<unsigned char>(input.back()) & 0xc0u) == 0x80u) {
                    input.pop_back();
                }
                if (!input.empty()) {
                    input.pop_back();
                }
            }
            update_title();
        }
    }
}

void show_secret_tree(SDL_Window* window, yipe::Player& player) {
    const std::optional<std::string> code = request_text(
        window, "Secret Tree",
        "A small note is nailed to the tree here. It seems to be asking you for some "
        "type of secret code.\n\nIt says:\nEnter the letter from the cave the correct "
        "number of times:");
    if (!code) {
        return;
    }
    if (yipe::enter_secret_tree_code(player, *code)
        == yipe::SecretTreeResult::reward_granted) {
        show_message(window, "Secret Tree",
                     "You have cracked the secret tree code! You find 1500 gold pieces "
                     "and a ring of bonus healing! The ring will enhance the use of all "
                     "healing items and spells.");
    } else {
        show_message(window, "Secret Tree",
                     "After entering the code, nothing seems to happen.");
    }
}

std::optional<std::uint16_t> choose_portrait(SDL_Window* window, SDL_Renderer* renderer,
                                            const GameTextures& textures) {
    show_message(window, "Choose Portrait",
                 "Click a portrait or press 1-8, then Enter. S switches the first four "
                 "portraits to Silly Mode. Escape returns to character setup.");
    const std::string original_title = SDL_GetWindowTitle(window);
    int selected = 0;
    bool silly = false;
    const auto portrait_id = [&](int index) {
        return silly && index < 4 ? 414 + index : 406 + index;
    };
    const auto draw = [&] {
        SDL_SetWindowTitle(window,
                           ("Choose Portrait " + std::to_string(selected + 1) + "/8 ("
                            + (silly ? "Silly" : "Normal") + ") - S toggles, Enter accepts").c_str());
        SDL_SetRenderDrawColor(renderer, 18, 24, 35, 255);
        SDL_RenderClear(renderer);
        for (int index = 0; index < 8; ++index) {
            const SDL_Rect border{10 + index % 4 * 70, 82 + index / 4 * 90, 58, 58};
            SDL_SetRenderDrawColor(renderer, index == selected ? 255 : 90,
                                   index == selected ? 213 : 106,
                                   index == selected ? 100 : 128, 255);
            SDL_RenderFillRect(renderer, &border);
            const SDL_Rect image{border.x + 5, border.y + 5, 48, 48};
            copy_texture(renderer, textures.find(portrait_id(index)), nullptr, &image);
        }
        SDL_RenderPresent(renderer);
    };
    const auto finish = [&] { SDL_SetWindowTitle(window, original_title.c_str()); };
    draw();
    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            finish();
            throw std::runtime_error(std::string("SDL event wait failed: ") + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            SDL_PushEvent(&event);
            finish();
            return std::nullopt;
        }
        if (event.type == SDL_WINDOWEVENT && event.window.event == SDL_WINDOWEVENT_EXPOSED) {
            draw();
        }
        if (event.type == SDL_MOUSEBUTTONUP && event.button.button == SDL_BUTTON_LEFT) {
            float x = 0.0F;
            float y = 0.0F;
            SDL_RenderWindowToLogical(renderer, event.button.x, event.button.y, &x, &y);
            for (int index = 0; index < 8; ++index) {
                const float left = static_cast<float>(10 + index % 4 * 70);
                const float top = static_cast<float>(82 + index / 4 * 90);
                if (x >= left && x < left + 58 && y >= top && y < top + 58) {
                    finish();
                    return static_cast<std::uint16_t>(portrait_id(index) - 405);
                }
            }
        }
        if (event.type != SDL_KEYDOWN || event.key.repeat != 0) {
            continue;
        }
        const SDL_Keycode key = event.key.keysym.sym;
        if (key >= SDLK_1 && key <= SDLK_8) {
            selected = static_cast<int>(key - SDLK_1);
            draw();
        } else if (key == SDLK_s) {
            silly = !silly;
            draw();
        } else if (key == SDLK_RETURN || key == SDLK_KP_ENTER) {
            finish();
            return static_cast<std::uint16_t>(portrait_id(selected) - 405);
        } else if (key == SDLK_ESCAPE) {
            finish();
            return std::nullopt;
        }
    }
}

std::optional<yipe::Player> choose_character(SDL_Window* window, SDL_Renderer* renderer,
                                             const GameTextures& textures,
                                             yipe::MsvcRandom& random) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Back"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Combat"},
        SDL_MessageBoxButtonData{0, 2, "Quickness"},
        SDL_MessageBoxButtonData{0, 3, "Magic"},
        SDL_MessageBoxButtonData{0, 4, "Random"},
        SDL_MessageBoxButtonData{0, 5, "Help"},
    };
    while (true) {
        const int choice = show_choice(
            window, "Create Character",
            "Choose starting attributes. Random can be rerolled before starting.",
            buttons.data(), static_cast<int>(buttons.size()));
        if (choice == 5) {
            show_original_help(window, 3);
            continue;
        }
        if (choice < 1 || choice > 4) {
            return std::nullopt;
        }
        yipe::Player player(choice == 4 ? yipe::CharacterType::warrior
                            : static_cast<yipe::CharacterType>(choice));
        if (choice == 4) {
            player.reroll_random_character(random);
        }
        bool use_stats = false;
        while (!use_stats) {
            const std::string summary =
                "Strength " + std::to_string(player.strength)
                + "   Dexterity " + std::to_string(player.dexterity)
                + "   Intelligence " + std::to_string(player.intelligence)
                + "\nHP " + std::to_string(player.health)
                + "   SP " + std::to_string(player.spell_points)
                + "   AP " + std::to_string(player.ability_points);
            constexpr std::array preset_buttons{
                SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Use Stats"},
                SDL_MessageBoxButtonData{0, 0, "Change Type"},
                SDL_MessageBoxButtonData{0, 3, "Help"},
            };
            constexpr std::array random_buttons{
                SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Use Stats"},
                SDL_MessageBoxButtonData{0, 2, "Reroll"},
                SDL_MessageBoxButtonData{0, 0, "Change Type"},
                SDL_MessageBoxButtonData{0, 3, "Help"},
            };
            const int stat_choice = choice == 4
                ? show_choice(window, "Character Attributes", summary,
                              random_buttons.data(), static_cast<int>(random_buttons.size()))
                : show_choice(window, "Character Attributes", summary,
                              preset_buttons.data(), static_cast<int>(preset_buttons.size()));
            if (stat_choice == 2 && choice == 4) {
                player.reroll_random_character(random);
            } else if (stat_choice == 3) {
                show_original_help(window, 3);
            } else if (stat_choice == 1) {
                use_stats = true;
            } else {
                break;
            }
        }
        if (!use_stats) {
            continue;
        }
        const auto name = request_text(window, "Character Name",
                                       "Enter your character's name. Leave it blank to use Yipie.",
                                       "name");
        if (!name) {
            continue;
        }
        if (!name->empty()) {
            player.name = *name;
        }
        constexpr std::array difficulty_buttons{
            SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Back"},
            SDL_MessageBoxButtonData{0, 1, "Easy"},
            SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 2, "Medium"},
            SDL_MessageBoxButtonData{0, 3, "Hard"},
        };
        const int difficulty = show_choice(
            window, "Difficulty", "Choose Easy, Medium, or Hard.",
            difficulty_buttons.data(), static_cast<int>(difficulty_buttons.size()));
        if (difficulty < 1 || difficulty > 3) {
            continue;
        }
        player.difficulty = difficulty;
        const auto portrait = choose_portrait(window, renderer, textures);
        if (!portrait) {
            continue;
        }
        player.inventory[28] = *portrait;
        constexpr std::array intro_buttons{
            SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT
                                         | SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT,
                                     0, "Start Game"},
            SDL_MessageBoxButtonData{0, 1, "Show Intro"},
        };
        if (show_choice(window, "Show Intro",
                        "Read the King's introduction before starting?",
                        intro_buttons.data(), static_cast<int>(intro_buttons.size())) == 1) {
            show_message(
                window, "The King",
                "Hello " + player.name + ".\n\n"
                    "The city of Yipe! needs you once again!\n\n"
                    "My collection of six rare stamps has been stolen!  I know that you "
                    "are a brave and loyal adventurer.  Therefore I am giving this very "
                    "important mission to you.\n\n"
                    "Please search the world and return my stamps to me.  Oh... I almost "
                    "forgot to mention something else.  All of my daughters are missing.  "
                    "I have no idea where they have gone.\n\n"
                    "I will highly reward you for returning both my stamp collection and "
                    "all of my daughters.  (I don't really remember exactly how many "
                    "princesses I have.)\n\n"
                    "Take this simple weapon and armor and begin your exploring.  Be sure "
                    "to stop by my castle from time to time to check in with me.");
        }
        return player;
    }
}

std::filesystem::path default_preference_directory() {
    std::unique_ptr<char, decltype(&SDL_free)> preference_path(
        SDL_GetPrefPath("Yipe Software", "Yipe III Rebuild"), SDL_free);
    if (!preference_path) {
        throw std::runtime_error(std::string("could not locate the settings directory: ")
                                 + SDL_GetError());
    }
    return preference_path.get();
}

std::optional<std::uint16_t> portable_key_code(SDL_Keycode key) {
    if (key >= SDLK_a && key <= SDLK_z) {
        return static_cast<std::uint16_t>('A' + (key - SDLK_a));
    }
    if (key >= SDLK_0 && key <= SDLK_9) {
        return static_cast<std::uint16_t>('0' + (key - SDLK_0));
    }
    if (key >= SDLK_F1 && key <= SDLK_F12) {
        return static_cast<std::uint16_t>(0x70 + (key - SDLK_F1));
    }
    switch (key) {
    case SDLK_BACKSPACE:
        return 0x08;
    case SDLK_TAB:
        return 0x09;
    case SDLK_RETURN:
    case SDLK_KP_ENTER:
        return 0x0d;
    case SDLK_ESCAPE:
        return 0x1b;
    case SDLK_SPACE:
        return 0x20;
    case SDLK_PAGEUP:
        return 0x21;
    case SDLK_PAGEDOWN:
        return 0x22;
    case SDLK_END:
        return 0x23;
    case SDLK_HOME:
        return 0x24;
    case SDLK_LEFT:
        return 0x25;
    case SDLK_UP:
        return 0x26;
    case SDLK_RIGHT:
        return 0x27;
    case SDLK_DOWN:
        return 0x28;
    case SDLK_INSERT:
        return 0x2d;
    case SDLK_DELETE:
        return 0x2e;
    case SDLK_KP_0:
        return 0x60;
    case SDLK_KP_1:
        return 0x61;
    case SDLK_KP_2:
        return 0x62;
    case SDLK_KP_3:
        return 0x63;
    case SDLK_KP_4:
        return 0x64;
    case SDLK_KP_5:
        return 0x65;
    case SDLK_KP_6:
        return 0x66;
    case SDLK_KP_7:
        return 0x67;
    case SDLK_KP_8:
        return 0x68;
    case SDLK_KP_9:
        return 0x69;
    default:
        return std::nullopt;
    }
}

std::uint16_t binding(const yipe::GameSettings& settings,
                      yipe::InputAction action) {
    return settings.keys[static_cast<std::size_t>(action)];
}

using Glyph = std::array<std::uint8_t, 7>;

Glyph glyph_for(char character) {
    if (character >= 'a' && character <= 'z') {
        character = static_cast<char>(character - 'a' + 'A');
    }
    switch (character) {
    case 'A': return {0x0e, 0x11, 0x11, 0x1f, 0x11, 0x11, 0x11};
    case 'B': return {0x1e, 0x11, 0x11, 0x1e, 0x11, 0x11, 0x1e};
    case 'C': return {0x0f, 0x10, 0x10, 0x10, 0x10, 0x10, 0x0f};
    case 'D': return {0x1e, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1e};
    case 'E': return {0x1f, 0x10, 0x10, 0x1e, 0x10, 0x10, 0x1f};
    case 'F': return {0x1f, 0x10, 0x10, 0x1e, 0x10, 0x10, 0x10};
    case 'G': return {0x0f, 0x10, 0x10, 0x13, 0x11, 0x11, 0x0f};
    case 'H': return {0x11, 0x11, 0x11, 0x1f, 0x11, 0x11, 0x11};
    case 'I': return {0x1f, 0x04, 0x04, 0x04, 0x04, 0x04, 0x1f};
    case 'J': return {0x01, 0x01, 0x01, 0x01, 0x11, 0x11, 0x0e};
    case 'K': return {0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11};
    case 'L': return {0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1f};
    case 'M': return {0x11, 0x1b, 0x15, 0x15, 0x11, 0x11, 0x11};
    case 'N': return {0x11, 0x19, 0x19, 0x15, 0x13, 0x13, 0x11};
    case 'O': return {0x0e, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0e};
    case 'P': return {0x1e, 0x11, 0x11, 0x1e, 0x10, 0x10, 0x10};
    case 'Q': return {0x0e, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0d};
    case 'R': return {0x1e, 0x11, 0x11, 0x1e, 0x14, 0x12, 0x11};
    case 'S': return {0x0f, 0x10, 0x10, 0x0e, 0x01, 0x01, 0x1e};
    case 'T': return {0x1f, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04};
    case 'U': return {0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0e};
    case 'V': return {0x11, 0x11, 0x11, 0x11, 0x11, 0x0a, 0x04};
    case 'W': return {0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0a};
    case 'X': return {0x11, 0x11, 0x0a, 0x04, 0x0a, 0x11, 0x11};
    case 'Y': return {0x11, 0x11, 0x0a, 0x04, 0x04, 0x04, 0x04};
    case 'Z': return {0x1f, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1f};
    case '0': return {0x0e, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0e};
    case '1': return {0x04, 0x0c, 0x14, 0x04, 0x04, 0x04, 0x1f};
    case '2': return {0x0e, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1f};
    case '3': return {0x1e, 0x01, 0x01, 0x0e, 0x01, 0x01, 0x1e};
    case '4': return {0x02, 0x06, 0x0a, 0x12, 0x1f, 0x02, 0x02};
    case '5': return {0x1f, 0x10, 0x10, 0x1e, 0x01, 0x01, 0x1e};
    case '6': return {0x0e, 0x10, 0x10, 0x1e, 0x11, 0x11, 0x0e};
    case '7': return {0x1f, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08};
    case '8': return {0x0e, 0x11, 0x11, 0x0e, 0x11, 0x11, 0x0e};
    case '9': return {0x0e, 0x11, 0x11, 0x0f, 0x01, 0x01, 0x0e};
    case '[': return {0x0e, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0e};
    case ']': return {0x0e, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0e};
    case '(': return {0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02};
    case ')': return {0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08};
    case ':': return {0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00};
    case '.': return {0x00, 0x00, 0x00, 0x00, 0x00, 0x0c, 0x0c};
    case ',': return {0x00, 0x00, 0x00, 0x00, 0x04, 0x04, 0x08};
    case '-': return {0x00, 0x00, 0x00, 0x1f, 0x00, 0x00, 0x00};
    case '/': return {0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10};
    case '!': return {0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04};
    case '\'': return {0x04, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00};
    case ' ': return {};
    default: return {0x0e, 0x11, 0x01, 0x02, 0x04, 0x00, 0x04};
    }
}

void draw_text(SDL_Renderer* renderer, int x, int y, std::string_view value,
               SDL_Color color, int scale = 1) {
    SDL_SetRenderDrawColor(renderer, color.r, color.g, color.b, color.a);
    int cursor_x = x;
    int cursor_y = y;
    for (const char character : value) {
        if (character == '\n') {
            cursor_x = x;
            cursor_y += 9 * scale;
            continue;
        }
        const Glyph glyph = glyph_for(character);
        for (std::size_t row = 0; row < glyph.size(); ++row) {
            for (int column = 0; column < 5; ++column) {
                if ((glyph[row] & (1u << static_cast<unsigned>(4 - column))) == 0) {
                    continue;
                }
                const SDL_Rect pixel{
                    cursor_x + column * scale,
                    cursor_y + static_cast<int>(row) * scale,
                    scale,
                    scale,
                };
                SDL_RenderFillRect(renderer, &pixel);
            }
        }
        cursor_x += 6 * scale;
    }
}

struct CombatButton {
    SDL_Rect bounds;
    int action;
    std::string label;
};

int combat_action_id(yipe::InputAction action) {
    switch (action) {
    case yipe::InputAction::attack: return 1;
    case yipe::InputAction::use_item: return 2;
    case yipe::InputAction::cast: return 3;
    case yipe::InputAction::run: return 4;
    case yipe::InputAction::special: return 6;
    default: return 0;
    }
}

void render_combat_picker(SDL_Renderer* renderer, const std::string& status,
                          const std::array<CombatButton, 6>& buttons,
                          int hovered_button) {
    SDL_SetRenderDrawColor(renderer, 24, 28, 36, 255);
    SDL_RenderClear(renderer);
    const SDL_Rect panel{12, 10, map_width - 24, map_height - 20};
    SDL_SetRenderDrawColor(renderer, 224, 216, 184, 255);
    SDL_RenderFillRect(renderer, &panel);
    SDL_SetRenderDrawColor(renderer, 52, 42, 28, 255);
    SDL_RenderDrawRect(renderer, &panel);
    draw_text(renderer, 172, 20, "COMBAT", {52, 42, 28, 255}, 2);
    draw_text(renderer, 24, 46, status, {32, 28, 24, 255});

    for (std::size_t index = 0; index < buttons.size(); ++index) {
        const CombatButton& button = buttons[index];
        const bool hovered = static_cast<int>(index) == hovered_button;
        SDL_SetRenderDrawColor(renderer, hovered ? 116 : 76,
                              hovered ? 104 : 68, hovered ? 72 : 48, 255);
        SDL_RenderFillRect(renderer, &button.bounds);
        SDL_SetRenderDrawColor(renderer, 32, 28, 24, 255);
        SDL_RenderDrawRect(renderer, &button.bounds);
        const int text_width = static_cast<int>(button.label.size()) * 6;
        draw_text(renderer,
                  button.bounds.x + std::max(4, (button.bounds.w - text_width) / 2),
                  button.bounds.y + 11, button.label, {255, 248, 220, 255});
    }
    SDL_RenderPresent(renderer);
}

int choose_combat_action(SDL_Renderer* renderer, const std::string& status,
                         const yipe::GameSettings& settings) {
    const auto label = [&settings](std::string_view name,
                                   yipe::InputAction action) {
        return std::string(name) + " ["
            + std::string(yipe::key_binding_name(binding(settings, action))) + "]";
    };
    const std::array<CombatButton, 6> buttons{{
        {{32, 142, 352, 29}, 1, label("Attack", yipe::InputAction::attack)},
        {{32, 175, 352, 29}, 2, label("Use Item", yipe::InputAction::use_item)},
        {{32, 208, 352, 29}, 3, label("Cast", yipe::InputAction::cast)},
        {{32, 241, 352, 29}, 4, label("Flee", yipe::InputAction::run)},
        {{32, 274, 352, 29}, 5, "Next Target [Arrows/Tab]"},
        {{32, 307, 352, 29}, 6, label("Special", yipe::InputAction::special)},
    }};
    const Cursor arrow(SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_ARROW), SDL_FreeCursor);
    const Cursor hand(SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_HAND), SDL_FreeCursor);
    int hovered_button = -1;
    render_combat_picker(renderer, status, buttons, hovered_button);

    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            throw std::runtime_error(std::string("SDL event wait failed: ")
                                     + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            return 0;
        }
        if (event.type == SDL_WINDOWEVENT
            && (event.window.event == SDL_WINDOWEVENT_EXPOSED
                || event.window.event == SDL_WINDOWEVENT_FOCUS_GAINED)) {
            render_combat_picker(renderer, status, buttons, hovered_button);
            continue;
        }
        if (event.type == SDL_KEYDOWN && event.key.repeat == 0) {
            const std::optional<std::uint16_t> key =
                portable_key_code(event.key.keysym.sym);
            if (key) {
                const std::optional<yipe::InputAction> action =
                    yipe::combat_action_for_key(settings, *key);
                if (action) {
                    return combat_action_id(*action);
                }
            }
            if (event.key.keysym.sym == SDLK_TAB
                || event.key.keysym.sym == SDLK_LEFT
                || event.key.keysym.sym == SDLK_RIGHT
                || event.key.keysym.sym == SDLK_UP
                || event.key.keysym.sym == SDLK_DOWN) {
                return 5;
            }
            if (event.key.keysym.sym == SDLK_RETURN
                || event.key.keysym.sym == SDLK_KP_ENTER) {
                return 1;
            }
            if (event.key.keysym.sym == SDLK_ESCAPE) {
                return 4;
            }
        }
        if (event.type == SDL_MOUSEMOTION) {
            float x = 0.0F;
            float y = 0.0F;
            SDL_RenderWindowToLogical(renderer, event.motion.x, event.motion.y,
                                      &x, &y);
            hovered_button = -1;
            for (std::size_t index = 0; index < buttons.size(); ++index) {
                const SDL_Rect& bounds = buttons[index].bounds;
                if (x >= static_cast<float>(bounds.x)
                    && x < static_cast<float>(bounds.x + bounds.w)
                    && y >= static_cast<float>(bounds.y)
                    && y < static_cast<float>(bounds.y + bounds.h)) {
                    hovered_button = static_cast<int>(index);
                    break;
                }
            }
            SDL_SetCursor(hovered_button >= 0 ? hand.get() : arrow.get());
            render_combat_picker(renderer, status, buttons, hovered_button);
        }
        if (event.type == SDL_MOUSEBUTTONUP
            && event.button.button == SDL_BUTTON_LEFT) {
            float x = 0.0F;
            float y = 0.0F;
            SDL_RenderWindowToLogical(renderer, event.button.x, event.button.y,
                                      &x, &y);
            for (const CombatButton& button : buttons) {
                if (x >= static_cast<float>(button.bounds.x)
                    && x < static_cast<float>(button.bounds.x + button.bounds.w)
                    && y >= static_cast<float>(button.bounds.y)
                    && y < static_cast<float>(
                        button.bounds.y + button.bounds.h)) {
                    return button.action;
                }
            }
        }
    }
}

std::optional<std::uint16_t> capture_key_binding(
    SDL_Window* window, yipe::InputAction action) {
    const std::string action_name(yipe::input_action_name(action));
    show_message(window, "Edit Keys",
                 "Press OK, then press the new key for " + action_name
                     + ".\n\nPress Escape to cancel.");
    const std::string original_title = SDL_GetWindowTitle(window);
    SDL_SetWindowTitle(window, ("Yipe! III - press key for " + action_name).c_str());
    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            SDL_SetWindowTitle(window, original_title.c_str());
            throw std::runtime_error(std::string("SDL event wait failed: ")
                                     + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            SDL_PushEvent(&event);
            SDL_SetWindowTitle(window, original_title.c_str());
            return std::nullopt;
        }
        if (event.type != SDL_KEYDOWN || event.key.repeat != 0) {
            continue;
        }
        SDL_SetWindowTitle(window, original_title.c_str());
        if (event.key.keysym.sym == SDLK_ESCAPE) {
            return std::nullopt;
        }
        const std::optional<std::uint16_t> key =
            portable_key_code(event.key.keysym.sym);
        if (!key) {
            show_message(window, "Edit Keys",
                         "That key is not supported. Letters, numbers, arrows, "
                         "navigation keys, the numeric keypad, and F1-F12 are supported.");
        }
        return key;
    }
}

std::optional<yipe::InputAction> choose_binding_action(
    SDL_Window* window, bool combat) {
    constexpr std::array movement_buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Back"},
        SDL_MessageBoxButtonData{0, 1, "Northwest"},
        SDL_MessageBoxButtonData{0, 2, "North"},
        SDL_MessageBoxButtonData{0, 3, "Northeast"},
        SDL_MessageBoxButtonData{0, 4, "West"},
        SDL_MessageBoxButtonData{0, 5, "East"},
        SDL_MessageBoxButtonData{0, 6, "Southwest"},
        SDL_MessageBoxButtonData{0, 7, "South"},
        SDL_MessageBoxButtonData{0, 8, "Southeast"},
    };
    constexpr std::array combat_buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Back"},
        SDL_MessageBoxButtonData{0, 9, "Attack"},
        SDL_MessageBoxButtonData{0, 10, "Run"},
        SDL_MessageBoxButtonData{0, 11, "Cast"},
        SDL_MessageBoxButtonData{0, 12, "Use Item"},
        SDL_MessageBoxButtonData{0, 13, "Special"},
    };
    const int choice = combat
        ? show_choice(window, "Combat Keys", "Choose a command to change.",
                      combat_buttons.data(), static_cast<int>(combat_buttons.size()))
        : show_choice(window, "Movement Keys", "Choose a direction to change.",
                      movement_buttons.data(), static_cast<int>(movement_buttons.size()));
    if (choice < 1 || choice > static_cast<int>(yipe::input_action_count)) {
        return std::nullopt;
    }
    return static_cast<yipe::InputAction>(choice - 1);
}

std::string settings_summary(const yipe::GameSettings& settings) {
    std::ostringstream summary;
    summary << "Sound: " << (settings.sound_enabled ? "On" : "Off")
            << "\n\nMovement";
    for (std::size_t index = 0; index < 8; ++index) {
        const auto action = static_cast<yipe::InputAction>(index);
        summary << "\n" << yipe::input_action_name(action) << ": "
                << yipe::key_binding_name(settings.keys[index]);
    }
    summary << "\n\nCombat";
    for (std::size_t index = 8; index < yipe::input_action_count; ++index) {
        const auto action = static_cast<yipe::InputAction>(index);
        summary << "\n" << yipe::input_action_name(action) << ": "
                << yipe::key_binding_name(settings.keys[index]);
    }
    return summary.str();
}

void show_help(SDL_Window* window, const yipe::GameSettings& settings) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT
                                     | SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 0, "Done"},
        SDL_MessageBoxButtonData{0, 1, "Original Manual"},
        SDL_MessageBoxButtonData{0, 2, "Current Keys"},
        SDL_MessageBoxButtonData{0, 3, "About"},
    };
    while (true) {
        const int choice = show_choice(window, "Yipe! III Help",
            "Rebuild Controls\n\n"
            "Startup: N New Game, L Load, S Settings, H High Scores.\n"
            "F1 opens this help at startup or while exploring.\n\n"
            "Explore with your configured movement keys or WASD.\n"
            "Walk onto a monster to fight; enter buildings to visit shops.\n"
            "Ctrl+I: Inventory    Ctrl+C: Cast Spell\n"
            "Ctrl+T: Player Status    Ctrl+E: Equipment\n"
            "Ctrl+H: High Scores    Ctrl+N: New Game\n"
            "Ctrl+O: Open saved game    Ctrl+Shift+O: Other\n"
            "F5 or Ctrl+S: Save    Ctrl+Shift+S: Save As\n"
            "F10: Settings; first save of a new game asks for a file.\n"
            "Q or Escape: Quit, with a save choice.\n\n"
            "In combat, use the configured action keys or mouse buttons.\n"
            "Arrow keys and Tab cycle targets.\n"
            "Current Keys lists your movement and combat bindings.\n\n"
            "Original Manual preserves the 2003 help, including historical\n"
            "registration information. Its interface descriptions differ\n"
            "from this rebuild; use the controls above to play.",
            buttons.data(), static_cast<int>(buttons.size()));
        if (choice == 1) {
            show_original_help(window);
        } else if (choice == 2) {
            show_message(window, "Current Keys", settings_summary(settings));
        } else if (choice == 3) {
            show_message(window, "About Yipe! III",
                "Original game: Yipe! III, Version 1.16\n"
                "Copyright (C) 2002-03, Kevin Kinell\n\n"
                "This application is a source reconstruction of the supplied\n"
                "game, with an adapted macOS interface.\n\n"
                "Original contact information (2003):\n"
                "YipeSFT@aol.com\nwww.yipesoftware.com");
        } else {
            return;
        }
    }
}

void show_settings(SDL_Window* window, yipe::GameSettings& settings,
                   const std::filesystem::path& path, AudioPlayer& audio) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Done"},
        SDL_MessageBoxButtonData{0, 1, "Toggle Sound"},
        SDL_MessageBoxButtonData{0, 2, "Movement Keys"},
        SDL_MessageBoxButtonData{0, 3, "Combat Keys"},
        SDL_MessageBoxButtonData{0, 4, "Restore Defaults"},
    };
    bool changed = false;
    while (true) {
        const int choice = show_choice(window, "Settings", settings_summary(settings),
                                       buttons.data(), static_cast<int>(buttons.size()));
        if (choice == 1) {
            if (!settings.sound_enabled && !audio.available()) {
                show_message(window, "Audio Unavailable", audio.error());
                continue;
            }
            settings.sound_enabled = !settings.sound_enabled;
            if (!settings.sound_enabled) {
                audio.stop();
            } else {
                audio.play(152, true);
            }
            changed = true;
        } else if (choice == 2 || choice == 3) {
            const std::optional<yipe::InputAction> action =
                choose_binding_action(window, choice == 3);
            if (!action) {
                continue;
            }
            const std::optional<std::uint16_t> key =
                capture_key_binding(window, *action);
            if (key) {
                settings.keys[static_cast<std::size_t>(*action)] = *key;
                changed = true;
            }
        } else if (choice == 4) {
            settings = yipe::GameSettings{};
            if (!audio.available()) {
                settings.sound_enabled = false;
            }
            changed = true;
        } else {
            if (changed) {
                try {
                    yipe::save_settings_atomic(path, settings);
                } catch (const std::runtime_error& error) {
                    show_message(window, "Settings Save Failed", error.what());
                }
            }
            return;
        }
    }
}

std::string purchase_message(const yipe::ShopStockItem& item,
                             yipe::PurchaseStatus status) {
    switch (status) {
    case yipe::PurchaseStatus::purchased:
        return "You purchased " + std::string(item.name) + ".";
    case yipe::PurchaseStatus::insufficient_gold:
        return "You don't have enough gold to purchase that item.";
    case yipe::PurchaseStatus::inventory_full:
        return "You don't have room to carry that item.";
    case yipe::PurchaseStatus::already_equipped:
        return "You already have this item or something better equipped.";
    }
    throw std::logic_error("unknown purchase result");
}

void show_freds_shop(SDL_Window* window, yipe::Player& player) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Buy"},
        SDL_MessageBoxButtonData{0, 2, "Next"},
        SDL_MessageBoxButtonData{0, 3, "Previous"},
    };
    const auto& stock = yipe::freds_miscellaneous_stock();
    std::size_t selected = 0;
    while (true) {
        const yipe::ShopStockItem& item = stock[selected];
        std::ostringstream message;
        message << item.name << "\nPrice: " << item.price << " gold\nYour gold: "
                << player.gold << "\n\nItem " << selected + 1 << " of " << stock.size();
        switch (show_choice(window, "Fred's Miscellaneous Shop", message.str(),
                            buttons.data(), static_cast<int>(buttons.size()))) {
        case 1: {
            const yipe::PurchaseResult result = yipe::purchase_shop_item(player, item);
            show_message(window, "Fred's Miscellaneous Shop",
                         purchase_message(item, result.status));
            break;
        }
        case 2:
            selected = (selected + 1) % stock.size();
            break;
        case 3:
            selected = (selected + stock.size() - 1) % stock.size();
            break;
        default:
            return;
        }
    }
}

std::string equipment_name(yipe::EquipmentKind kind, int rank) {
    static const yipe::ItemNames names =
        yipe::load_item_names(runtime_assets("Yipe! III").item_names);
    if (rank < 1) {
        return "None";
    }
    const auto index = static_cast<std::size_t>(rank - 1);
    if (kind == yipe::EquipmentKind::weapon && index < names.weapons.size()) {
        return names.weapons[index];
    }
    if (kind == yipe::EquipmentKind::armor && index < names.armor.size()) {
        return names.armor[index];
    }
    return "Rank " + std::to_string(rank);
}

void show_equipment_shop(SDL_Window* window, yipe::Player& player, int map_id,
                         yipe::EquipmentKind kind) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Buy"},
        SDL_MessageBoxButtonData{0, 2, "Next"},
        SDL_MessageBoxButtonData{0, 3, "Sell"},
    };
    constexpr std::array confirm_buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Keep It"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Sell"},
    };
    const char* title = kind == yipe::EquipmentKind::weapon ? "Weapon Shop" : "Armor Shop";
    const auto stock = yipe::equipment_shop_stock(kind, map_id);
    std::size_t selected = 0;
    while (true) {
        const yipe::EquipmentShopItem item = stock[selected];
        const int current_rank = kind == yipe::EquipmentKind::weapon
            ? player.weapon_rank : player.armor_rank;
        std::ostringstream message;
        message << equipment_name(kind, item.rank) << " (rank " << item.rank << ")\nCost: "
                << item.price << " gold\n\nCurrent: "
                << equipment_name(kind, current_rank) << "\nSell value: "
                << yipe::equipment_sale_value(player, kind) << " gold\nYour gold: "
                << player.gold << "\n\nItem " << selected + 1 << " of " << stock.size();
        const int choice = show_choice(window, title, message.str(), buttons.data(),
                                       static_cast<int>(buttons.size()));
        if (choice == 1) {
            const auto status = yipe::purchase_equipment(player, kind, item.rank);
            if (status == yipe::EquipmentTransactionStatus::purchased) {
                show_message(window, title, "Purchase complete.");
            } else if (status == yipe::EquipmentTransactionStatus::must_sell_current) {
                show_message(window, title,
                             "You must first sell your current equipment.");
            } else {
                show_message(window, title,
                             "You don't have enough money to buy that.");
            }
        } else if (choice == 2) {
            selected = (selected + 1) % stock.size();
        } else if (choice == 3) {
            if (yipe::equipment_sale_value(player, kind) == 0) {
                show_message(window, title, "You have nothing to sell.");
                continue;
            }
            if (yipe::equipment_sale_requires_confirmation(player, kind)
                && show_choice(window, title,
                               "You will not be able to afford anything better. Sell anyway?",
                               confirm_buttons.data(),
                               static_cast<int>(confirm_buttons.size())) != 1) {
                continue;
            }
            const int value = yipe::equipment_sale_value(player, kind);
            static_cast<void>(yipe::sell_equipment(player, kind));
            show_message(window, title,
                         "Equipment sold for " + std::to_string(value) + " gold.");
        } else {
            return;
        }
    }
}

void show_healing_shop(SDL_Window* window, yipe::Player& player,
                       yipe::MsvcRandom& random) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Order"},
        SDL_MessageBoxButtonData{0, 2, "Next"},
        SDL_MessageBoxButtonData{0, 3, "Previous"},
    };
    const auto& services = yipe::healing_services();
    std::size_t selected = 0;
    while (true) {
        const yipe::HealingService service = services[selected];
        std::ostringstream message;
        message << service.name << "\nPrice: " << service.price << " gold\nHP: "
                << player.health << "/" << player.maximum_health << "\nYour gold: "
                << player.gold << "\n\nItem " << selected + 1 << " of " << services.size();
        switch (show_choice(window, "Heal Shop", message.str(), buttons.data(),
                            static_cast<int>(buttons.size()))) {
        case 1: {
            const yipe::ServiceResult result =
                yipe::purchase_healing(player, selected, random);
            const std::string response = result.status == yipe::ServiceStatus::completed
                ? "Health restored by " + std::to_string(result.amount) + "."
                : result.status == yipe::ServiceStatus::already_full
                    ? "Your health is already full."
                    : "You don't have enough gold.";
            show_message(window, "Heal Shop", response);
            break;
        }
        case 2:
            selected = (selected + 1) % services.size();
            break;
        case 3:
            selected = (selected + services.size() - 1) % services.size();
            break;
        default:
            return;
        }
    }
}

void show_magic_shop(SDL_Window* window, yipe::Player& player,
                     yipe::MsvcRandom& random) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{0, 1, "Small SP Heal"},
        SDL_MessageBoxButtonData{0, 2, "Full SP Heal"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 3,
                                 "Learn Spell"},
    };
    while (true) {
        const std::optional<yipe::SpellOffer> offer = yipe::next_spell_offer(player);
        std::ostringstream message;
        message << "SP: " << player.spell_points << "/" << player.maximum_spell_points
                << "  Gold: " << player.gold << "\n\nSmall SP Heal: 50 gold\n"
                << "Full SP Heal: 250 gold\n\n";
        if (offer) {
            message << "Available spell: " << offer->name << " (" << offer->price
                    << " gold)\n" << offer->description;
        } else {
            message << "You cannot learn any new spells at this time.";
        }
        const int choice = show_choice(window, "Magic Shop", message.str(), buttons.data(),
                                       static_cast<int>(buttons.size()));
        if (choice == 1 || choice == 2) {
            const yipe::ServiceResult result =
                yipe::purchase_spell_point_healing(player, choice == 2, random);
            const std::string response = result.status == yipe::ServiceStatus::completed
                ? "Spell points restored by " + std::to_string(result.amount) + "."
                : result.status == yipe::ServiceStatus::already_full
                    ? "Your spell points are already full."
                    : "You don't have enough gold.";
            show_message(window, "Magic Shop", response);
        } else if (choice == 3) {
            const yipe::SpellLearningStatus status = yipe::learn_next_spell(player);
            const std::string response = status == yipe::SpellLearningStatus::learned
                ? "You learned the new spell."
                : status == yipe::SpellLearningStatus::insufficient_gold
                    ? "You don't have enough gold."
                    : "You cannot learn any new spells at this time.";
            show_message(window, "Magic Shop", response);
        } else {
            return;
        }
    }
}

void show_ability_training(SDL_Window* window, yipe::Player& player, int environment) {
    const std::optional<yipe::AbilityOffer> offer =
        yipe::ability_offer_for_environment(environment);
    if (!offer) {
        show_message(window, "Special Ability Training Center",
                     "No ability is taught here.");
        return;
    }
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1,
                                 "Learn Ability"},
    };
    std::ostringstream message;
    message << offer->name << "\nPrice: " << offer->price << " gold\nYour gold: "
            << player.gold << "\n\n" << offer->description;
    if (show_choice(window, "Special Ability Training Center", message.str(),
                    buttons.data(), static_cast<int>(buttons.size())) != 1) {
        return;
    }
    const yipe::AbilityTrainingStatus status = yipe::train_ability(player, environment);
    const std::string response = status == yipe::AbilityTrainingStatus::learned
        ? "Ability training complete."
        : status == yipe::AbilityTrainingStatus::already_trained
            ? "You are already trained in this ability."
            : status == yipe::AbilityTrainingStatus::insufficient_gold
                ? "You don't have enough gold to purchase this special ability."
                : "No ability is taught here.";
    show_message(window, "Special Ability Training Center", response);
}

std::string wager_status_message(yipe::WagerStatus status) {
    switch (status) {
    case yipe::WagerStatus::placed:
        return "Bet placed.";
    case yipe::WagerStatus::insufficient_gold:
        return "You don't have enough gold.";
    case yipe::WagerStatus::maximum_reached:
        return "The maximum bet is 250 gold.";
    case yipe::WagerStatus::invalid_amount:
        return "That bet is invalid.";
    }
    throw std::logic_error("unknown wager result");
}

int choose_bet(SDL_Window* window, const char* title) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Cancel"},
        SDL_MessageBoxButtonData{0, 5, "5"},
        SDL_MessageBoxButtonData{0, 10, "10"},
        SDL_MessageBoxButtonData{0, 25, "25"},
        SDL_MessageBoxButtonData{0, 50, "50"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 100, "100"},
    };
    return show_choice(window, title, "How much gold would you like to add?",
                       buttons.data(), static_cast<int>(buttons.size()));
}

void show_slots(SDL_Window* window, yipe::Player& player, yipe::MsvcRandom& random,
                AudioPlayer& audio, const yipe::GameSettings& settings) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{0, 1, "Insert 10"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 2, "Spin"},
        SDL_MessageBoxButtonData{0, 3, "Collect"},
    };
    yipe::SlotMachineState state{player.gold, 0};
    while (true) {
        std::ostringstream message;
        message << "Gold: " << state.gold << "\nCredits: " << state.credits
                << "\n\nEach spin costs 10 credits.";
        const int choice = show_choice(window, "Slot Machine", message.str(),
                                       buttons.data(), static_cast<int>(buttons.size()));
        if (choice == 1) {
            if (!yipe::insert_slot_credits(state)) {
                show_message(window, "Slot Machine", "You need 10 gold to buy credits.");
            }
        } else if (choice == 2) {
            const yipe::SlotSpinResult result = yipe::spin_slots(state, random);
            if (!result.spun) {
                show_message(window, "Slot Machine", "You need 10 credits to spin.");
                continue;
            }
            std::ostringstream outcome;
            outcome << result.symbols[0] << " | " << result.symbols[1] << " | "
                    << result.symbols[2] << "\n";
            if (result.payout > 0) {
                audio.play(145, settings.sound_enabled);
                outcome << "You won " << result.payout << " credits!";
            } else {
                outcome << "No match.";
            }
            show_message(window, "Slot Machine", outcome.str());
        } else if (choice == 3) {
            yipe::collect_slot_credits(state);
        } else {
            player.gold = state.gold;
            return;
        }
    }
}

void show_dot_racing(SDL_Window* window, yipe::Player& player,
                     yipe::MsvcRandom& random, AudioPlayer& audio,
                     const yipe::GameSettings& settings) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{0, 1, "Add Bet"},
        SDL_MessageBoxButtonData{0, 2, "Clear Bet"},
        SDL_MessageBoxButtonData{0, 3, "Select Dot"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 4,
                                 "Start Race"},
    };
    constexpr std::array dot_buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Cancel"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Dot 1"},
        SDL_MessageBoxButtonData{0, 2, "Dot 2"},
        SDL_MessageBoxButtonData{0, 3, "Dot 3"},
    };
    yipe::CasinoWager wager{player.gold, 0};
    int selected_dot = 0;
    while (true) {
        std::ostringstream message;
        message << "Gold: " << wager.gold << "\nCurrent bet: " << wager.bet
                << "\nSelected dot: ";
        if (selected_dot == 0) {
            message << "none";
        } else {
            message << selected_dot;
        }
        const int choice = show_choice(window, "Dot Racing", message.str(),
                                       buttons.data(), static_cast<int>(buttons.size()));
        if (choice == 1) {
            const int amount = choose_bet(window, "Dot Racing");
            if (amount != 0) {
                const yipe::WagerStatus status = yipe::place_casino_wager(wager, amount);
                if (status != yipe::WagerStatus::placed) {
                    show_message(window, "Dot Racing", wager_status_message(status));
                }
            }
        } else if (choice == 2) {
            yipe::clear_casino_wager(wager);
        } else if (choice == 3) {
            const int dot = show_choice(window, "Dot Racing", "Choose a dot.",
                                        dot_buttons.data(),
                                        static_cast<int>(dot_buttons.size()));
            if (dot != 0) {
                selected_dot = dot;
            }
        } else if (choice == 4) {
            if (wager.bet == 0 || selected_dot == 0) {
                show_message(window, "Dot Racing",
                             "Enter a bet and select a dot before starting the race.");
                continue;
            }
            audio.play(35, settings.sound_enabled);
            const yipe::DotRaceResult result =
                yipe::race_dots(wager, selected_dot, random);
            std::ostringstream outcome;
            outcome << "Dot " << result.winner << " won. ";
            if (result.payout > 0) {
                outcome << "You won " << result.payout << " gold!";
            } else {
                outcome << "Your bet was lost.";
            }
            show_message(window, "Dot Racing", outcome.str());
        } else {
            yipe::clear_casino_wager(wager);
            player.gold = wager.gold;
            return;
        }
    }
}

void show_darts(SDL_Window* window, yipe::Player& player, yipe::MsvcRandom& random,
                int& high_score, AudioPlayer& audio,
                const yipe::GameSettings& settings) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{0, 1, "Add Bet"},
        SDL_MessageBoxButtonData{0, 2, "Clear Bet"},
        SDL_MessageBoxButtonData{0, 3, "Aim Up"},
        SDL_MessageBoxButtonData{0, 4, "Aim Down"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 5, "Throw"},
    };
    yipe::CasinoWager wager{player.gold, 0};
    std::array<int, 3> scores{};
    int throw_index = 0;
    int aim_y = yipe::initial_dart_aim;
    while (true) {
        std::ostringstream message;
        message << "Gold: " << wager.gold << "\nCurrent bet: " << wager.bet
                << "\nAim: " << aim_y << "\nScores: " << scores[0] << ", "
                << scores[1] << ", " << scores[2] << "\nToday's high score: "
                << high_score;
        const int choice = show_choice(window, "Darts", message.str(), buttons.data(),
                                       static_cast<int>(buttons.size()));
        if (choice == 1) {
            if (throw_index != 0) {
                show_message(window, "Darts", "Finish the current round first.");
                continue;
            }
            const int amount = choose_bet(window, "Darts");
            if (amount != 0) {
                const yipe::WagerStatus status = yipe::place_casino_wager(wager, amount);
                if (status != yipe::WagerStatus::placed) {
                    show_message(window, "Darts", wager_status_message(status));
                }
            }
        } else if (choice == 2) {
            if (throw_index == 0) {
                yipe::clear_casino_wager(wager);
            }
        } else if (choice == 3 || choice == 4) {
            aim_y = yipe::adjust_dart_aim(aim_y, choice == 3);
        } else if (choice == 5) {
            if (wager.bet == 0) {
                show_message(window, "Darts", "You must enter a bet before throwing.");
                continue;
            }
            audio.play(148, settings.sound_enabled);
            const yipe::DartThrowResult result =
                yipe::throw_dart(aim_y, throw_index, random);
            audio.play(147, settings.sound_enabled);
            scores[static_cast<std::size_t>(throw_index)] = result.score;
            ++throw_index;
            aim_y = yipe::initial_dart_aim;
            show_message(window, "Darts",
                         "Throw " + std::to_string(throw_index) + " scored "
                             + std::to_string(result.score) + ".");
            if (throw_index == 3) {
                const int total = scores[0] + scores[1] + scores[2];
                const int payout = yipe::dart_round_payout(scores, wager.bet);
                wager.gold += payout;
                wager.bet = 0;
                high_score = std::max(high_score, total);
                std::ostringstream outcome;
                outcome << "Round score: " << total << "\n";
                if (payout > 0) {
                    outcome << "You won " << payout << " gold!";
                } else {
                    outcome << "No payout.";
                }
                show_message(window, "Darts", outcome.str());
                scores.fill(0);
                throw_index = 0;
            }
        } else {
            player.gold = wager.gold;
            return;
        }
    }
}

void show_casino(SDL_Window* window, yipe::Player& player, yipe::MsvcRandom& random,
                 AudioPlayer& audio, const yipe::GameSettings& settings) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Leave"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Slots"},
        SDL_MessageBoxButtonData{0, 2, "Dot Racing"},
        SDL_MessageBoxButtonData{0, 3, "Darts"},
    };
    int darts_high_score = 0;
    while (true) {
        const int choice = show_choice(
            window, "Yipe! Casino", "Welcome to the Yipe! Casino.\nGold: "
                                        + std::to_string(player.gold),
            buttons.data(), static_cast<int>(buttons.size()));
        if (choice == 1) {
            show_slots(window, player, random, audio, settings);
        } else if (choice == 2) {
            show_dot_racing(window, player, random, audio, settings);
        } else if (choice == 3) {
            show_darts(window, player, random, darts_high_score, audio, settings);
        } else {
            return;
        }
    }
}

std::string inventory_item_name(std::uint16_t item_id) {
    constexpr std::array<std::string_view, 10> names{
        "French Fries", "Juicy Cheeseburger", "Spell Point Potion",
        "Monster Repellent", "Return to Surface Potion", "Strength Potion",
        "Dexterity Potion", "Intelligence Potion", "Potion of HP Increase",
        "Potion of SP Increase",
    };
    if (item_id >= 1 && item_id <= names.size()) {
        return std::string(names[item_id - 1]);
    }
    return "Item " + std::to_string(item_id);
}

std::optional<std::size_t> choose_inventory_item(SDL_Window* window,
                                                 const yipe::Player& player) {
    std::vector<std::size_t> occupied;
    for (std::size_t slot = 0; slot < yipe::Player::carried_inventory_size; ++slot) {
        if (player.inventory[slot] != 0) {
            occupied.push_back(slot);
        }
    }
    if (occupied.empty()) {
        show_message(window, "Inventory", "You are not carrying any usable items.");
        return std::nullopt;
    }

    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Back"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Use"},
        SDL_MessageBoxButtonData{0, 2, "Next"},
    };
    std::size_t selected = 0;
    while (true) {
        const std::size_t slot = occupied[selected];
        std::ostringstream message;
        message << inventory_item_name(player.inventory[slot]) << "\nSlot " << slot + 1
                << "\n\nItem " << selected + 1 << " of " << occupied.size();
        const int choice = show_choice(window, "Inventory", message.str(), buttons.data(),
                                       static_cast<int>(buttons.size()));
        if (choice == 1) {
            return slot;
        }
        if (choice == 2) {
            selected = (selected + 1) % occupied.size();
        } else {
            return std::nullopt;
        }
    }
}

struct SpellSelection {
    std::size_t index;
    yipe::SpellPower power;
};

std::optional<SpellSelection> choose_spell(SDL_Window* window,
                                           const yipe::Player& player,
                                           bool in_combat) {
    const auto& definitions = yipe::spell_definitions();
    std::vector<std::size_t> available;
    for (std::size_t index = 0; index < definitions.size(); ++index) {
        const yipe::SpellUsage usage = definitions[index].usage;
        const bool right_context = in_combat
            ? usage != yipe::SpellUsage::non_combat
            : usage != yipe::SpellUsage::combat;
        if (player.spells[index] != 0 && right_context) {
            available.push_back(index);
        }
    }
    if (available.empty()) {
        show_message(window, "Spells", in_combat
                         ? "You do not know any combat spells."
                         : "You do not know any spells that can be cast here.");
        return std::nullopt;
    }

    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Back"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1,
                                 "Low Power"},
        SDL_MessageBoxButtonData{0, 2, "High Power"},
        SDL_MessageBoxButtonData{0, 3, "Next"},
    };
    std::size_t selected = 0;
    while (true) {
        const std::size_t index = available[selected];
        const yipe::SpellDefinition& spell = definitions[index];
        std::ostringstream message;
        message << spell.name << "\n" << spell.description << "\n\nLow power: "
                << yipe::spell_point_cost(player, index, yipe::SpellPower::low)
                << " SP\nHigh power: "
                << yipe::spell_point_cost(player, index, yipe::SpellPower::high)
                << " SP\nYour SP: " << player.spell_points << "/"
                << player.maximum_spell_points << "\n\nSpell " << selected + 1
                << " of " << available.size();
        const int choice = show_choice(window, "Spells", message.str(), buttons.data(),
                                       static_cast<int>(buttons.size()));
        if (choice == 1 || choice == 2) {
            return SpellSelection{index, choice == 1 ? yipe::SpellPower::low
                                                     : yipe::SpellPower::high};
        }
        if (choice == 3) {
            selected = (selected + 1) % available.size();
        } else {
            return std::nullopt;
        }
    }
}

std::string spell_failure_message(yipe::SpellCastStatus status) {
    switch (status) {
    case yipe::SpellCastStatus::unlearned:
        return "You have not learned that spell.";
    case yipe::SpellCastStatus::wrong_context:
        return "That spell cannot be cast here.";
    case yipe::SpellCastStatus::insufficient_spell_points:
        return "You do not have enough spell points.";
    case yipe::SpellCastStatus::unavailable:
        return "That spell cannot take effect right now.";
    case yipe::SpellCastStatus::cast:
        break;
    }
    return {};
}

std::string monster_defeat_page(const yipe::Player& player, int page) {
    if (page < 1 || page > 5) {
        throw std::out_of_range("monster statistics page is outside one through five");
    }
    std::uint32_t total = 0;
    for (std::size_t index = 0; index < 41; ++index) {
        total += static_cast<std::uint32_t>(player.monster_defeats[index]);
    }
    std::ostringstream message;
    message << "Kills: " << static_cast<std::int32_t>(total)
            << "  Deaths: " << player.death_count << "\n\n";
    if (!player.monster_defeat_history_complete) {
        message << "Older save: all kill counts shown start at the statistics upgrade.\n"
                   "Earlier per-type counts are unavailable.\n\n";
    }
    for (int type = (page - 1) * 9 + 1; type <= std::min(page * 9, 41); ++type) {
        const std::int32_t count = player.monster_defeats[static_cast<std::size_t>(type - 1)];
        const auto monster = yipe::original_monster_definition(type);
        if (!monster) {
            throw std::logic_error("statistics monster is missing from the catalog");
        }
        // 0x0044cb20 reveals names after a positive kill count, not an encounter.
        message << (count > 0 ? monster->name : "?????") << ": " << count << '\n';
    }
    message << "\nPage " << page << " of 5";
    return message.str();
}

void show_monster_defeats(SDL_Window* window, const yipe::Player& player) {
    int page = 1;
    while (true) {
        std::vector<SDL_MessageBoxButtonData> buttons{
            {SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Back"}};
        if (page > 1) {
            buttons.push_back({0, 1, "Previous"});
        }
        if (page < 5) {
            buttons.push_back({SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 2, "Next"});
        } else {
            buttons.front().flags |= SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT;
        }
        const int choice = show_choice(window, "Monster Kills", monster_defeat_page(player, page),
                                       buttons.data(), static_cast<int>(buttons.size()));
        if (choice == 1) {
            --page;
        } else if (choice == 2) {
            ++page;
        } else {
            return;
        }
    }
}

void record_player_high_scores(const std::filesystem::path& path, yipe::HighScores& scores,
                               const yipe::Player& player) {
    yipe::HighScores updated = scores;
    if (yipe::update_high_scores(updated, player, player.gatekeeper_state == 3)) {
        yipe::save_high_scores_atomic(path, updated);
        scores = std::move(updated);
    }
}

std::string high_score_page(const yipe::HighScores& scores, int difficulty) {
    constexpr std::array<std::string_view, 3> modes{"Easy", "Medium", "Hard"};
    constexpr std::array<std::string_view, 5> categories{
        "Most Kills", "Most Princesses", "Most Cash at One Time", "Highest Level",
        "First 3 Players to Complete Game",
    };
    const std::size_t group = yipe::high_score_group_offset(difficulty);
    std::ostringstream message;
    message << modes[group / 15] << " Mode\n";
    for (std::size_t category = 0; category < categories.size(); ++category) {
        message << '\n' << categories[category] << '\n';
        for (std::size_t rank = 0; rank < 3; ++rank) {
            const auto& entry = scores[group + category * 3 + rank];
            message << rank + 1 << ". " << entry.name;
            if (category < 4) {
                message << " - " << entry.value;
            }
            message << '\n';
        }
    }
    return message.str();
}

void show_high_scores(SDL_Window* window, const yipe::HighScores& scores, int difficulty = 2) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT
                                     | SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 0, "Back"},
        SDL_MessageBoxButtonData{0, 1, "Easy"},
        SDL_MessageBoxButtonData{0, 2, "Medium"},
        SDL_MessageBoxButtonData{0, 3, "Hard"},
    };
    while (true) {
        const int choice = show_choice(window, "High Scores", high_score_page(scores, difficulty),
                                       buttons.data(), static_cast<int>(buttons.size()));
        if (choice < 1 || choice > 3) {
            return;
        }
        difficulty = choice;
    }
}

std::string special_statistics_page(const yipe::Player& player) {
    std::string_view ability;
    switch (player.special_ability) {
    case yipe::SpecialAbility::none: ability = "None"; break;
    case yipe::SpecialAbility::backstab: ability = "Backstab"; break;
    case yipe::SpecialAbility::frighten: ability = "Frighten"; break;
    case yipe::SpecialAbility::invisibility: ability = "Invisibility"; break;
    case yipe::SpecialAbility::enrage: ability = "Enrage"; break;
    default: throw std::invalid_argument("statistics contain an unknown special ability");
    }

    std::ostringstream message;
    message << "Rare Stamps Collected: " << static_cast<std::int16_t>(player.stamps_found)
            << "\nPrincesses Rescued: " << player.rescued_princess_count() << "\n\nKeys:";
    if (player.has_rainbow_key) {
        message << "\nRainbow Key";
    }
    if (player.has_underworld_key) {
        message << "\nUnderworld Key";
    }
    if (!player.has_rainbow_key && !player.has_underworld_key) {
        message << "\nNone";
    }

    message << "\n\nSpecial Items:";
    if (player.has_bonus_healing_ring) {
        message << "\nRing of Bonus Healing";
    }
    if (player.has_tree_climbing_shoes) {
        message << "\nTree Climbing Tools";
    }
    if (player.has_waterproof_shoe_spray) {
        message << "\nWaterproof Shoe Spray";
    }
    if (player.has_lucky_rabbit_foot) {
        message << "\nKing's Lucky Rabbit Foot";
    }
    if (!player.has_bonus_healing_ring && !player.has_tree_climbing_shoes
        && !player.has_waterproof_shoe_spray && !player.has_lucky_rabbit_foot) {
        message << "\nNone";
    }
    message << "\n\nCurrent Ability: " << ability;
    return message.str();
}

// Dialog 284, initialized by 0x004187c0. The row ratings are display values;
// notably gloves grant WC only at ranks 4/5 here, while combat checks rank > 3.
std::string equipment_statistics_page(const yipe::Player& player) {
    constexpr std::array<std::string_view, 7> helmets{
        "Yipe! Baseball Hat", "Shower Cap", "Sombrero", "Miner's Helmet",
        "Raiders Hard Hat", "Horned Wooden Helmet", "Double Horned Helmet",
    };
    constexpr std::array<std::string_view, 5> gloves{
        "Wet Mittens", "Kitchen Gloves", "Working Gloves", "Leather Gloves",
        "Magical Spiked Gloves",
    };
    constexpr std::array<std::string_view, 5> rings{
        "Cheap Ring", "Aluminum Ring", "Platinum Ring", "Glowing Ring", "Protection Ring",
    };
    constexpr std::array<std::string_view, 6> boots{
        "Sandals", "Leather Boots", "Steel Toe Boots", "Strong Hiking Boots",
        "Combat Boots", "Speedy Boots",
    };
    constexpr std::array<std::string_view, 6> amulets{
        "Plain Necklace", "Gold Medallion", "Protection Chain", "Glowing Amulet",
        "Fire Amulet", "Eye-Piercing Amulet",
    };
    const int helmet = static_cast<std::int16_t>(player.helmet_rank);
    const int glove = static_cast<std::int16_t>(player.gloves_rank);
    const int ring = static_cast<std::int16_t>(player.ring_rank);
    const int boot = static_cast<std::int16_t>(player.boots_rank);
    const int amulet = static_cast<std::int16_t>(player.necklace_rank);
    std::ostringstream message;
    message << "Current Items Equipped\nAC = Armor Class; WC = Weapon Class\n\n"
            << "Weapon: " << equipment_name(yipe::EquipmentKind::weapon, player.weapon_rank)
            << "  (AC 0, WC " << player.weapon_rank << ")\n"
            << "Body Armor: " << equipment_name(yipe::EquipmentKind::armor, player.armor_rank)
            << "  (AC " << player.armor_rank << ", WC 0)";
    const auto append_accessory = [&](std::string_view label, int rank,
                                      const auto& names, int ac, int wc) {
        message << '\n' << label << ": ";
        if (rank < 1) {
            message << "None";
        } else if (static_cast<std::size_t>(rank) <= names.size()) {
            message << names[static_cast<std::size_t>(rank - 1)];
        } else {
            message << "Rank " << rank;
        }
        message << "  (AC " << ac << ", WC " << wc << ')';
    };
    append_accessory("Helmet", helmet, helmets, helmet,
                     helmet == 5 ? 1 : helmet == 6 ? 2 : helmet == 7 ? 4 : 0);
    append_accessory("Gloves", glove, gloves, glove, glove == 4 || glove == 5 ? 1 : 0);
    append_accessory("Ring", ring, rings, ring == 5 ? 6 : ring, 0);
    append_accessory("Boots", boot, boots, boot == 6 ? 7 : boot, 0);
    append_accessory("Amulet", amulet, amulets, amulet, amulet == 5 ? 1 : amulet == 6 ? 2 : 0);
    return message.str();
}

void show_player_status(SDL_Window* window, const yipe::Player& player,
                        const std::filesystem::path& high_scores_path,
                        yipe::HighScores& high_scores) {
    const auto carried = std::count_if(
        player.inventory.begin(),
        player.inventory.begin()
            + static_cast<std::ptrdiff_t>(yipe::Player::carried_inventory_size),
        [](std::uint16_t item) { return item != 0; });
    std::ostringstream message;
    message << player.name << "\nLevel " << player.level << "  XP " << player.experience
            << "  Gold " << player.gold << "\nHP " << player.health << "/"
            << player.maximum_health << "  SP " << player.spell_points << "/"
            << player.maximum_spell_points << "  AP " << player.ability_points << "/"
            << player.maximum_ability_points << "\nStrength " << player.strength
            << "  Dexterity " << player.dexterity << "  Intelligence "
            << player.intelligence << "\nWC " << yipe::player_attack_equipment_rating(player)
            << "  AC " << yipe::player_defense_equipment_rating(player)
            << "\nCarried items " << carried << "/"
            << yipe::Player::carried_inventory_size;
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT
                                     | SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 0, "Back"},
        SDL_MessageBoxButtonData{0, 1, "Kills"},
        SDL_MessageBoxButtonData{0, 2, "High Scores"},
        SDL_MessageBoxButtonData{0, 3, "Special"},
        SDL_MessageBoxButtonData{0, 4, "Equipment"},
    };
    while (true) {
        const int choice = show_choice(window, "Player Status", message.str(), buttons.data(),
                                       static_cast<int>(buttons.size()));
        if (choice == 1) {
            show_monster_defeats(window, player);
        } else if (choice == 2) {
            record_player_high_scores(high_scores_path, high_scores, player);
            show_high_scores(window, high_scores, player.difficulty);
        } else if (choice == 3) {
            show_message(window, "Special Statistics", special_statistics_page(player));
        } else if (choice == 4) {
            show_message(window, "Equipment Statistics", equipment_statistics_page(player));
        } else {
            return;
        }
    }
}

enum class EncounterResult { victory, fled, defeat, quit_requested };

std::string_view stat_increase_name(yipe::StatIncrease stat) {
    switch (stat) {
    case yipe::StatIncrease::strength:
        return "Strength";
    case yipe::StatIncrease::intelligence:
        return "Intelligence";
    case yipe::StatIncrease::dexterity:
        return "Dexterity";
    case yipe::StatIncrease::none:
        return {};
    }
    return {};
}

void append_victory_details(std::ostringstream& message, const yipe::CombatTurn& turn,
                            const yipe::Player& player) {
    message << "\n\nAll monsters are defeated.\nYou gain "
            << turn.reward.experience << " experience and " << turn.reward.gold
            << " gold.";
    if (turn.reward.equipment_upgrade != yipe::EquipmentUpgrade::none) {
        message << "\nYou found a "
                << yipe::equipment_upgrade_name(turn.reward.equipment_upgrade)
                << " upgrade.";
    }
    if (turn.level_up.leveled_up) {
        message << "\nYou reached level " << player.level << " and gained "
                << turn.level_up.health_increase << " HP and "
                << turn.level_up.spell_point_increase << " SP.";
        const std::string_view stat = stat_increase_name(turn.level_up.stat_increase);
        if (!stat.empty()) {
            message << "\n" << stat << " increased.";
        }
    }
}

std::array<int, yipe::CombatEncounter::maximum_opponents> monster_healths(
    const yipe::CombatEncounter& encounter) {
    std::array<int, yipe::CombatEncounter::maximum_opponents> health{};
    for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
        health[slot] = encounter.monsters[slot].health;
    }
    return health;
}

void play_defeated_monsters(
    AudioPlayer& audio, const yipe::GameSettings& settings,
    const std::array<int, yipe::CombatEncounter::maximum_opponents>& previous_health,
    const yipe::CombatEncounter& encounter) {
    for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
        if (previous_health[slot] > 0 && encounter.monsters[slot].health < 1) {
            audio.play_monster_death(
                encounter.monsters[slot].type_id, settings.sound_enabled);
        }
    }
}

void play_combat_turn_sounds(
    AudioPlayer& audio, const yipe::GameSettings& settings,
    const std::array<int, yipe::CombatEncounter::maximum_opponents>& previous_health,
    const yipe::CombatEncounter& encounter, const yipe::CombatTurn& turn,
    bool player_attack_sound) {
    if (player_attack_sound && turn.player_damage > 0) {
        audio.play(130, settings.sound_enabled);
    }
    play_defeated_monsters(audio, settings, previous_health, encounter);
    if (turn.monster_damage > 0) {
        audio.play(134, settings.sound_enabled);
    }
    if (turn.outcome == yipe::CombatOutcome::defeat) {
        audio.play(128, settings.sound_enabled);
    }
}

EncounterResult run_monster_encounter(
    SDL_Window* window, SDL_Renderer* renderer,
    const yipe::MonsterPlacement& placement,
    yipe::Player& player, const yipe::WorldLocation& location,
    yipe::MsvcRandom& random, int map_environment,
    AudioPlayer& audio, const yipe::GameSettings& settings) {
    yipe::CombatEncounter encounter = yipe::start_monster_encounter(
        placement, player, random, map_environment);

    while (true) {
        std::ostringstream status;
        status << "Monsters block your path.\nYour HP: " << player.health << "/"
               << player.maximum_health << "  AP: " << player.ability_points << "/"
               << player.maximum_ability_points;
        for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
            const yipe::Monster& monster = encounter.monsters[slot];
            if (monster.type_id == 0) {
                continue;
            }
            status << "\n" << slot + 1 << ". " << monster.name << " HP: "
                   << std::max(monster.health, 0) << "/" << monster.maximum_health;
            if (static_cast<int>(slot) == encounter.selected_target
                && monster.health > 0) {
                status << " [target]";
            }
        }
        const int action = choose_combat_action(renderer, status.str(), settings);
        if (action == 0) {
            return EncounterResult::quit_requested;
        }
        if (action == 1) {
            const auto previous_health = monster_healths(encounter);
            const yipe::CombatTurn turn =
                yipe::resolve_attack_turn(player, encounter, random);
            play_combat_turn_sounds(
                audio, settings, previous_health, encounter, turn, true);
            std::ostringstream result;
            result << "You deal " << turn.player_damage << " damage.";
            if (turn.outcome == yipe::CombatOutcome::victory) {
                append_victory_details(result, turn, player);
                show_message(window, "Victory", result.str());
                return EncounterResult::victory;
            }
            result << "\nThe monsters deal " << turn.monster_damage << " damage.";
            show_message(window, turn.outcome == yipe::CombatOutcome::defeat ? "Defeat"
                                                                             : "Combat",
                         result.str());
            if (turn.outcome == yipe::CombatOutcome::defeat) {
                return EncounterResult::defeat;
            }
        } else if (action == 2) {
            const std::optional<std::size_t> slot = choose_inventory_item(window, player);
            if (!slot) {
                continue;
            }
            const yipe::ItemUseResult used =
                player.use_inventory_item(
                    *slot, random, true, true, location.map_id,
                    location.alternate_map_set);
            if (!used.consumed) {
                show_message(window, "Inventory", used.message);
                continue;
            }
            audio.play(used.sound_id, settings.sound_enabled);
            const yipe::CombatTurn response =
                yipe::resolve_monster_turn(player, encounter, random);
            if (response.monster_damage > 0) {
                audio.play(134, settings.sound_enabled);
            }
            if (response.outcome == yipe::CombatOutcome::defeat) {
                audio.play(128, settings.sound_enabled);
            }
            std::ostringstream result;
            result << used.message;
            if (used.amount != 0) {
                result << " (" << used.amount << ")";
            }
            result << ".\nThe monsters deal " << response.monster_damage << " damage.";
            show_message(window, response.outcome == yipe::CombatOutcome::defeat ? "Defeat"
                                                                                 : "Combat",
                         result.str());
            if (response.outcome == yipe::CombatOutcome::defeat) {
                return EncounterResult::defeat;
            }
        } else if (action == 3) {
            const std::optional<SpellSelection> selection =
                choose_spell(window, player, true);
            if (!selection) {
                continue;
            }
            const auto previous_health = monster_healths(encounter);
            const yipe::CombatSpellResult spell = yipe::resolve_spell_turn(
                player, encounter, selection->index, selection->power, random);
            if (spell.status != yipe::SpellCastStatus::cast) {
                show_message(window, "Spells", spell_failure_message(spell.status));
                continue;
            }
            audio.play(yipe::spell_sound_id(selection->index),
                       settings.sound_enabled);
            play_combat_turn_sounds(
                audio, settings, previous_health, encounter, spell.turn, false);

            std::ostringstream result;
            result << "You cast " << yipe::spell_definitions()[selection->index].name
                   << ".";
            if (spell.turn.player_damage > 0) {
                result << "\nThe spell deals " << spell.turn.player_damage << " damage.";
            }
            if (spell.healing > 0) {
                result << "\nYou recover " << spell.healing << " health.";
            }
            if (selection->index == 10) {
                result << "\nThe monsters' health shifts.";
            } else if (selection->index == 11) {
                result << "\nOne monster becomes a rabbit.";
            }
            if (spell.turn.outcome == yipe::CombatOutcome::victory) {
                append_victory_details(result, spell.turn, player);
                show_message(window, "Victory", result.str());
                return EncounterResult::victory;
            }
            result << "\nThe monsters deal " << spell.turn.monster_damage
                   << " damage.";
            show_message(window,
                         spell.turn.outcome == yipe::CombatOutcome::defeat
                             ? "Defeat" : "Combat",
                         result.str());
            if (spell.turn.outcome == yipe::CombatOutcome::defeat) {
                return EncounterResult::defeat;
            }
        } else if (action == 4) {
            const yipe::FleeResult result = yipe::try_flee(player, encounter, random);
            if (result.escaped) {
                show_message(window, "Combat", "You escaped from the monsters.");
                return EncounterResult::fled;
            }
            if (result.monster_damage > 0) {
                audio.play(134, settings.sound_enabled);
            }
            if (result.player_defeated) {
                audio.play(128, settings.sound_enabled);
            }
            show_message(window, result.player_defeated ? "Defeat" : "Combat",
                         "You fail to escape. The monsters deal "
                             + std::to_string(result.monster_damage) + " damage.");
            if (result.player_defeated) {
                return EncounterResult::defeat;
            }
        } else if (action == 5) {
            for (std::size_t offset = 1; offset <= encounter.monsters.size(); ++offset) {
                const std::size_t slot =
                    (static_cast<std::size_t>(encounter.selected_target) + offset)
                    % encounter.monsters.size();
                if (encounter.monsters[slot].health > 0) {
                    encounter.selected_target = static_cast<int>(slot);
                    break;
                }
            }
        } else if (action == 6) {
            if (player.special_ability == yipe::SpecialAbility::none) {
                show_message(window, "Combat", "You have not learned a special ability.");
                continue;
            }
            if (player.ability_points < 3) {
                show_message(window, "Combat",
                             "You need three ability points to use your special ability.");
                continue;
            }
            const int previous_ability_points = player.ability_points;
            const auto previous_health = monster_healths(encounter);
            const yipe::CombatTurn turn =
                yipe::resolve_special_ability_turn(player, encounter, random);
            if (player.ability_points == previous_ability_points) {
                show_message(window, "Combat",
                             "Your special ability cannot take effect right now.");
                continue;
            }
            audio.play(yipe::ability_sound_id(player.special_ability),
                       settings.sound_enabled);
            play_combat_turn_sounds(
                audio, settings, previous_health, encounter, turn, false);

            std::ostringstream result;
            result << "You use your special ability.";
            if (turn.player_damage > 0) {
                result << "\nIt deals " << turn.player_damage << " damage.";
            }
            if (player.special_ability_duration > 0) {
                result << "\nIts effect will last for "
                       << player.special_ability_duration << " combat actions.";
            }
            if (turn.outcome == yipe::CombatOutcome::victory) {
                append_victory_details(result, turn, player);
                show_message(window, "Victory", result.str());
                return EncounterResult::victory;
            }
            show_message(window, "Combat", result.str());
        }
    }
}

std::optional<yipe::MonsterPlacement> monster_encounter_at(
    const yipe::GameState& game) {
    for (const yipe::MonsterPlacement& placement :
         active_monster_placements(game)) {
        if (yipe::monster_placement_contains(
                placement, game.location.position)) {
            return placement;
        }
    }
    return std::nullopt;
}

yipe::MonsterPlacement transient_monster_placement(
    int type_id, yipe::MapPosition position) {
    yipe::MonsterPlacement placement;
    placement.type_id = type_id;
    placement.position = position;
    placement.encounter_size = 0;
    return placement;
}

void apply_map_tile_changes(yipe::Map& map, const yipe::GameState& game) {
    for (const yipe::MapTileChange& change : game.map_tile_changes) {
        if (change.map_id == map.id()) {
            map.restore_tile(change.position, change.tile);
        }
    }
}

std::optional<yipe::Map> load_world_map(const yipe::WorldLocation& location,
                                        yipe::MsvcRandom& random) {
    if (location.map_id >= 0x76 && location.map_id <= 0x78) {
        return yipe::generate_original_map(location.map_id, location.position, random);
    }
    return yipe::original_map(location.map_id);
}

std::string_view location_message(int value) {
    if (value < 1 || value > static_cast<int>(location_messages.size())) {
        throw std::logic_error("map event has an invalid location-message index");
    }
    return location_messages[static_cast<std::size_t>(value - 1)];
}

std::string gatekeeper_offer_message(const yipe::Player& player) {
    constexpr std::string_view ordinary_offer =
        "  I am the Gatekeeper.  You have been summoned here because your health "
        "has run out.  From here, only I can send you back to your world.  For a "
        "small fee of xx gold, I will send you back fully healed.";
    constexpr std::string_view secret_offer =
        "  I am the Gatekeeper.  I see that you have found a secret entrance to "
        "find me!  From here, only I can send you back to your world.  For a small "
        "fee of xx gold, I will send you back fully healed.";
    std::string message(player.entered_hell_through_secret_passage
                            ? secret_offer : ordinary_offer);
    message.replace(message.find("xx"), 2,
                    std::to_string(yipe::gatekeeper_recovery_fee(player)));
    message += "\n\n   Or perhaps you are feeling strong enough to challenge me to a "
               "battle.  If you defeat me, I shall forever leave and give you a "
               "free passage back to town.";
    return message;
}

void synchronize_player_location(yipe::GameState& game) {
    game.player.current_map_id = game.location.map_id;
    game.player.alternate_map_set = game.location.alternate_map_set;
}

void initialize_map_state(yipe::GameState& game, yipe::Map& map) {
    yipe::initialize_map_actions(game.player, map, game.location.position);
    game.roaming_monster.reset();
    game.pursuing_monster =
        yipe::pursuing_monster_for_map(map, game.player.level);
}

void recover_from_combat_defeat(SDL_Window* window, yipe::GameState& game,
                                yipe::Map& map, yipe::MsvcRandom& random,
                                int monster_type) {
    const yipe::CombatDefeatResult defeat = yipe::resolve_combat_defeat(
        game.player, game.location, monster_type, random);
    show_message(window, "Defeat", std::string(defeat.message));
    const std::optional<yipe::Map> hell = load_world_map(game.location, random);
    if (!hell) {
        throw std::runtime_error("Yipe Hell map is unavailable");
    }
    map = *hell;
    game.temporarily_defeated_monster_placements.clear();
    initialize_map_state(game, map);
    apply_map_tile_changes(map, game);
    yipe::initialize_map_visibility(
        game.map_visibility, map, game.location.position);
}

bool apply_pending_relocation(yipe::GameState& game, yipe::Map& map,
                              yipe::MsvcRandom& random) {
    const yipe::PendingRelocation relocation =
        yipe::apply_pending_relocation(game.player, game.location);
    if (relocation == yipe::PendingRelocation::none) {
        return false;
    }
    const std::optional<yipe::Map> destination =
        load_world_map(game.location, random);
    if (!destination) {
        throw std::runtime_error("relocation destination map is unavailable");
    }
    map = *destination;
    if (relocation == yipe::PendingRelocation::surface) {
        if (const std::optional<yipe::MapPosition> position =
                yipe::surface_return_position(map)) {
            game.location.position = *position;
        }
    }
    game.temporarily_defeated_monster_placements.clear();
    initialize_map_state(game, map);
    apply_map_tile_changes(map, game);
    yipe::initialize_map_visibility(
        game.map_visibility, map, game.location.position);
    return true;
}

void cast_exploration_spell(SDL_Window* window, yipe::GameState& game,
                            yipe::Map& map, yipe::MsvcRandom& random,
                            AudioPlayer& audio, const yipe::GameSettings& settings) {
    const std::optional<SpellSelection> selection =
        choose_spell(window, game.player, false);
    if (!selection) {
        return;
    }
    const yipe::ExplorationSpellResult result = yipe::cast_exploration_spell(
        game.player, game.location, selection->index, selection->power, random);
    if (result.status != yipe::SpellCastStatus::cast) {
        show_message(window, "Spells", spell_failure_message(result.status));
        return;
    }
    audio.play(yipe::spell_sound_id(selection->index), settings.sound_enabled);
    if (result.location_changed) {
        static_cast<void>(apply_pending_relocation(game, map, random));
    }

    std::ostringstream message;
    message << "You cast " << yipe::spell_definitions()[selection->index].name << ".";
    if (result.healing > 0) {
        message << "\nYou recover " << result.healing << " health.";
    } else if (selection->index == 4) {
        message << "\nThe smell will repel monsters for "
                << result.repellent_duration << " steps.";
    } else if (result.location_changed) {
        message << "\nYou arrive safely on the surface.";
    } else {
        message << "\nThe spell takes effect.";
    }
    message << "\nSpell points: " << game.player.spell_points << "/"
            << game.player.maximum_spell_points;
    show_message(window, "Spells", message.str());
}

void use_exploration_item(SDL_Window* window, yipe::GameState& game,
                          yipe::Map& map,
                          yipe::MsvcRandom& random, AudioPlayer& audio,
                          const yipe::GameSettings& settings) {
    const std::optional<std::size_t> slot =
        choose_inventory_item(window, game.player);
    if (!slot) {
        return;
    }
    const yipe::ItemUseResult result =
        game.player.use_inventory_item(
            *slot, random, true, false, game.location.map_id,
            game.location.alternate_map_set);
    if (result.consumed) {
        audio.play(result.sound_id, settings.sound_enabled);
    }
    std::string message = result.message;
    if (result.amount != 0) {
        message += " (" + std::to_string(result.amount) + ")";
    }
    show_message(window, "Inventory", message);
    if (apply_pending_relocation(game, map, random)) {
        audio.play(201, settings.sound_enabled);
    }
}

void remember_map_tile(yipe::GameState& game, const yipe::Map& map,
                       yipe::MapPosition position) {
    const auto existing = std::find_if(
        game.map_tile_changes.begin(), game.map_tile_changes.end(),
        [&map, position](const yipe::MapTileChange& change) {
            return change.map_id == map.id() && change.position == position;
        });
    if (existing != game.map_tile_changes.end()) {
        existing->tile = map.tile(position);
    } else {
        game.map_tile_changes.push_back({map.id(), position, map.tile(position)});
    }
}

yipe::OtherActionResult use_other_action(yipe::GameState& game, yipe::Map& map,
                                         yipe::MsvcRandom& random) {
    const yipe::OtherActionResult result = yipe::perform_other_action(
        game.player, map, game.location.position, random);
    if (!apply_pending_relocation(game, map, random)) {
        yipe::reveal_map_from(game.map_visibility, map, game.location.position);
    }
    return result;
}

bool save_current_game(SDL_Window* window, std::filesystem::path& path,
                       yipe::GameState& game, const yipe::MsvcRandom& random,
                       bool show_confirmation,
                       const std::filesystem::path& excluded_path = {}) {
    std::filesystem::path destination = path;
    if (destination.empty()) {
        const auto selected = yipe_app::choose_save_game_file(
            default_preference_directory() / "save.y3r");
        if (!selected) {
            return false;
        }
        destination = *selected;
    }
    std::string extension = destination.extension().string();
    std::transform(extension.begin(), extension.end(), extension.begin(),
                   [](unsigned char character) {
                       return static_cast<char>(std::tolower(character));
                   });
    if (extension != ".y3r") {
        show_message(window, "Save Game Failed",
                     "Choose a .y3r file. Original .yp3 saves are import-only.");
        return false;
    }
    std::error_code error;
    if (!excluded_path.empty()
        && (destination.lexically_normal() == excluded_path.lexically_normal()
            || std::filesystem::equivalent(destination, excluded_path, error))) {
        show_message(window, "Choose Another Save File",
                     "That file is the game you are opening. Save the current game elsewhere.");
        return false;
    }
    game.random_state = random.state();
    synchronize_player_location(game);
    yipe::save_game_atomic(destination, game);
    path = std::move(destination);
    if (show_confirmation) {
        show_message(window, "Game Saved", "Your game was saved to:\n" + path.string());
    }
    return true;
}

std::optional<GameExit> choose_game_to_open(
    const std::filesystem::path& initial_directory) {
    const auto selected = yipe_app::choose_open_game_file(initial_directory);
    if (!selected) {
        return std::nullopt;
    }
    std::string extension = selected->extension().string();
    std::transform(extension.begin(), extension.end(), extension.begin(),
                   [](unsigned char character) {
                       return static_cast<char>(std::tolower(character));
                   });
    const bool legacy = extension == ".yp3";
    yipe::GameState game = legacy
        ? yipe::load_legacy_save(*selected).game : yipe::load_game(*selected);
    yipe::MsvcRandom check_random(game.random_state);
    const auto check_map = load_world_map(game.location, check_random);
    if (!check_map || !check_map->contains(game.location.position)) {
        throw std::runtime_error("save contains an unsupported world location");
    }
    std::filesystem::path native_path = *selected;
    if (legacy) {
        const auto suggested = initial_directory / (selected->stem().string() + ".y3r");
        const auto converted = yipe_app::choose_save_game_file(suggested);
        if (!converted) {
            return std::nullopt;
        }
        native_path = *converted;
    }
    return GameExit{StartupChoice::load_game, std::move(game), std::move(native_path)};
}

void save_current_game_as(SDL_Window* window, std::filesystem::path& path,
                          yipe::GameState& game, const yipe::MsvcRandom& random) {
    const auto selected = yipe_app::choose_save_game_file(
        path.empty() ? default_preference_directory() / "save.y3r" : path);
    if (!selected) {
        return;
    }
    std::filesystem::path new_path = *selected;
    if (save_current_game(window, new_path, game, random, true)) {
        path = std::move(new_path);
    }
}

bool confirm_leave_game(SDL_Window* window, const char* title, const char* message,
                        std::filesystem::path& save_path,
                        yipe::GameState& game, const yipe::MsvcRandom& random,
                        const std::filesystem::path& excluded_path = {}) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Cancel"},
        SDL_MessageBoxButtonData{0, 1, "Leave Without Saving"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 2,
                                 "Save and Leave"},
    };
    const int choice = show_choice(window, title, message,
                                   buttons.data(), static_cast<int>(buttons.size()));
    if (choice == 2) {
        return save_current_game(window, save_path, game, random, false, excluded_path);
    }
    return choice == 1;
}

GameExit run_game(SDL_Window* window, SDL_Renderer* renderer,
                  const GameTextures& textures, yipe::GameState game,
                  std::filesystem::path& save_path,
                  const std::filesystem::path& settings_path,
                  const std::filesystem::path& high_scores_path,
                  yipe::HighScores& high_scores, yipe::GameSettings& settings,
                  AudioPlayer& audio) {
    prepare_game_view(window, renderer);
    yipe::MsvcRandom random(game.random_state);
    const std::optional<yipe::Map> restored_map = load_world_map(game.location, random);
    if (!restored_map || !restored_map->contains(game.location.position)) {
        throw std::runtime_error("save contains an unsupported world location");
    }
    yipe::Map map = *restored_map;
    if (!apply_pending_relocation(game, map, random)) {
        synchronize_player_location(game);
        initialize_map_state(game, map);
        apply_map_tile_changes(map, game);
        yipe::initialize_map_visibility(
            game.map_visibility, map, game.location.position);
    }
    if (!map.contains(game.location.position)) {
        throw std::runtime_error("save contains an invalid relocation destination");
    }
    const bool completed_saved_finale = map.id() == 0x11
        && yipe::complete_gatekeeper_finale(game.player);
    if (game.player.gatekeeper_state == 3) {
        record_player_high_scores(high_scores_path, high_scores, game.player);
    }
    render_game(renderer, map, textures, game);
    if (completed_saved_finale) {
        audio.play(129, settings.sound_enabled);
        show_message(window, "Victory",
                     "You've rescued all the princesses!!\n\n"
                     "You are the hero of the entire world!!");
    }

    const auto fight_monster = [&](const yipe::MonsterPlacement& placement) {
        const int previous_level = game.player.level;
        const EncounterResult result = run_monster_encounter(
            window, renderer, placement, game.player, game.location, random,
            map.environment(), audio, settings);
        if (result == EncounterResult::victory) {
            map.relocate_overlay_after_combat(game.location.position);
        }
        if (result == EncounterResult::victory && game.player.level > previous_level) {
            record_player_high_scores(high_scores_path, high_scores, game.player);
        }
        return result;
    };

    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            throw std::runtime_error(std::string("SDL event wait failed: ") + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            if (save_current_game(window, save_path, game, random, false)) {
                return {StartupChoice::quit};
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.type == SDL_WINDOWEVENT
            && (event.window.event == SDL_WINDOWEVENT_EXPOSED
                || event.window.event == SDL_WINDOWEVENT_FOCUS_GAINED)) {
            render_game(renderer, map, textures, game);
        }
        if (event.type != SDL_KEYDOWN) {
            continue;
        }
        if (event.key.keysym.sym == SDLK_ESCAPE || event.key.keysym.sym == SDLK_q) {
            if (confirm_leave_game(window, "Quit Game",
                                   "Do you want to save before quitting?",
                                   save_path, game, random)) {
                return {StartupChoice::quit};
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if ((event.key.keysym.mod & KMOD_CTRL) != 0
            && (event.key.keysym.sym == SDLK_n
                || (event.key.keysym.sym == SDLK_o
                    && (event.key.keysym.mod & KMOD_SHIFT) == 0))) {
            if (event.key.repeat == 0) {
                const bool opening = event.key.keysym.sym == SDLK_o;
                if (opening) {
                    try {
                        auto opened = choose_game_to_open(
                            default_preference_directory());
                        if (opened) {
                            std::error_code error;
                            const bool same_path = !save_path.empty()
                                && std::filesystem::equivalent(
                                    save_path, opened->opened_save_path, error);
                            if (same_path) {
                                constexpr std::array buttons{
                                    SDL_MessageBoxButtonData{
                                        SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Cancel"},
                                        SDL_MessageBoxButtonData{
                                            SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1,
                                            "Open Selected Game"},
                                };
                                if (show_choice(
                                        window, "Reopen Game",
                                        "Discard unsaved changes and reopen this save?",
                                        buttons.data(), static_cast<int>(buttons.size())) == 1) {
                                    return std::move(*opened);
                                }
                            } else if (confirm_leave_game(
                                           window, "Open Game",
                                           "Open the selected game? Save current progress first?",
                                           save_path, game, random,
                                           opened->opened_save_path)) {
                                return std::move(*opened);
                            }
                        }
                    } catch (const std::exception& error) {
                        show_message(window, "Open Game Failed", error.what());
                    }
                } else if (confirm_leave_game(
                               window, "New Game",
                               "Start a new game? Save current progress first?",
                               save_path, game, random)) {
                    return {StartupChoice::new_game};
                }
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.key.keysym.sym == SDLK_F1) {
            if (event.key.repeat == 0) {
                show_help(window, settings);
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.key.keysym.sym == SDLK_F5
            || (event.key.keysym.sym == SDLK_s
                && (event.key.keysym.mod & (KMOD_CTRL | KMOD_SHIFT)) == KMOD_CTRL)) {
            if (event.key.repeat == 0) {
                static_cast<void>(save_current_game(
                    window, save_path, game, random, true));
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.key.keysym.sym == SDLK_s
            && (event.key.keysym.mod & (KMOD_CTRL | KMOD_SHIFT))
                == (KMOD_CTRL | KMOD_SHIFT)) {
            if (event.key.repeat == 0) {
                save_current_game_as(window, save_path, game, random);
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.key.keysym.sym == SDLK_F10) {
            show_settings(window, settings, settings_path, audio);
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.key.keysym.sym == SDLK_t
            && (event.key.keysym.mod & KMOD_CTRL) != 0) {
            if (event.key.repeat == 0) {
                show_player_status(window, game.player, high_scores_path, high_scores);
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if ((event.key.keysym.mod & KMOD_CTRL) != 0
            && (event.key.keysym.sym == SDLK_e || event.key.keysym.sym == SDLK_i
                || event.key.keysym.sym == SDLK_c)) {
            if (event.key.repeat == 0) {
                if (event.key.keysym.sym == SDLK_e) {
                    show_message(window, "Equipment Statistics",
                                 equipment_statistics_page(game.player));
                } else if (event.key.keysym.sym == SDLK_i) {
                    use_exploration_item(window, game, map, random, audio, settings);
                } else {
                    cast_exploration_spell(window, game, map, random, audio, settings);
                }
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.key.keysym.sym == SDLK_h
            && (event.key.keysym.mod & KMOD_CTRL) != 0) {
            if (event.key.repeat == 0) {
                record_player_high_scores(high_scores_path, high_scores, game.player);
                show_high_scores(window, high_scores, game.player.difficulty);
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.key.keysym.sym == SDLK_r
            && (event.key.keysym.mod & KMOD_CTRL) != 0) {
            if (event.key.repeat == 0) {
                const yipe::MapEvent& sign = map.events().message;
                if (sign.has_position() && sign.position == game.location.position) {
                    show_message(window, "Location",
                                 std::string(location_message(sign.value)));
                }
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (event.key.keysym.sym == SDLK_o
            && (event.key.keysym.mod & (KMOD_CTRL | KMOD_SHIFT))
                == (KMOD_CTRL | KMOD_SHIFT)) {
            if (event.key.repeat == 0) {
                const yipe::OtherActionResult result = use_other_action(game, map, random);
                audio.play(result.sound_id, settings.sound_enabled);
                if (!result.message.empty()) {
                    show_message(window, "Other", std::string(result.message));
                }
                render_game(renderer, map, textures, game);
            }
            continue;
        }

        const std::optional<std::uint16_t> key =
            portable_key_code(event.key.keysym.sym);
        int delta_x = 0;
        int delta_y = 0;
        if (key && *key == binding(settings, yipe::InputAction::northwest)) {
            delta_x = -1;
            delta_y = -1;
        } else if (key && *key == binding(settings, yipe::InputAction::north)) {
            delta_y = -1;
        } else if (key && *key == binding(settings, yipe::InputAction::northeast)) {
            delta_x = 1;
            delta_y = -1;
        } else if (key && *key == binding(settings, yipe::InputAction::west)) {
            delta_x = -1;
        } else if (key && *key == binding(settings, yipe::InputAction::east)) {
            delta_x = 1;
        } else if (key && *key == binding(settings, yipe::InputAction::southwest)) {
            delta_x = -1;
            delta_y = 1;
        } else if (key && *key == binding(settings, yipe::InputAction::south)) {
            delta_y = 1;
        } else if (key && *key == binding(settings, yipe::InputAction::southeast)) {
            delta_x = 1;
            delta_y = 1;
        } else {
            switch (event.key.keysym.sym) {
            case SDLK_a:
                delta_x = -1;
                break;
            case SDLK_d:
                delta_x = 1;
                break;
            case SDLK_w:
                delta_y = -1;
                break;
            case SDLK_s:
                delta_y = 1;
                break;
            default:
                break;
            }
        }
        if (delta_x == 0 && delta_y == 0) {
            if (key && *key == binding(settings, yipe::InputAction::use_item)) {
                use_exploration_item(window, game, map, random, audio, settings);
                render_game(renderer, map, textures, game);
            } else if (key && *key == binding(settings, yipe::InputAction::cast)) {
                cast_exploration_spell(
                    window, game, map, random, audio, settings);
                render_game(renderer, map, textures, game);
            }
            continue;
        }
        const yipe::WorldLocation previous_location = game.location;
        const yipe::MovementState movement_state{
            game.player.has_waterproof_shoe_spray,
            game.player.has_rainbow_key,
            game.player.has_underworld_key,
            game.player.has_tree_climbing_shoes,
        };
        const yipe::MovementResult result =
            yipe::try_move(map, game.location.position, delta_x, delta_y, movement_state);
        bool suppress_roaming_update = false;
        bool encounter_resolved = false;
        if (result.position_changed) {
            if (result.original_code == 0x12) {
                map.open_door(previous_location.position, game.location.position);
                remember_map_tile(game, map, game.location.position);
                audio.play(131, settings.sound_enabled);
            } else if (result.original_code == 0x1a) {
                if (map.toggle_linked_tiles()) {
                    remember_map_tile(game, map, map.events().linked[0].position);
                    if (map.events().linked[1].has_position()) {
                        remember_map_tile(game, map, map.events().linked[1].position);
                    }
                }
            }
            bool changed_map = false;
            bool completed_finale = false;
            if (result.original_code == 2) {
                changed_map = yipe::cross_map_edge(game.location);
            } else if (result.original_code == 0x0e || result.original_code == 0x0f
                       || result.original_code == 0x10) {
                changed_map = yipe::change_map_depth(game.location, result.original_code);
            }
            if (changed_map) {
                suppress_roaming_update = true;
                const std::optional<yipe::Map> next_map =
                    load_world_map(game.location, random);
                if (next_map) {
                    map = *next_map;
                    if (result.original_code == 0x0e) {
                        game.player.surface_return_map_id =
                            static_cast<std::int16_t>(previous_location.map_id);
                        game.player.surface_return_region_x =
                            static_cast<std::int16_t>(previous_location.region_x);
                        game.player.surface_return_region_y =
                            static_cast<std::int16_t>(previous_location.region_y);
                    }
                    game.temporarily_defeated_monster_placements.clear();
                    initialize_map_state(game, map);
                    synchronize_player_location(game);
                    apply_map_tile_changes(map, game);
                    yipe::initialize_map_visibility(
                        game.map_visibility, map, game.location.position);
                    completed_finale = map.id() == 0x11
                        && yipe::complete_gatekeeper_finale(game.player);
                } else {
                    game.location = previous_location;
                }
            }
            yipe::reveal_map_from(
                game.map_visibility, map, game.location.position);
            render_game(renderer, map, textures, game);
            if (completed_finale) {
                record_player_high_scores(high_scores_path, high_scores, game.player);
                audio.play(129, settings.sound_enabled);
                show_message(window, "Victory",
                             "You've rescued all the princesses!!\n\n"
                             "You are the hero of the entire world!!");
            }
            if (result.original_code == 3) {
                const std::uint16_t tile = map.tile(game.location.position);
                if (tile == 0x26) {
                    show_message(window, "Water Hole",
                                 "You are standing on a water hole. Press Ctrl+Shift+O (Other) to drink.");
                } else if (tile == 0x42) {
                    show_message(
                        window, "Underworld Tunnel",
                        "You find a special tunnel leading down into the depths of the "
                        "underworld. Press Ctrl+Shift+O (Other) to travel to Yipe Hell.");
                } else if (tile == 0x56 || tile == 0x57) {
                    show_message(window, "Tree",
                                 "You climb over the tree using your special shoes.");
                    if (game.location.map_id == 1
                        && !game.player.has_bonus_healing_ring) {
                        show_secret_tree(window, game.player);
                    }
                } else {
                    std::string hint(yipe::map_movement_hint(game.player, map.id()));
                    if (!hint.empty()) {
                        if (map.id() == 3 || map.id() == 0x12 || map.id() == 0x45) {
                            hint += "\n\nPress Ctrl+Shift+O (Other) to investigate.";
                        }
                        show_message(window, "Location", hint);
                    }
                }
            }
            const yipe::MapEvent& message = map.events().message;
            if (message.has_position()
                && message.position == game.location.position) {
                show_message(window, "Location",
                             std::string(location_message(message.value)));
            }
            const yipe::PrincessEvent& princess = map.events().princess;
            if (princess.has_position()
                && princess.position == game.location.position
                && game.player.inventory_values[princess.rescue_index] == 0) {
                show_princess(window, game.player, princess, random);
                render_game(renderer, map, textures, game);
            }
            if (const std::optional<yipe::MonsterPlacement> placement =
                    monster_encounter_at(game)) {
                encounter_resolved = true;
                const EncounterResult encounter = fight_monster(*placement);
                if (encounter == EncounterResult::quit_requested) {
                    if (save_current_game(window, save_path, game, random, false)) {
                        return {StartupChoice::quit};
                    }
                    render_game(renderer, map, textures, game);
                    continue;
                }
                if (encounter == EncounterResult::victory) {
                    auto& defeated = placement->respawns_after_map_reload
                        ? game.temporarily_defeated_monster_placements
                        : game.defeated_monster_placements;
                    defeated.push_back(placement->placement_index);
                } else if (encounter == EncounterResult::fled) {
                    game.location = previous_location;
                    synchronize_player_location(game);
                    if (map.id() != game.location.map_id) {
                        const std::optional<yipe::Map> previous_map =
                            load_world_map(game.location, random);
                        if (!previous_map) {
                            throw std::runtime_error(
                                "could not restore the map after fleeing");
                        }
                        map = *previous_map;
                        initialize_map_state(game, map);
                        apply_map_tile_changes(map, game);
                        yipe::initialize_map_visibility(
                            game.map_visibility, map, game.location.position);
                    }
                } else {
                    recover_from_combat_defeat(
                        window, game, map, random, placement->type_id);
                }
                render_game(renderer, map, textures, game);
            }
        } else {
            switch (result.original_code) {
            case 5:
                show_equipment_shop(window, game.player, game.location.map_id,
                                    yipe::EquipmentKind::weapon);
                break;
            case 6:
                show_equipment_shop(window, game.player, game.location.map_id,
                                    yipe::EquipmentKind::armor);
                break;
            case 7:
                show_healing_shop(window, game.player, random);
                break;
            case 8:
                if (game.location.map_id == 0x11) {
                    show_freds_shop(window, game.player);
                } else {
                    show_message(window, "Miscellaneous Shop",
                                 "This entrance leads to a miscellaneous shop.");
                }
                break;
            case 9:
                show_magic_shop(window, game.player, random);
                break;
            case 0x0c:
                if (yipe::use_restoration_tile(game.player)) {
                    audio.play(206, settings.sound_enabled);
                    show_message(window, "Restored",
                                 "Your health, spell points, and ability points are fully "
                                 "restored.");
                }
                break;
            case 0x0d:
                show_casino(window, game.player, random, audio, settings);
                break;
            case 0x11:
                show_ability_training(window, game.player, map.environment());
                break;
            case 0x13:
                show_message(window, "Locked Door",
                             "You need a rainbow key to open this door.");
                break;
            case 0x14:
                show_message(window, "Locked Door",
                             "You need an underworld key to open this door.");
                break;
            case 0x15:
            {
                audio.play(140, settings.sound_enabled);
                const std::time_t now = std::time(nullptr);
                const std::tm* local_time = std::localtime(&now);
                const int hour = local_time == nullptr ? 12 : local_time->tm_hour;
                const yipe::KingConversation conversation =
                    yipe::talk_to_king(game.player, random, hour);
                show_message(window, "The King",
                             conversation.greeting + "\n\n"
                                 + std::string(conversation.message) + "\n\n"
                                 + std::string(conversation.footer));
                break;
            }
            case 0x16: {
                if (game.location.map_id == 0x75
                    && game.player.hell_gatekeeper_state
                        == yipe::HellGatekeeperState::waiting) {
                    constexpr std::array buttons{
                        SDL_MessageBoxButtonData{
                            SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Cancel"},
                        SDL_MessageBoxButtonData{
                            SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Pay"},
                        SDL_MessageBoxButtonData{0, 2, "Fight"},
                    };
                    const int choice = show_choice(
                        window, "Gatekeeper", gatekeeper_offer_message(game.player),
                        buttons.data(), static_cast<int>(buttons.size()));
                    if (choice == 1) {
                        static_cast<void>(
                            yipe::pay_gatekeeper_recovery(game.player));
                        show_message(
                            window, "Gatekeeper",
                            "After paying the Gatekeeper the small fee, he somehow "
                            "vanishes and leaves a path back to Yipe! before you.");
                    } else if (choice == 2) {
                        yipe::MonsterPlacement gatekeeper =
                            transient_monster_placement(
                                41, hell_gatekeeper_position);
                        gatekeeper.encounter_size = 3;
                        const EncounterResult encounter = fight_monster(gatekeeper);
                        if (encounter == EncounterResult::quit_requested) {
                            if (save_current_game(
                                    window, save_path, game, random, false)) {
                                return {StartupChoice::quit};
                            }
                            render_game(renderer, map, textures, game);
                            continue;
                        }
                        if (encounter == EncounterResult::victory) {
                            game.player.hell_gatekeeper_state =
                                yipe::HellGatekeeperState::defeated;
                        } else if (encounter == EncounterResult::defeat) {
                            recover_from_combat_defeat(
                                window, game, map, random, 41);
                        }
                    }
                    suppress_roaming_update = true;
                    break;
                }
                if (game.location.map_id == 0x75
                    && game.player.hell_gatekeeper_state
                        != yipe::HellGatekeeperState::defeated) {
                    game.player.hell_gatekeeper_state =
                        yipe::HellGatekeeperState::absent;
                }
                yipe::return_to_surface(game.location);
                map = yipe::starting_map();
                game.temporarily_defeated_monster_placements.clear();
                initialize_map_state(game, map);
                suppress_roaming_update = true;
                synchronize_player_location(game);
                apply_map_tile_changes(map, game);
                yipe::initialize_map_visibility(
                    game.map_visibility, map, game.location.position);
                audio.play(201, settings.sound_enabled);
                break;
            }
            case 0x17: {
                audio.play(144, settings.sound_enabled);
                const int damage = yipe::apply_fire_wall(game.player, random);
                show_message(window, "Fire Wall",
                             "OUCH! You burn your hand slightly on the fire wall and lose "
                                 + std::to_string(damage) + " health.");
                break;
            }
            case 0x1b:
                if (const std::optional<yipe::MapPosition> destination =
                        map.linked_destination()) {
                    game.location.position = *destination;
                    yipe::reveal_map_from(
                        game.map_visibility, map, game.location.position);
                    audio.play(142, settings.sound_enabled);
                }
                break;
            default:
                break;
            }
            render_game(renderer, map, textures, game);
        }

        if (suppress_roaming_update || encounter_resolved) {
            continue;
        }
        if (game.roaming_monster
            && game.roaming_monster->position == game.location.position) {
            const int monster_type = game.roaming_monster->type_id;
            const EncounterResult encounter = fight_monster(
                transient_monster_placement(
                    monster_type, game.roaming_monster->position));
            game.roaming_monster.reset();
            if (encounter == EncounterResult::quit_requested) {
                if (save_current_game(window, save_path, game, random, false)) {
                    return {StartupChoice::quit};
                }
                render_game(renderer, map, textures, game);
                continue;
            }
            if (encounter == EncounterResult::fled) {
                game.location = previous_location;
                synchronize_player_location(game);
            } else if (encounter == EncounterResult::defeat) {
                recover_from_combat_defeat(
                    window, game, map, random, monster_type);
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (game.pursuing_monster
            && game.pursuing_monster->position == game.location.position) {
            const int monster_type = game.pursuing_monster->type_id;
            const EncounterResult encounter = fight_monster(
                transient_monster_placement(
                    monster_type, game.pursuing_monster->position));
            if (encounter == EncounterResult::quit_requested) {
                if (save_current_game(window, save_path, game, random, false)) {
                    return {StartupChoice::quit};
                }
                render_game(renderer, map, textures, game);
                continue;
            }
            if (encounter == EncounterResult::victory) {
                game.pursuing_monster.reset();
            } else if (encounter == EncounterResult::fled) {
                game.location = previous_location;
                synchronize_player_location(game);
            } else {
                recover_from_combat_defeat(
                    window, game, map, random, monster_type);
            }
            render_game(renderer, map, textures, game);
            continue;
        }

        const std::vector<yipe::MonsterPlacement> active_placements =
            active_monster_placements(game);
        const std::optional<yipe::MapPosition> pursuing_position =
            game.pursuing_monster
            ? std::optional<yipe::MapPosition>{
                  game.pursuing_monster->position}
            : std::nullopt;
        const yipe::RoamingMonsterUpdate roaming_update =
            yipe::update_roaming_monster(
                game.roaming_monster, map, game.player, game.location,
                static_cast<std::uint16_t>(result.original_code),
                active_placements, game.map_visibility, {}, random,
                pursuing_position);
        if (roaming_update
            == yipe::RoamingMonsterUpdate::encountered_player) {
            const int monster_type = game.roaming_monster->type_id;
            const EncounterResult encounter = fight_monster(
                transient_monster_placement(
                    monster_type, game.roaming_monster->position));
            game.roaming_monster.reset();
            if (encounter == EncounterResult::quit_requested) {
                if (save_current_game(window, save_path, game, random, false)) {
                    return {StartupChoice::quit};
                }
                render_game(renderer, map, textures, game);
                continue;
            }
            if (encounter == EncounterResult::defeat) {
                recover_from_combat_defeat(
                    window, game, map, random, monster_type);
            }
            render_game(renderer, map, textures, game);
            continue;
        }
        if (roaming_update == yipe::RoamingMonsterUpdate::spawned) {
            render_game(renderer, map, textures, game);
            continue;
        }

        if (game.pursuing_monster
            && yipe::move_pursuing_monster(
                   *game.pursuing_monster, map, game.location.position,
                   active_placements, game.roaming_monster,
                   game.map_visibility, random)
                == yipe::RoamingMonsterMove::encountered_player) {
            const int monster_type = game.pursuing_monster->type_id;
            const EncounterResult encounter = fight_monster(
                transient_monster_placement(
                    monster_type, game.pursuing_monster->position));
            if (encounter == EncounterResult::quit_requested) {
                if (save_current_game(window, save_path, game, random, false)) {
                    return {StartupChoice::quit};
                }
                render_game(renderer, map, textures, game);
                continue;
            }
            if (encounter == EncounterResult::victory) {
                game.pursuing_monster.reset();
            } else if (encounter == EncounterResult::defeat) {
                recover_from_combat_defeat(
                    window, game, map, random, monster_type);
            }
        }
        render_game(renderer, map, textures, game);
    }
}

} // namespace

int main(int argc, char* argv[]) {
    try {
        const bool smoke_test = argc == 2 && std::string(argv[1]) == "--smoke-test";
        const SdlSession sdl;
        const RuntimeAssets assets = runtime_assets(argv[0]);
        const std::filesystem::path preference_directory =
            default_preference_directory();
        const std::filesystem::path settings_path =
            preference_directory / "settings.y3r";
        const std::filesystem::path high_scores_path =
            preference_directory / "highscores.y3r";
        yipe::GameSettings settings;
        std::string settings_warning;
        if (std::filesystem::exists(settings_path)) {
            try {
                settings = yipe::load_settings(settings_path);
            } catch (const std::runtime_error& error) {
                settings.sound_enabled = false;
                settings_warning = std::string(error.what())
                    + "\n\nDefaulting sound off and restoring default keys.";
                std::cerr << "Yipe! III: " << settings_warning << '\n';
            }
        }

        const Uint32 window_flags = smoke_test ? SDL_WINDOW_HIDDEN : SDL_WINDOW_RESIZABLE;
        Window window(SDL_CreateWindow("Yipe! III", SDL_WINDOWPOS_CENTERED,
                                       SDL_WINDOWPOS_CENTERED, startup_width * 2,
                                       startup_height * 2, window_flags),
                      SDL_DestroyWindow);
        if (!window) {
            throw std::runtime_error(std::string("could not create window: ") + SDL_GetError());
        }

        Renderer renderer(SDL_CreateRenderer(window.get(), -1,
                                             SDL_RENDERER_ACCELERATED | SDL_RENDERER_PRESENTVSYNC),
                          SDL_DestroyRenderer);
        if (!renderer) {
            renderer.reset(SDL_CreateRenderer(window.get(), -1, SDL_RENDERER_SOFTWARE));
        }
        if (!renderer
            || SDL_RenderSetLogicalSize(renderer.get(), startup_width, startup_height) != 0) {
            throw std::runtime_error(std::string("could not create renderer: ") + SDL_GetError());
        }
        SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "nearest");

        AudioPlayer audio(assets);
        if (settings.sound_enabled && !audio.available()) {
            settings.sound_enabled = false;
            settings_warning = audio.error() + "\n\nDefaulting sound off.";
            std::cerr << "Yipe! III: " << settings_warning << '\n';
        }

        const Texture menu =
            load_bitmap(renderer.get(), assets.bitmaps / "379-1033.bmp");
        const Texture copyright =
            load_bitmap(renderer.get(), assets.bitmaps / "326-1033.bmp");
        const GameTextures game_textures =
            load_game_textures(renderer.get(), assets.bitmaps);
        if (smoke_test) {
            if (!audio.available()) {
                throw std::runtime_error(audio.error());
            }
            audio.validate_packaged_monster_sounds();
            render_startup(renderer.get(), menu.get(), copyright.get());
            prepare_game_view(window.get(), renderer.get());
            yipe::GameState game;
            for (int map_id = 0; map_id <= 0x85; ++map_id) {
                std::optional<yipe::Map> map = yipe::original_map(map_id);
                if (map) {
                    game.location.map_id = map_id;
                    game.location.position = {5, 5};
                    initialize_map_state(game, *map);
                    yipe::initialize_map_visibility(
                        game.map_visibility, *map, game.location.position);
                    if (map->events().overlay.has_position()) {
                        yipe::reveal_map_from(
                            game.map_visibility, *map,
                            map->events().overlay.position);
                    }
                    render_game(renderer.get(), *map, game_textures, game);
                }
            }
            for (int map_id = 0x76; map_id <= 0x78; ++map_id) {
                yipe::MsvcRandom random(1);
                yipe::Map map =
                    yipe::generate_original_map(map_id, {1, 1}, random);
                game.location.map_id = map_id;
                game.location.position = {1, 1};
                initialize_map_state(game, map);
                yipe::initialize_map_visibility(
                    game.map_visibility, map, game.location.position);
                render_game(renderer.get(), map, game_textures, game);
            }
            yipe::MsvcRandom other_random(1);
            game = yipe::GameState{};
            yipe::Map other_map = *yipe::original_map(0x12);
            game.location = {{5, 8}, 1, 0, 0, false, 0x12};
            if (!yipe::try_move(other_map, game.location.position, 0, -1).position_changed
                || other_map.tile(game.location.position) != 0x35) {
                throw std::runtime_error("the special flowers are not reachable");
            }
            const yipe::OtherActionResult flowers =
                use_other_action(game, other_map, other_random);
            if (flowers.message_resource_id != 1023 || !game.map_tile_changes.empty()
                || other_map.tile({3, 7}) != 0x46 || other_map.tile({4, 7}) != 0x46) {
                throw std::runtime_error("Other did not open the temporary mountain passage");
            }
            other_map = *yipe::original_map(0x12);
            apply_map_tile_changes(other_map, game);
            if (other_map.tile({3, 7}) != 3 || other_map.tile({4, 7}) != 3) {
                throw std::runtime_error("Other passage did not reset on map reload");
            }
            other_map = *yipe::original_map(0x1f);
            game.location = {{4, 7}, -4, -3, 0, false, 0x1f};
            if (yipe::try_move(other_map, game.location.position, 0, 1).original_code != 3
                || other_map.tile(game.location.position) != 0x42) {
                throw std::runtime_error("the secret Hell tunnel is not reachable");
            }
            const int health_before_tunnel = game.player.health;
            static_cast<void>(use_other_action(game, other_map, other_random));
            if (other_map.id() != 0x75 || game.map_visibility.map_id != 0x75
                || game.player.health != health_before_tunnel || game.player.death_count != 0
                || !game.player.entered_hell_through_secret_passage) {
                throw std::runtime_error("Other did not enter Hell without a death");
            }
            render_game(renderer.get(), other_map, game_textures, game);
            other_map = *yipe::original_map(3);
            game.location = {{4, 7}, -2, 2, 0, false, 3};
            game.player.stamps_found = 5;
            initialize_map_state(game, other_map);
            static_cast<void>(use_other_action(game, other_map, other_random));
            if (other_map.id() != 0x15 || game.map_visibility.map_id != 0x15
                || game.location.position != yipe::MapPosition{6, 8}
                || game.player.pending_relocation != 0) {
                throw std::runtime_error("Other did not follow the stone-circle relocation");
            }
            other_map = *yipe::original_map(0x2d);
            game.location = {{10, 9}, 0, 0, 1, false, 0x2d};
            game.player.strength = 40;
            initialize_map_state(game, other_map);
            if (yipe::try_move(other_map, game.location.position, 1, 0).original_code != 0x1a
                || !other_map.toggle_linked_tiles() || other_map.tile({12, 9}) != 0x53) {
                throw std::runtime_error("the strength-gated passage did not open");
            }
            other_map = *yipe::original_map(0x2c);
            game.location = {{0, 5}, 0, 0, 1, false, 0x2c};
            initialize_map_state(game, other_map);
            if (other_map.tile({0, 5}) != 0x53 || other_map.tile({0, 4}) != 0x25) {
                throw std::runtime_error("map arrival did not clear only the occupied wall tile");
            }
            game.player.gatekeeper_state = 1;
            yipe::enter_hell(game.player, game.location, true);
            other_map = *yipe::original_map(0x75);
            initialize_map_state(game, other_map);
            if (other_map.tile({12, 1}) != 0x27) {
                throw std::runtime_error("quest progress did not open Hell's east boundary");
            }
            const auto packaged_names = yipe::load_item_names(assets.item_names);
            if (packaged_names.used_embedded_fallback) {
                throw std::runtime_error("the supplied equipment-name file is missing or invalid");
            }
            for (std::size_t index = 0; index < packaged_names.weapons.size(); ++index) {
                if (equipment_name(yipe::EquipmentKind::weapon, static_cast<int>(index + 1))
                    != packaged_names.weapons[index]) {
                    throw std::runtime_error("weapon displays did not use the packaged item names");
                }
            }
            for (std::size_t index = 0; index < packaged_names.armor.size(); ++index) {
                if (equipment_name(yipe::EquipmentKind::armor, static_cast<int>(index + 1))
                    != packaged_names.armor[index]) {
                    throw std::runtime_error("armor displays did not use the packaged item names");
                }
            }
            yipe::Player equipment_player;
            if (equipment_statistics_page(equipment_player)
                != "Current Items Equipped\nAC = Armor Class; WC = Weapon Class\n\n"
                   "Weapon: Small Dagger  (AC 0, WC 2)\n"
                   "Body Armor: Robe  (AC 2, WC 0)\n"
                   "Helmet: Yipe! Baseball Hat  (AC 1, WC 0)\n"
                   "Gloves: None  (AC 0, WC 0)\n"
                   "Ring: None  (AC 0, WC 0)\n"
                   "Boots: None  (AC 0, WC 0)\n"
                   "Amulet: None  (AC 0, WC 0)") {
                throw std::runtime_error("equipment statistics lost the starting gear or empty slots");
            }
            equipment_player.weapon_rank = 38;
            equipment_player.armor_rank = 30;
            equipment_player.helmet_rank = 7;
            equipment_player.gloves_rank = 5;
            equipment_player.ring_rank = 5;
            equipment_player.boots_rank = 6;
            equipment_player.necklace_rank = 6;
            const auto highest_equipment_page = equipment_statistics_page(equipment_player);
            if (highest_equipment_page
                != "Current Items Equipped\nAC = Armor Class; WC = Weapon Class\n\n"
                   "Weapon: Almighty Longsword  (AC 0, WC 38)\n"
                   "Body Armor: Dragonscale Armor  (AC 30, WC 0)\n"
                   "Helmet: Double Horned Helmet  (AC 7, WC 4)\n"
                   "Gloves: Magical Spiked Gloves  (AC 5, WC 1)\n"
                   "Ring: Protection Ring  (AC 6, WC 0)\n"
                   "Boots: Speedy Boots  (AC 7, WC 0)\n"
                   "Amulet: Eye-Piercing Amulet  (AC 6, WC 2)"
                || yipe::player_attack_equipment_rating(equipment_player) != 45
                || yipe::player_defense_equipment_rating(equipment_player) != 61) {
                throw std::runtime_error("equipment statistics lost the special-tier names or bonuses");
            }
            equipment_player.helmet_rank = 5;
            equipment_player.gloves_rank = 4;
            equipment_player.necklace_rank = 5;
            const auto middle_equipment_page = equipment_statistics_page(equipment_player);
            if (middle_equipment_page.find("Helmet: Raiders Hard Hat  (AC 5, WC 1)")
                    == std::string::npos
                || middle_equipment_page.find("Gloves: Leather Gloves  (AC 4, WC 1)")
                    == std::string::npos
                || middle_equipment_page.find("Amulet: Fire Amulet  (AC 5, WC 1)")
                    == std::string::npos) {
                throw std::runtime_error("equipment statistics lost the intermediate attack bonuses");
            }
            equipment_player.helmet_rank = 6;
            equipment_player.gloves_rank = 6;
            equipment_player.ring_rank = 0xffff;
            equipment_player.boots_rank = 0x8000;
            const auto unusual_equipment_page = equipment_statistics_page(equipment_player);
            if (unusual_equipment_page.find("Helmet: Horned Wooden Helmet  (AC 6, WC 2)")
                    == std::string::npos
                || unusual_equipment_page.find("Gloves: Rank 6  (AC 6, WC 0)")
                    == std::string::npos
                || unusual_equipment_page.find("Ring: None  (AC -1, WC 0)")
                    == std::string::npos
                || unusual_equipment_page.find("Boots: None  (AC -32768, WC 0)")
                    == std::string::npos) {
                throw std::runtime_error("equipment statistics lost signed ranks or the display-only glove rule");
            }
            yipe::GameState special_game;
            auto& special_player = special_game.player;
            if (special_statistics_page(special_player)
                != "Rare Stamps Collected: 0\nPrincesses Rescued: 0\n\nKeys:\nNone"
                   "\n\nSpecial Items:\nNone\n\nCurrent Ability: None") {
                throw std::runtime_error("special statistics lost the empty-state labels");
            }
            special_player.stamps_found = 6;
            special_player.princesses_found = 20;
            special_player.inventory_values[0] = 1;
            special_player.inventory_values[29] = 0x7fff;
            special_player.inventory_values[1] = 0x8000;
            special_player.inventory_values[2] = 0xffff;
            constexpr std::array<std::string_view, 6> special_names{
                "Rainbow Key", "Underworld Key", "Ring of Bonus Healing",
                "Tree Climbing Tools", "Waterproof Shoe Spray", "King's Lucky Rabbit Foot",
            };
            constexpr std::array<std::string_view, 5> ability_names{
                "None", "Backstab", "Frighten", "Invisibility", "Enrage",
            };
            for (unsigned mask = 0; mask < 64; ++mask) {
                special_player.has_rainbow_key = (mask & 1u) != 0;
                special_player.has_underworld_key = (mask & 2u) != 0;
                special_player.has_bonus_healing_ring = (mask & 4u) != 0;
                special_player.has_tree_climbing_shoes = (mask & 8u) != 0;
                special_player.has_waterproof_shoe_spray = (mask & 16u) != 0;
                special_player.has_lucky_rabbit_foot = (mask & 32u) != 0;
                std::ostringstream expected;
                expected << "Rare Stamps Collected: 6\nPrincesses Rescued: 2\n\nKeys:";
                for (unsigned index = 0; index < special_names.size(); ++index) {
                    if (index == 2) {
                        if ((mask & 3u) == 0) {
                            expected << "\nNone";
                        }
                        expected << "\n\nSpecial Items:";
                    }
                    if ((mask & (1u << index)) != 0) {
                        expected << '\n' << special_names[index];
                    }
                }
                if ((mask & 60u) == 0) {
                    expected << "\nNone";
                }
                expected << "\n\nCurrent Ability: ";
                for (std::size_t ability = 0; ability < ability_names.size(); ++ability) {
                    special_player.special_ability = static_cast<yipe::SpecialAbility>(ability);
                    if (special_statistics_page(special_player)
                        != expected.str() + std::string(ability_names[ability])) {
                        throw std::runtime_error("special statistics lost an item, ability, count, or display order");
                    }
                }
            }
            for (const std::uint16_t stamps : std::array<std::uint16_t, 3>{0x7fff, 0x8000, 0xffff}) {
                special_player.stamps_found = stamps;
                const int signed_stamps = stamps < 0x8000 ? stamps : static_cast<int>(stamps) - 65536;
                if (!special_statistics_page(special_player).starts_with(
                        "Rare Stamps Collected: " + std::to_string(signed_stamps) + '\n')) {
                    throw std::runtime_error("special statistics lost the signed stamp count");
                }
            }
            const auto special_save_path = std::filesystem::temp_directory_path()
                / "yipe3-smoke-special-statistics.y3r";
            yipe::save_game_atomic(special_save_path, special_game);
            const auto saved_special_page = special_statistics_page(special_player);
            if (special_statistics_page(yipe::load_game(special_save_path).player)
                != saved_special_page) {
                throw std::runtime_error("special statistics changed after saving and resuming");
            }
            std::filesystem::remove(special_save_path);
            special_player.special_ability = static_cast<yipe::SpecialAbility>(255);
            bool rejected_ability = false;
            try {
                static_cast<void>(special_statistics_page(special_player));
            } catch (const std::invalid_argument&) {
                rejected_ability = true;
            }
            if (!rejected_ability) {
                throw std::runtime_error("special statistics silently accepted an unknown ability");
            }
            yipe::Player statistics_player;
            statistics_player.encountered_monster_types[2] = true;
            if (monster_defeat_page(statistics_player, 1).find("Giant Beetle") != std::string::npos) {
                throw std::runtime_error("kill statistics exposed an undefeated monster name");
            }
            statistics_player.record_monster_defeat(2);
            statistics_player.record_monster_defeat(41);
            statistics_player.record_monster_defeat(50);
            statistics_player.death_count = 7;
            const auto first_kill_page = monster_defeat_page(statistics_player, 1);
            const auto last_kill_page = monster_defeat_page(statistics_player, 5);
            if (!first_kill_page.starts_with("Kills: 2  Deaths: 7")
                || first_kill_page.find("Giant Beetle: 1") == std::string::npos
                || last_kill_page.find("Gatekeeper: 1") == std::string::npos) {
                throw std::runtime_error("kill statistics lost counts, pagination, or the 41-type total");
            }
            for (int page = 1; page <= 5; ++page) {
                const auto text = monster_defeat_page(statistics_player, page);
                if (!text.ends_with("Page " + std::to_string(page) + " of 5")
                    || std::count(text.begin(), text.end(), ':') != (page == 5 ? 7 : 11)) {
                    throw std::runtime_error("kill statistics page has an incorrect row count");
                }
            }
            statistics_player.monster_defeat_history_complete = false;
            if (monster_defeat_page(statistics_player, 3).find("Earlier per-type counts are unavailable")
                == std::string::npos) {
                throw std::runtime_error("kill statistics hid the older-save history limitation");
            }
            yipe::HighScores score_table = yipe::default_high_scores();
            for (std::size_t index = 0; index < score_table.size(); ++index) {
                score_table[index] = {"Player" + std::to_string(index),
                                      static_cast<std::int32_t>(100 + index)};
            }
            constexpr std::array<std::string_view, 3> score_modes{"Easy", "Medium", "Hard"};
            for (int difficulty = 1; difficulty <= 3; ++difficulty) {
                const auto text = high_score_page(score_table, difficulty);
                const std::size_t first = static_cast<std::size_t>((difficulty - 1) * 15);
                std::ostringstream expected;
                expected << score_modes[static_cast<std::size_t>(difficulty - 1)]
                         << " Mode\n";
                constexpr std::array<std::string_view, 5> headings{
                    "Most Kills", "Most Princesses", "Most Cash at One Time", "Highest Level",
                    "First 3 Players to Complete Game",
                };
                for (std::size_t category = 0; category < headings.size(); ++category) {
                    expected << '\n' << headings[category] << '\n';
                    for (std::size_t rank = 0; rank < 3; ++rank) {
                        const auto index = first + category * 3 + rank;
                        expected << rank + 1 << ". Player" << index;
                        if (category < 4) {
                            expected << " - " << 100 + index;
                        }
                        expected << '\n';
                    }
                }
                if (text != expected.str()) {
                    throw std::runtime_error("high scores lost difficulty, category, rank, or value order");
                }
            }
            if (high_score_page(score_table, -1) != high_score_page(score_table, 1)) {
                throw std::runtime_error("high scores did not fall back to Easy mode");
            }
            const auto smoke_scores_path = std::filesystem::temp_directory_path()
                / "yipe3-smoke-high-scores.y3r";
            score_table = yipe::default_high_scores();
            statistics_player.gold = 50;
            record_player_high_scores(smoke_scores_path, score_table, statistics_player);
            if (yipe::load_high_scores(smoke_scores_path) != score_table
                || score_table[15].value != 3 || score_table[21].value != 50
                || score_table[27].name != "----") {
                throw std::runtime_error("score submission did not persist the current player values");
            }
            const auto saved_scores = score_table;
            record_player_high_scores(smoke_scores_path / "invalid.y3r", score_table, statistics_player);
            statistics_player.gold = 60;
            bool rejected_score_write = false;
            try {
                record_player_high_scores(smoke_scores_path / "invalid.y3r", score_table, statistics_player);
            } catch (const std::runtime_error&) {
                rejected_score_write = true;
            }
            if (!rejected_score_write || score_table != saved_scores
                || yipe::load_high_scores(smoke_scores_path) != saved_scores) {
                throw std::runtime_error("failed score submission published or overwrote unpersisted values");
            }
            statistics_player.gatekeeper_state = 3;
            record_player_high_scores(smoke_scores_path, score_table, statistics_player);
            if (score_table[21].value != 60 || score_table[27].name != statistics_player.name
                || yipe::load_high_scores(smoke_scores_path) != score_table) {
                throw std::runtime_error("score submission lost the completed game or same-name improvement");
            }
            std::filesystem::remove(smoke_scores_path);
            if (yipe::original_help_pages.front().first != "Background"
                || yipe::original_help_pages.back().first != "Distribution"
                || yipe::original_help_pages.at(3).first != "New Character Creation") {
                throw std::runtime_error("original help lost its endpoints or character-help destination");
            }
            for (std::size_t page = 0; page < yipe::original_help_pages.size(); ++page) {
                const std::string formatted = original_help_page(page);
                std::istringstream lines(formatted);
                std::string line;
                while (std::getline(lines, line)) {
                    if (line.size() > 72) {
                        throw std::runtime_error("original help contains an unwrapped line");
                    }
                }
                const auto body_start = formatted.find("\n\n") + 2;
                std::istringstream actual_words(formatted.substr(body_start));
                std::istringstream original_words{
                    std::string(yipe::original_help_pages[page].second)};
                std::string actual_word;
                std::string original_word;
                while (original_words >> original_word) {
                    if (!(actual_words >> actual_word) || actual_word != original_word) {
                        throw std::runtime_error("help formatting altered the original manual text");
                    }
                }
                if (actual_words >> actual_word) {
                    throw std::runtime_error("help formatting added words to the original manual");
                }
            }
            bool rejected_help_page = false;
            try {
                static_cast<void>(original_help_page(28));
            } catch (const std::out_of_range&) {
                rejected_help_page = true;
            }
            if (!rejected_help_page) {
                throw std::runtime_error("help accepted a page past the original final page");
            }
            prepare_startup_view(window.get(), renderer.get());
            SDL_Event help_key{};
            help_key.type = SDL_KEYDOWN;
            help_key.key.keysym.sym = SDLK_F1;
            help_key.key.repeat = 1;
            SDL_PushEvent(&help_key);
            SDL_Event help_quit{};
            help_quit.type = SDL_KEYDOWN;
            help_quit.key.keysym.sym = SDLK_q;
            SDL_PushEvent(&help_quit);
            if (run_startup_menu(renderer.get(), menu.get(), copyright.get()) != StartupChoice::quit) {
                throw std::runtime_error("startup help accepted a repeated keydown");
            }
            help_key.key.repeat = 0;
            SDL_PushEvent(&help_key);
            SDL_PushEvent(&help_quit);
            if (run_startup_menu(renderer.get(), menu.get(), copyright.get()) != StartupChoice::help) {
                throw std::runtime_error("startup help did not accept F1");
            }
            SDL_FlushEvent(SDL_KEYDOWN);
            SDL_Event score_key{};
            score_key.type = SDL_KEYDOWN;
            score_key.key.keysym.sym = SDLK_h;
            score_key.key.repeat = 1;
            SDL_PushEvent(&score_key);
            SDL_Event quit_key = score_key;
            quit_key.key.keysym.sym = SDLK_q;
            quit_key.key.repeat = 0;
            SDL_PushEvent(&quit_key);
            if (run_startup_menu(renderer.get(), menu.get(), copyright.get()) != StartupChoice::quit) {
                throw std::runtime_error("startup high scores accepted a repeated keydown");
            }
            score_key.key.repeat = 0;
            SDL_PushEvent(&score_key);
            SDL_PushEvent(&quit_key);
            if (run_startup_menu(renderer.get(), menu.get(), copyright.get())
                != StartupChoice::high_scores) {
                throw std::runtime_error("startup high scores did not accept H");
            }
            SDL_FlushEvent(SDL_KEYDOWN);
            prepare_game_view(window.get(), renderer.get());
            SDL_Event repeated_key{};
            repeated_key.type = SDL_KEYDOWN;
            repeated_key.key.repeat = 1;
            repeated_key.key.keysym.sym = SDLK_i;
            SDL_PushEvent(&repeated_key);
            SDL_Event exposed{};
            exposed.type = SDL_WINDOWEVENT;
            exposed.window.event = SDL_WINDOWEVENT_EXPOSED;
            SDL_PushEvent(&exposed);
            SDL_Event combat_key = repeated_key;
            combat_key.key.repeat = 0;
            SDL_PushEvent(&combat_key);
            const yipe::GameSettings default_settings;
            if (choose_combat_action(
                    renderer.get(),
                    "Monsters block your path.\nYour HP: 10/10  AP: 5/5",
                    default_settings) != 2) {
                throw std::runtime_error(
                    "combat picker did not consume the configured item key");
            }
            SDL_Event combat_click{};
            combat_click.type = SDL_MOUSEBUTTONUP;
            combat_click.button.button = SDL_BUTTON_LEFT;
            SDL_RenderLogicalToWindow(
                renderer.get(), 100.0F, 288.0F,
                &combat_click.button.x, &combat_click.button.y);
            SDL_PushEvent(&combat_click);
            if (choose_combat_action(
                    renderer.get(),
                    "Monsters block your path.\nYour HP: 10/10  AP: 5/5",
                    default_settings) != 5) {
                throw std::runtime_error(
                    "combat picker did not retain mouse target selection");
            }
            return 0;
        }

        if (!settings_warning.empty()) {
            show_message(window.get(), "Settings Warning", settings_warning);
        }
        yipe::HighScores high_scores = std::filesystem::exists(high_scores_path)
            ? yipe::load_high_scores(high_scores_path) : yipe::default_high_scores();
        std::filesystem::path active_save_path;
        std::optional<GameExit> pending_exit;
        while (true) {
            prepare_startup_view(window.get(), renderer.get());
            audio.play(152, settings.sound_enabled);
            std::optional<GameExit> pending = std::move(pending_exit);
            pending_exit.reset();
            const StartupChoice choice = pending
                ? pending->choice
                : run_startup_menu(renderer.get(), menu.get(), copyright.get());
            switch (choice) {
            case StartupChoice::new_game: {
                active_save_path.clear();
                yipe::MsvcRandom creation_random(
                    static_cast<std::uint32_t>(std::time(nullptr)));
                const std::optional<yipe::Player> player = choose_character(
                    window.get(), renderer.get(), game_textures, creation_random);
                if (!player) {
                    break;
                }
                yipe::GameState game;
                game.player = *player;
                game.random_state = creation_random.state();
                GameExit exit = run_game(
                    window.get(), renderer.get(), game_textures, game,
                    active_save_path, settings_path, high_scores_path, high_scores,
                    settings, audio);
                if (exit.choice == StartupChoice::quit) {
                    return 0;
                }
                pending_exit = std::move(exit);
                break;
            }
            case StartupChoice::load_game:
                try {
                    auto opened = pending && pending->opened_game
                        ? std::move(pending)
                        : choose_game_to_open(preference_directory);
                    if (!opened) {
                        break;
                    }
                    active_save_path = opened->opened_save_path;
                    GameExit exit = run_game(
                        window.get(), renderer.get(), game_textures,
                        std::move(*opened->opened_game), active_save_path, settings_path,
                        high_scores_path, high_scores, settings, audio);
                    if (exit.choice == StartupChoice::quit) {
                        return 0;
                    }
                    pending_exit = std::move(exit);
                } catch (const std::exception& error) {
                    show_message(window.get(), "Load Game Failed", error.what());
                }
                break;
            case StartupChoice::settings:
                show_settings(window.get(), settings, settings_path, audio);
                break;
            case StartupChoice::high_scores:
                show_high_scores(window.get(), high_scores);
                break;
            case StartupChoice::help:
                show_help(window.get(), settings);
                break;
            case StartupChoice::quit:
                return 0;
            }
        }
    } catch (const std::exception& error) {
        std::cerr << "Yipe! III: " << error.what() << '\n';
        return 1;
    }
}
