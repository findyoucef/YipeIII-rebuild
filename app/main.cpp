#include "yipe/casino.hpp"
#include "yipe/encounter.hpp"
#include "yipe/item_names.hpp"
#include "yipe/map.hpp"
#include "yipe/random.hpp"
#include "yipe/save.hpp"
#include "yipe/shop.hpp"
#include "yipe/spell.hpp"

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

std::string equipment_name(yipe::EquipmentKind kind, int rank) {
    static const yipe::ItemNames names = yipe::embedded_item_names();
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

void show_slots(SDL_Window* window, yipe::Player& player, yipe::MsvcRandom& random) {
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
                     yipe::MsvcRandom& random) {
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
                int& high_score) {
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
            const yipe::DartThrowResult result =
                yipe::throw_dart(aim_y, throw_index, random);
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

void show_casino(SDL_Window* window, yipe::Player& player, yipe::MsvcRandom& random) {
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
            show_slots(window, player, random);
        } else if (choice == 2) {
            show_dot_racing(window, player, random);
        } else if (choice == 3) {
            show_darts(window, player, random, darts_high_score);
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
            << player.maximum_spell_points << "  AP " << player.ability_points << "/"
            << player.maximum_ability_points << "\nStrength " << player.strength
            << "  Dexterity " << player.dexterity << "  Intelligence "
            << player.intelligence << "\nWeapon " << player.weapon_rank << "  Armor "
            << player.armor_rank << "\nCarried items " << carried << "/"
            << yipe::Player::carried_inventory_size;
    show_message(window, "Player Status", message.str());
}

enum class EncounterResult { victory, fled, defeat };

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
    message << "\n\nThe Giant Beetle is defeated.\nYou gain "
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

EncounterResult run_giant_beetle_encounter(SDL_Window* window, yipe::Player& player,
                                            yipe::MsvcRandom& random) {
    yipe::CombatEncounter encounter = yipe::start_monster_encounter(2, 1, random);
    yipe::Monster& monster = encounter.monsters[0];
    constexpr std::array buttons{
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT, 1, "Attack"},
        SDL_MessageBoxButtonData{0, 2, "Use Item"},
        SDL_MessageBoxButtonData{0, 3, "Cast Spell"},
        SDL_MessageBoxButtonData{SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT, 4, "Flee"},
    };

    while (true) {
        std::ostringstream status;
        status << "A Giant Beetle blocks your path.\n\nYour HP: " << player.health << "/"
               << player.maximum_health << "\nGiant Beetle HP: " << monster.health << "/"
               << monster.maximum_health;
        const int action = show_choice(window, "Combat", status.str(), buttons.data(),
                                       static_cast<int>(buttons.size()));
        if (action == 1) {
            const yipe::CombatTurn turn =
                yipe::resolve_attack_turn(player, encounter, random);
            std::ostringstream result;
            result << "You deal " << turn.player_damage << " damage.";
            if (turn.outcome == yipe::CombatOutcome::victory) {
                append_victory_details(result, turn, player);
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
                yipe::resolve_monster_turn(player, encounter, random);
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
        } else if (action == 3) {
            const std::optional<SpellSelection> selection =
                choose_spell(window, player, true);
            if (!selection) {
                continue;
            }
            const yipe::CombatSpellResult spell = yipe::resolve_spell_turn(
                player, encounter, selection->index, selection->power, random);
            if (spell.status != yipe::SpellCastStatus::cast) {
                show_message(window, "Spells", spell_failure_message(spell.status));
                continue;
            }

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
            result << "\nThe Giant Beetle deals " << spell.turn.monster_damage
                   << " damage.";
            show_message(window,
                         spell.turn.outcome == yipe::CombatOutcome::defeat
                             ? "Defeat" : "Combat",
                         result.str());
            if (spell.turn.outcome == yipe::CombatOutcome::defeat) {
                return EncounterResult::defeat;
            }
        } else {
            const yipe::FleeResult result = yipe::try_flee(player, encounter, random);
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

void apply_map_tile_changes(yipe::Map& map, const yipe::GameState& game) {
    for (const yipe::MapTileChange& change : game.map_tile_changes) {
        if (change.map_id == map.id()) {
            map.restore_tile(change.position, change.tile);
        }
    }
}

void cast_exploration_spell(SDL_Window* window, yipe::GameState& game,
                            yipe::Map& map, yipe::MsvcRandom& random) {
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
    if (result.location_changed) {
        const std::optional<yipe::Map> destination =
            yipe::original_map(game.location.map_id);
        if (!destination) {
            throw std::runtime_error("spell destination map is unavailable");
        }
        map = *destination;
        apply_map_tile_changes(map, game);
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
    apply_map_tile_changes(map, game);
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
        if (event.key.keysym.sym == SDLK_c) {
            cast_exploration_spell(window, game, map, random);
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
        const yipe::MovementState movement_state{
            game.player.has_tree_climbing_shoes,
            game.player.has_rainbow_key,
            game.player.has_underworld_key,
            game.player.unknown_0674,
        };
        const yipe::MovementResult result =
            yipe::try_move(map, game.location.position, delta_x, delta_y, movement_state);
        if (result.position_changed) {
            if (result.original_code == 0x12) {
                map.open_door(previous_location.position, game.location.position);
                remember_map_tile(game, map, game.location.position);
            } else if (result.original_code == 0x1a) {
                if (map.toggle_linked_tiles()) {
                    remember_map_tile(game, map, map.events().linked[0].position);
                    if (map.events().linked[1].has_position()) {
                        remember_map_tile(game, map, map.events().linked[1].position);
                    }
                }
            }
            bool changed_map = false;
            if (result.original_code == 2) {
                changed_map = yipe::cross_map_edge(game.location);
            } else if (result.original_code == 0x0e || result.original_code == 0x0f
                       || result.original_code == 0x10) {
                changed_map = yipe::change_map_depth(game.location, result.original_code);
            }
            if (changed_map) {
                const std::optional<yipe::Map> next_map =
                    yipe::original_map(game.location.map_id);
                if (next_map) {
                    map = *next_map;
                    apply_map_tile_changes(map, game);
                } else {
                    game.location = previous_location;
                }
            }
            render_game(renderer, map, textures, game.location.position,
                        game.giant_beetle_defeated);
            if (result.original_code == 3) {
                const std::uint16_t tile = map.tile(game.location.position);
                if (tile == 0x26) {
                    show_message(window, "Water Hole", "You are standing on a water hole.");
                } else if (tile == 0x42) {
                    show_message(
                        window, "Underworld Tunnel",
                        "You find a special tunnel leading down into the depths of the "
                        "underworld.");
                } else if (tile == 0x56 || tile == 0x57) {
                    show_message(window, "Tree",
                                 "You climb over the tree using your special shoes.");
                }
            }
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
                    show_message(window, "Restored",
                                 "Your health, spell points, and ability points are fully "
                                 "restored.");
                }
                break;
            case 0x0d:
                show_casino(window, game.player, random);
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
                show_message(window, "Secret Tree",
                             "A small note on the tree asks for a secret code.");
                break;
            case 0x16:
                yipe::return_to_surface(game.location);
                map = yipe::starting_map();
                apply_map_tile_changes(map, game);
                break;
            case 0x17: {
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
                }
                break;
            default:
                break;
            }
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
