#include "yipe/encounter.hpp"
#include "yipe/map.hpp"
#include "yipe/random.hpp"
#include "yipe/save.hpp"
#include "yipe/shop.hpp"

#include <SDL.h>

#include <algorithm>
#include <array>
#include <ctime>
#include <cstdint>
#include <filesystem>
#include <iostream>
#include <memory>
#include <optional>
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
    Texture giant_beetle;

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
    return {std::move(terrain), load_bitmap(renderer, assets / "406-1033.bmp", true),
            load_bitmap(renderer, assets / "175-1033.bmp", true)};
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

void prepare_startup_view(SDL_Window* window, SDL_Renderer* renderer) {
    SDL_SetWindowSize(window, startup_width * 2, startup_height * 2);
    if (SDL_RenderSetLogicalSize(renderer, startup_width, startup_height) != 0) {
        throw std::runtime_error(std::string("could not set startup render size: ")
                                 + SDL_GetError());
    }
}

void render_game(SDL_Renderer* renderer, const yipe::Map& map,
                 const GameTextures& textures, yipe::MapPosition player,
                 bool giant_beetle_defeated) {
    SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
    SDL_RenderClear(renderer);
    for (int x = 0; x < yipe::Map::width; ++x) {
        for (int y = 0; y < yipe::Map::height; ++y) {
            const TileGraphic graphic = tile_graphic(map.tile({x, y}), textures);
            const SDL_Rect destination{x * tile_size, y * tile_size, tile_size, tile_size};
            copy_texture(renderer, graphic.texture, &graphic.source, &destination);
        }
    }
    if (!giant_beetle_defeated && map.id() == 0x0b) {
        for (const yipe::MonsterPlacement& monster :
             yipe::original_monsters_for_map(map.id())) {
            const SDL_Rect destination{monster.position.x * tile_size,
                                       monster.position.y * tile_size,
                                       tile_size, tile_size};
            copy_texture(renderer, textures.giant_beetle.get(), nullptr, &destination);
        }
    }
    const SDL_Rect player_destination{player.x * tile_size, player.y * tile_size,
                                      tile_size, tile_size};
    copy_texture(renderer, textures.warrior.get(), nullptr, &player_destination);
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

std::optional<yipe::CharacterType> choose_character(SDL_Window* window) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Back"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Warrior"},
        SDL_MessageBoxButtonData{0, 2, "Rogue"},
        SDL_MessageBoxButtonData{0, 3, "Wizard"},
    };
    const int choice = show_choice(
        window, "Create Character",
        "Choose a character class.\n\nWarrior: strong melee fighter\n"
        "Rogue: agile adventurer\nWizard: intelligent spellcaster",
        buttons.data(), static_cast<int>(buttons.size()));
    if (choice < 1 || choice > 3) {
        return std::nullopt;
    }
    return static_cast<yipe::CharacterType>(choice);
}

std::filesystem::path default_save_path() {
    std::unique_ptr<char, decltype(&SDL_free)> preference_path(
        SDL_GetPrefPath("Yipe Software", "Yipe III Rebuild"), SDL_free);
    if (!preference_path) {
        throw std::runtime_error(std::string("could not locate the save directory: ")
                                 + SDL_GetError());
    }
    return std::filesystem::path(preference_path.get()) / "save.y3r";
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

void show_player_status(SDL_Window* window, const yipe::Player& player) {
    const auto carried = std::count_if(
        player.inventory.begin(),
        player.inventory.begin()
            + static_cast<std::ptrdiff_t>(yipe::Player::carried_inventory_size),
        [](std::uint16_t item) { return item != 0; });
    std::ostringstream message;
    message << player.name << "\nLevel " << player.level << "  XP " << player.experience
            << "  Gold " << player.gold << "\nHP " << player.health << "/"
            << player.maximum_health << "  SP " << player.spell_points << "/"
            << player.maximum_spell_points << "\nStrength " << player.strength
            << "  Dexterity " << player.dexterity << "  Intelligence "
            << player.intelligence << "\nWeapon " << player.weapon_rank << "  Armor "
            << player.armor_rank << "\nCarried items " << carried << "/"
            << yipe::Player::carried_inventory_size;
    show_message(window, "Player Status", message.str());
}

enum class EncounterResult { victory, fled, defeat };

EncounterResult run_giant_beetle_encounter(SDL_Window* window, yipe::Player& player,
                                            yipe::MsvcRandom& random) {
    yipe::Monster monster = yipe::start_single_monster_encounter(2, random);
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Attack"},
        SDL_MessageBoxButtonData{0, 2, "Use Item"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 3, "Flee"},
    };

    while (true) {
        std::ostringstream status;
        status << "A Giant Beetle blocks your path.\n\nYour HP: " << player.health << "/"
               << player.maximum_health << "\nGiant Beetle HP: " << monster.health << "/"
               << monster.maximum_health;
        const int action = show_choice(window, "Combat", status.str(), buttons.data(),
                                       static_cast<int>(buttons.size()));
        if (action == 1) {
            const yipe::CombatTurn turn = yipe::resolve_attack_turn(player, monster, random);
            std::ostringstream result;
            result << "You deal " << turn.player_damage << " damage.";
            if (turn.outcome == yipe::CombatOutcome::victory) {
                result << "\n\nThe Giant Beetle is defeated.\nYou gain "
                       << turn.reward.experience << " experience and " << turn.reward.gold
                       << " gold.";
                if (turn.reward.equipment_upgrade != yipe::EquipmentUpgrade::none) {
                    result << "\nYou found a "
                           << yipe::equipment_upgrade_name(turn.reward.equipment_upgrade)
                           << " upgrade.";
                }
                show_message(window, "Victory", result.str());
                return EncounterResult::victory;
            }
            result << "\nThe Giant Beetle deals " << turn.monster_damage << " damage.";
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
                player.use_inventory_item(*slot, random, true, true);
            if (!used.consumed) {
                show_message(window, "Inventory", used.message);
                continue;
            }
            const yipe::CombatTurn response =
                yipe::resolve_monster_turn(player, monster, random);
            std::ostringstream result;
            result << used.message;
            if (used.amount != 0) {
                result << " (" << used.amount << ")";
            }
            result << ".\nThe Giant Beetle deals " << response.monster_damage << " damage.";
            show_message(window, response.outcome == yipe::CombatOutcome::defeat ? "Defeat"
                                                                                 : "Combat",
                         result.str());
            if (response.outcome == yipe::CombatOutcome::defeat) {
                return EncounterResult::defeat;
            }
        } else {
            const yipe::FleeResult result = yipe::try_flee(player, monster, random);
            if (result.escaped) {
                show_message(window, "Combat", "You escaped from the Giant Beetle.");
                return EncounterResult::fled;
            }
            show_message(window, result.player_defeated ? "Defeat" : "Combat",
                         "You fail to escape. The Giant Beetle deals "
                             + std::to_string(result.monster_damage) + " damage.");
            if (result.player_defeated) {
                return EncounterResult::defeat;
            }
        }
    }
}

bool has_encounter_at(const yipe::GameState& game) {
    if (game.giant_beetle_defeated || game.location.map_id != 0x0b) {
        return false;
    }
    for (const yipe::MonsterPlacement& monster :
         yipe::original_monsters_for_map(game.location.map_id)) {
        if (monster.position == game.location.position) {
            return true;
        }
    }
    return false;
}

void save_current_game(SDL_Window* window, const std::filesystem::path& path,
                       yipe::GameState& game, const yipe::MsvcRandom& random,
                       bool show_confirmation) {
    game.random_state = random.state();
    yipe::save_game_atomic(path, game);
    if (show_confirmation) {
        show_message(window, "Game Saved", "Your game was saved to:\n" + path.string());
    }
}

bool confirm_quit(SDL_Window* window, const std::filesystem::path& save_path,
                  yipe::GameState& game, const yipe::MsvcRandom& random) {
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 0, "Cancel"},
        SDL_MessageBoxButtonData{0, 1, "Quit Without Saving"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 2,
                                 "Save and Quit"},
    };
    const int choice = show_choice(window, "Quit Game", "Do you want to save before quitting?",
                                   buttons.data(), static_cast<int>(buttons.size()));
    if (choice == 2) {
        save_current_game(window, save_path, game, random, false);
    }
    return choice == 1 || choice == 2;
}

bool run_game(SDL_Window* window, SDL_Renderer* renderer, const GameTextures& textures,
              yipe::GameState game, const std::filesystem::path& save_path) {
    prepare_game_view(window, renderer);
    const std::optional<yipe::Map> restored_map = yipe::original_map(game.location.map_id);
    if (!restored_map || !restored_map->contains(game.location.position)) {
        throw std::runtime_error("save contains an unsupported world location");
    }
    yipe::Map map = *restored_map;
    yipe::MsvcRandom random(game.random_state);
    render_game(renderer, map, textures, game.location.position,
                game.giant_beetle_defeated);

    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            throw std::runtime_error(std::string("SDL event wait failed: ") + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            save_current_game(window, save_path, game, random, false);
            return true;
        }
        if (event.type == SDL_WINDOWEVENT
            && (event.window.event == SDL_WINDOWEVENT_EXPOSED
                || event.window.event == SDL_WINDOWEVENT_FOCUS_GAINED)) {
            render_game(renderer, map, textures, game.location.position,
                        game.giant_beetle_defeated);
        }
        if (event.type != SDL_KEYDOWN) {
            continue;
        }
        if (event.key.keysym.sym == SDLK_ESCAPE || event.key.keysym.sym == SDLK_q) {
            if (confirm_quit(window, save_path, game, random)) {
                return true;
            }
            render_game(renderer, map, textures, game.location.position,
                        game.giant_beetle_defeated);
            continue;
        }
        if (event.key.keysym.sym == SDLK_F5) {
            save_current_game(window, save_path, game, random, true);
            render_game(renderer, map, textures, game.location.position,
                        game.giant_beetle_defeated);
            continue;
        }
        if (event.key.keysym.sym == SDLK_i) {
            show_player_status(window, game.player);
            render_game(renderer, map, textures, game.location.position,
                        game.giant_beetle_defeated);
            continue;
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
        const yipe::WorldLocation previous_location = game.location;
        const yipe::MovementResult result =
            yipe::try_move(map, game.location.position, delta_x, delta_y);
        if (result.position_changed) {
            if (result.original_code == 2 && yipe::cross_map_edge(game.location)) {
                const std::optional<yipe::Map> next_map =
                    yipe::original_map(game.location.map_id);
                if (next_map) {
                    map = *next_map;
                } else {
                    game.location = previous_location;
                }
            }
            render_game(renderer, map, textures, game.location.position,
                        game.giant_beetle_defeated);
            if (has_encounter_at(game)) {
                const EncounterResult encounter =
                    run_giant_beetle_encounter(window, game.player, random);
                if (encounter == EncounterResult::victory) {
                    game.giant_beetle_defeated = true;
                } else if (encounter == EncounterResult::fled) {
                    game.location = previous_location;
                } else {
                    show_message(window, "Defeat",
                                 "Your adventure has ended. Load a saved game to try again.");
                    return false;
                }
                render_game(renderer, map, textures, game.location.position,
                            game.giant_beetle_defeated);
            }
        } else if (result.original_code == 8 && game.location.map_id == 0x11) {
            show_freds_shop(window, game.player);
            render_game(renderer, map, textures, game.location.position,
                        game.giant_beetle_defeated);
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
                    render_game(renderer.get(), *map, game_textures, {5, 5}, false);
                }
            }
            return 0;
        }

        const std::filesystem::path save_path = default_save_path();
        while (true) {
            prepare_startup_view(window.get(), renderer.get());
            switch (run_startup_menu(renderer.get(), menu.get(), copyright.get())) {
            case StartupChoice::new_game: {
                const std::optional<yipe::CharacterType> type = choose_character(window.get());
                if (!type) {
                    break;
                }
                yipe::GameState game;
                game.player = yipe::Player(*type);
                game.random_state = static_cast<std::uint32_t>(std::time(nullptr));
                if (run_game(window.get(), renderer.get(), game_textures, game, save_path)) {
                    return 0;
                }
                break;
            }
            case StartupChoice::load_game:
                try {
                    if (run_game(window.get(), renderer.get(), game_textures,
                                 yipe::load_game(save_path), save_path)) {
                        return 0;
                    }
                } catch (const std::exception& error) {
                    show_message(window.get(), "Load Game Failed", error.what());
                }
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
