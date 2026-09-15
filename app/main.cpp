#include "yipe/map.hpp"
#include "yipe/shop.hpp"

#include <SDL.h>

#include <cstdint>
#include <filesystem>
#include <iostream>
#include <memory>
#include <sstream>
#include <stdexcept>
#include <string>

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
    Texture terrain_133;
    Texture terrain_134;
    Texture terrain_137;
    Texture terrain_139;
    Texture terrain_141;
    Texture terrain_142;
    Texture special_377;
    Texture special_418;
    Texture warrior;
};

GameTextures load_game_textures(SDL_Renderer* renderer, const std::filesystem::path& assets) {
    return {
        load_bitmap(renderer, assets / "133-1033.bmp"),
        load_bitmap(renderer, assets / "134-1033.bmp"),
        load_bitmap(renderer, assets / "137-1033.bmp"),
        load_bitmap(renderer, assets / "139-1033.bmp"),
        load_bitmap(renderer, assets / "141-1033.bmp"),
        load_bitmap(renderer, assets / "142-1033.bmp"),
        load_bitmap(renderer, assets / "377-1033.bmp"),
        load_bitmap(renderer, assets / "418-1033.bmp"),
        load_bitmap(renderer, assets / "406-1033.bmp", true),
    };
}

struct TileGraphic {
    SDL_Texture* texture;
    SDL_Rect source;
};

TileGraphic tile_graphic(std::uint16_t tile, const GameTextures& textures) {
    switch (tile) {
    case 1:
        return {textures.terrain_137.get(), {0, 0, tile_size, tile_size}};
    case 2:
        return {textures.terrain_133.get(), {0, 0, tile_size, tile_size}};
    case 3:
        return {textures.terrain_139.get(), {0, 32, tile_size, tile_size}};
    case 5:
        return {textures.terrain_137.get(), {32, 0, tile_size, tile_size}};
    case 6:
        return {textures.terrain_137.get(), {0, 32, tile_size, tile_size}};
    case 7:
        return {textures.terrain_137.get(), {32, 32, tile_size, tile_size}};
    case 8:
        return {textures.terrain_142.get(), {32, 0, tile_size, tile_size}};
    case 10:
        return {textures.special_418.get(), {0, 0, tile_size, tile_size}};
    case 0x0b:
        return {textures.terrain_133.get(), {0, 32, tile_size, tile_size}};
    case 0x0e:
    case 0x59:
        return {textures.terrain_134.get(), {0, 32, tile_size, tile_size}};
    case 0x0f:
        return {textures.terrain_141.get(), {0, 32, tile_size, tile_size}};
    case 0x10:
        return {textures.terrain_141.get(), {32, 32, tile_size, tile_size}};
    case 0x16:
        return {textures.terrain_142.get(), {0, 0, tile_size, tile_size}};
    case 0x19:
        return {textures.terrain_134.get(), {32, 0, tile_size, tile_size}};
    case 0x1a:
        return {textures.terrain_134.get(), {32, 32, tile_size, tile_size}};
    case 0x30:
        return {textures.special_377.get(), {0, 0, tile_size, tile_size}};
    case 0x32:
        return {textures.terrain_133.get(), {32, 0, tile_size, tile_size}};
    case 0x39:
        return {textures.terrain_139.get(), {0, 0, tile_size, tile_size}};
    default:
        throw std::runtime_error("starting map contains an unmapped tile graphic");
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
    const yipe::Map map = yipe::starting_map();
    yipe::MapPosition player{5, 5};
    render_game(renderer, map, textures, player);

    while (true) {
        SDL_Event event{};
        if (SDL_WaitEvent(&event) == 0) {
            throw std::runtime_error(std::string("SDL event wait failed: ") + SDL_GetError());
        }
        if (event.type == SDL_QUIT) {
            return;
        }
        if (event.type == SDL_WINDOWEVENT && event.window.event == SDL_WINDOWEVENT_EXPOSED) {
            render_game(renderer, map, textures, player);
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
        const yipe::MovementResult result = yipe::try_move(map, player, delta_x, delta_y);
        if (result.position_changed) {
            render_game(renderer, map, textures, player);
        } else if (result.original_code == 8) {
            show_freds_shop(window);
            render_game(renderer, map, textures, player);
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
            render_game(renderer.get(), yipe::starting_map(), game_textures, {5, 5});
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
