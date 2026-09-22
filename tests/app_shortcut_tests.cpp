// Exercise the production event loop without refactoring its private helpers.
// Only the native dialogs and preference-directory boundary are substituted.
#include <SDL.h>
#define main yipe_application_main
#include "../app/main.cpp"
#undef main

#include <chrono>

namespace {
std::filesystem::path test_directory;
std::optional<std::filesystem::path> selected_save;
int save_dialogs = 0;
int open_dialogs = 0;
std::vector<std::string> messages;
}

char* SDL_GetPrefPath(const char*, const char*) {
    return SDL_strdup(test_directory.string().c_str());
}

int SDL_ShowSimpleMessageBox(Uint32, const char* title, const char*, SDL_Window*) {
    messages.emplace_back(title);
    return 0;
}

int SDL_ShowMessageBox(const SDL_MessageBoxData* data, int* selected) {
    if (std::string_view(data->title) == "Quit Game") {
        for (int i = 0; i < data->numbuttons; ++i) {
            if (std::string_view(data->buttons[i].text) == "Leave Without Saving") {
                *selected = data->buttons[i].buttonid;
                return 0;
            }
        }
    }
    return SDL_SetError("unexpected choice dialog: %s", data->title);
}

namespace yipe_app {
std::optional<std::filesystem::path> choose_open_game_file(
    const std::filesystem::path&) {
    ++open_dialogs;
    return std::nullopt;
}

std::optional<std::filesystem::path> choose_save_game_file(
    const std::filesystem::path&) {
    ++save_dialogs;
    return selected_save;
}
}

int main(int argc, char* argv[]) {
    static_cast<void>(argc);
    try {
        test_directory = std::filesystem::temp_directory_path()
            / ("yipe-shortcuts-" + std::to_string(
                std::chrono::steady_clock::now().time_since_epoch().count()));
        if (!std::filesystem::create_directory(test_directory)) {
            throw std::runtime_error("could not create isolated test directory");
        }
        const SdlSession sdl;
        const RuntimeAssets assets = runtime_assets(argv[0]);
        Window window(SDL_CreateWindow("Shortcut tests", 0, 0, map_width,
                                       map_height, SDL_WINDOW_HIDDEN), SDL_DestroyWindow);
        Renderer renderer(SDL_CreateRenderer(window.get(), -1, SDL_RENDERER_SOFTWARE),
                          SDL_DestroyRenderer);
        if (!window || !renderer) {
            throw std::runtime_error(SDL_GetError());
        }
        const GameTextures textures = load_game_textures(renderer.get(), assets.bitmaps);
        AudioPlayer audio(assets);
        yipe::GameSettings settings;
        settings.sound_enabled = false;
        auto scores = yipe::default_high_scores();
        int cases = 0;
        const auto check = [&](SDL_Keycode key, int modifiers, bool repeat,
                               std::string_view expected, bool cancel = false) {
            ++cases;
            const auto original = test_directory / (std::to_string(cases) + ".y3r");
            const auto alternate = test_directory / (std::to_string(cases) + "-as.y3r");
            auto save_path = original;
            selected_save = cancel ? std::nullopt
                                   : std::optional<std::filesystem::path>{alternate};
            save_dialogs = open_dialogs = 0;
            messages.clear();
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            SDL_Event command{};
            command.type = SDL_KEYDOWN;
            command.key.keysym.sym = key;
            command.key.keysym.mod = static_cast<Uint16>(modifiers);
            command.key.repeat = repeat ? 1 : 0;
            SDL_Event quit{};
            quit.type = SDL_KEYDOWN;
            quit.key.keysym.sym = SDLK_q;
            if (SDL_PushEvent(&command) != 1 || SDL_PushEvent(&quit) != 1) {
                throw std::runtime_error(SDL_GetError());
            }
            yipe::GameState game;
            game.player.name = "ShortcutTest";
            const auto result = run_game(
                window.get(), renderer.get(), textures, game, save_path,
                test_directory / "settings.y3r", test_directory / "highscores.y3r",
                scores, settings, audio);
            const bool saved = expected == "save" || (expected == "save_as" && !cancel);
            const auto expected_path = expected == "save_as" && !cancel ? alternate : original;
            std::vector<std::string> expected_messages;
            if (saved) expected_messages.emplace_back("Game Saved");
            if (expected == "other") expected_messages.emplace_back("Other");
            if (result.choice != StartupChoice::quit
                || save_dialogs != (expected == "save_as" ? 1 : 0)
                || open_dialogs != (expected == "open" ? 1 : 0)
                || messages != expected_messages || save_path != expected_path
                || std::filesystem::exists(expected_path) != saved
                || (saved && yipe::load_game(expected_path).player.name != "ShortcutTest")
                || (expected_path == alternate && std::filesystem::exists(original))) {
                throw std::runtime_error("shortcut case " + std::to_string(cases)
                    + " failed: key=" + std::to_string(key)
                    + " modifiers=" + std::to_string(modifiers)
                    + " expected=" + std::string(expected));
            }
        };
        for (int control : {KMOD_LCTRL, KMOD_RCTRL, KMOD_CTRL}) {
            for (int locks : {0, KMOD_CAPS | KMOD_NUM}) {
                check(SDLK_s, control | locks, false, "save");
                check(SDLK_s, control | locks, true, "none");
                check(SDLK_o, control | locks, false, "open");
                for (int shift : {KMOD_LSHIFT, KMOD_RSHIFT, KMOD_SHIFT}) {
                    const int modifiers = control | shift | locks;
                    check(SDLK_s, modifiers, false, "save_as");
                    check(SDLK_s, modifiers, false, "save_as", true);
                    check(SDLK_s, modifiers, true, "none");
                    check(SDLK_o, modifiers, false, "other");
                    check(SDLK_o, modifiers, true, "none");
                }
            }
        }
        for (int modifiers : {KMOD_NONE, KMOD_LSHIFT, KMOD_RSHIFT}) {
            check(SDLK_s, modifiers, false, "none");
            check(SDLK_o, modifiers, false, "none");
        }
        check(SDLK_F5, KMOD_NONE, false, "save");
        std::filesystem::remove_all(test_directory);
        std::cout << cases << " shortcut event-loop cases passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
