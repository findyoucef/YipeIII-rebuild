#include "yipe/map.hpp"
#include "yipe/shop.hpp"

#include <SDL.h>

#include <array>
#include <cstdint>
#include <filesystem>
#include <iostream>
#include <memory>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace {

constexpr int startup_width = 288;
constexpr int startup_menu_height = 360;
constexpr int startup_height = 397;
constexpr int tile_size = 32;
constexpr int map_width = yipe::Map::width * tile_size;
constexpr int map_height = yipe::Map::height * tile_size;

using Window = std::unique_ptr<SDL_Window, decltype(&SDL_DestroyWindow)>;
using Renderer = std::unique_ptr<SDL_Renderer, decltype(&SDL_DestroyRenderer)>;
using Texture = std::unique_ptr<SDL_Texture, decltype(&SDL_DestroyTexture)>;
using Cursor = std::unique_ptr<SDL_Cursor, decltype(&SDL_FreeCursor)>;

enum class StartupChoice { new_game, load_game, quit };

class SdlSession {
public:
    SdlSession() {
        if (SDL_Init(SDL_INIT_VIDEO) != 0) {
            throw std::runtime_error(std::string("SDL initialization failed: ") + SDL_GetError());
        }
    }
    ~SdlSession() { SDL_Quit(); }

    SdlSession(const SdlSession&) = delete;
    SdlSession& operator=(const SdlSession&) = delete;
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

std::filesystem::path runtime_asset_directory(const char* executable) {
    std::unique_ptr<char, decltype(&SDL_free)> base_path(SDL_GetBasePath(), SDL_free);
    if (base_path) {
        const std::filesystem::path beside_executable =
            std::filesystem::path(base_path.get()) / "assets";
        if (std::filesystem::exists(beside_executable / "379-1033.bmp")) {
            return beside_executable;
        }
    }

    const std::filesystem::path source_tree = "assets/embedded/bitmaps";
    if (std::filesystem::exists(source_tree / "379-1033.bmp")) {
        return source_tree;
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
    std::vector<std::pair<int, Texture>> terrain;
    Texture warrior;

    [[nodiscard]] SDL_Texture* find(int resource_id) const {
        for (const auto& [id, texture] : terrain) {
            if (id == resource_id) {
                return texture.get();
            }
        }
        throw std::runtime_error("missing terrain bitmap resource "
                                 + std::to_string(resource_id));
    }
};

GameTextures load_game_textures(SDL_Renderer* renderer, const std::filesystem::path& assets) {
    constexpr std::array resource_ids{133, 134, 135, 136, 137, 138, 139, 140, 141, 142,
                                      143, 144, 145, 146, 147, 153, 377, 418, 460};
    std::vector<std::pair<int, Texture>> terrain;
    terrain.reserve(resource_ids.size());
    for (const int id : resource_ids) {
        terrain.emplace_back(
            id, load_bitmap(renderer, assets / (std::to_string(id) + "-1033.bmp")));
    }
    return {std::move(terrain), load_bitmap(renderer, assets / "406-1033.bmp", true)};
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
    default:
        return graphic(153, 32, 32);
    }
}

void prepare_game_view(SDL_Window* window, SDL_Renderer* renderer) {
    SDL_SetWindowSize(window, map_width * 2, map_height * 2);
    if (SDL_RenderSetLogicalSize(renderer, map_width, map_height) != 0) {
        throw std::runtime_error(std::string("could not set map render size: ") + SDL_GetError());
    }
}

void render_game(SDL_Renderer* renderer, const yipe::Map& map,
                 const GameTextures& textures, yipe::MapPosition player) {
    SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
    SDL_RenderClear(renderer);
    for (int x = 0; x < yipe::Map::width; ++x) {
        for (int y = 0; y < yipe::Map::height; ++y) {
            const TileGraphic graphic = tile_graphic(map.tile({x, y}), textures);
            const SDL_Rect destination{x * tile_size, y * tile_size, tile_size, tile_size};
            copy_texture(renderer, graphic.texture, &graphic.source, &destination);
        }
    }
    const SDL_Rect player_destination{player.x * tile_size, player.y * tile_size,
                                      tile_size, tile_size};
    copy_texture(renderer, textures.warrior.get(), nullptr, &player_destination);
    SDL_RenderPresent(renderer);
}

void show_freds_shop(SDL_Window* window) {
    std::ostringstream message;
    for (const yipe::ShopStockItem& item : yipe::freds_miscellaneous_stock()) {
        message << item.name << "  " << item.price << " gold\n";
    }
    if (SDL_ShowSimpleMessageBox(SDL_MESSAGEBOX_INFORMATION, "Fred's Miscellaneous Shop",
                                 message.str().c_str(), window)
        != 0) {
        throw std::runtime_error(std::string("could not show shop: ") + SDL_GetError());
    }
}

void run_game(SDL_Window* window, SDL_Renderer* renderer, const GameTextures& textures) {
    prepare_game_view(window, renderer);
    yipe::WorldLocation location;
    yipe::Map map = yipe::starting_map();
    render_game(renderer, map, textures, location.position);

    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            throw std::runtime_error(std::string("SDL event wait failed: ") + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            return;
        }
        if (event.type == SDL_WINDOWEVENT && event.window.event == SDL_WINDOWEVENT_EXPOSED) {
            render_game(renderer, map, textures, location.position);
        }
        if (event.type != SDL_KEYDOWN) {
            continue;
        }
        if (event.key.keysym.sym == SDLK_ESCAPE || event.key.keysym.sym == SDLK_q) {
            return;
        }

        int delta_x = 0;
        int delta_y = 0;
        switch (event.key.keysym.sym) {
        case SDLK_LEFT:
        case SDLK_a:
            delta_x = -1;
            break;
        case SDLK_RIGHT:
        case SDLK_d:
            delta_x = 1;
            break;
        case SDLK_UP:
        case SDLK_w:
            delta_y = -1;
            break;
        case SDLK_DOWN:
        case SDLK_s:
            delta_y = 1;
            break;
        default:
            continue;
        }
        const yipe::WorldLocation previous_location = location;
        const yipe::MovementResult result =
            yipe::try_move(map, location.position, delta_x, delta_y);
        if (result.position_changed) {
            if (result.original_code == 2 && yipe::cross_map_edge(location)) {
                const std::optional<yipe::Map> next_map = yipe::original_map(location.map_id);
                if (next_map) {
                    map = *next_map;
                } else {
                    location = previous_location;
                }
            }
            render_game(renderer, map, textures, location.position);
        } else if (result.original_code == 8 && location.map_id == 0x11) {
            show_freds_shop(window);
            render_game(renderer, map, textures, location.position);
        }
    }
}

} // namespace

int main(int argc, char* argv[]) {
    try {
        const bool smoke_test = argc == 2 && std::string(argv[1]) == "--smoke-test";
        const SdlSession sdl;
        const std::filesystem::path assets = runtime_asset_directory(argv[0]);

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

        const Texture menu = load_bitmap(renderer.get(), assets / "379-1033.bmp");
        const Texture copyright = load_bitmap(renderer.get(), assets / "326-1033.bmp");
        const GameTextures game_textures = load_game_textures(renderer.get(), assets);
        if (smoke_test) {
            render_startup(renderer.get(), menu.get(), copyright.get());
            prepare_game_view(window.get(), renderer.get());
            for (int map_id = 0; map_id <= 0x85; ++map_id) {
                const std::optional<yipe::Map> map = yipe::original_map(map_id);
                if (map) {
                    render_game(renderer.get(), *map, game_textures, {5, 5});
                }
            }
            return 0;
        }

        switch (run_startup_menu(renderer.get(), menu.get(), copyright.get())) {
        case StartupChoice::new_game:
            run_game(window.get(), renderer.get(), game_textures);
            break;
        case StartupChoice::load_game:
            std::cout << "load_game\n";
            break;
        case StartupChoice::quit:
            break;
        }
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "Yipe! III: " << error.what() << '\n';
        return 1;
    }
}
