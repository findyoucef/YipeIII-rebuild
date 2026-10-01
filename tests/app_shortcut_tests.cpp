// Exercise the production event loop without refactoring its private helpers.
// Native dialogs, preference-directory lookup, and scripted SDL input/time are substituted.
#include <SDL.h>
#include "yipe/encounter.hpp"
#include "yipe/spell.hpp"
namespace {
bool scripted_clock = false;
Uint64 scripted_now = 0;
int pending_ticks = 0;
bool delay_settings_dialog = false;
bool delay_status_dialog = false;
bool delay_score_code_dialog = false;
int clipboard_failures = 0;
bool fail_printing = false;
int print_dialogs = 0;
int page_setup_dialogs = 0;
std::vector<unsigned char> printed_pixels;
bool route_combat = false;
bool full_game_mode = false;
bool full_game_variant = false;
bool full_game_resuming = false;
bool full_game_continuing = false;
bool full_game_live = false;
void full_game_input(SDL_Event* event);
void full_game_log(const std::string& text);
int route_combat_actions = 0;
const yipe::Player* route_player = nullptr;
std::optional<std::size_t> route_spell;
yipe::SpellPower route_spell_power = yipe::SpellPower::low;
std::size_t route_attack_spell = 3;
bool route_attack_spell_enabled = true;
yipe::SpellPower route_attack_power = yipe::SpellPower::low;
bool route_healing_item = false;
bool route_flee = false;
int route_flee_above_health = 0;
int route_target_steps = 0;
int route_pending_target_steps = 0;
std::optional<std::size_t> route_opening_spell;
bool route_opening_spell_pending = false;
yipe::SpellPower route_opening_power = yipe::SpellPower::low;
std::size_t route_heal_spell = 5;
yipe::SpellPower route_heal_power = yipe::SpellPower::low;
bool route_food_before_small_heal = false;
bool route_use_mana_potions = false;
bool route_mana_item = false;
int route_dialog_pages = 0;
std::vector<SDL_Event> text_input_events;
std::size_t text_event_index = 0;

// The opt-in progression proof checks game state, not pixels. Avoid rasterizing
// millions of hidden rest frames; normal regression runs retain real rendering.
bool omit_proof_pixels() { return full_game_mode && full_game_continuing; }
int test_render_copy(SDL_Renderer* renderer, SDL_Texture* texture,
                     const SDL_Rect* source, const SDL_Rect* destination) {
    return omit_proof_pixels() ? 0 : SDL_RenderCopy(renderer, texture, source, destination);
}
int test_render_clear(SDL_Renderer* renderer) {
    return omit_proof_pixels() ? 0 : SDL_RenderClear(renderer);
}
int test_render_fill_rect(SDL_Renderer* renderer, const SDL_Rect* rectangle) {
    return omit_proof_pixels() ? 0 : SDL_RenderFillRect(renderer, rectangle);
}
int test_render_draw_rect(SDL_Renderer* renderer, const SDL_Rect* rectangle) {
    return omit_proof_pixels() ? 0 : SDL_RenderDrawRect(renderer, rectangle);
}
void test_render_present(SDL_Renderer* renderer) {
    if (!omit_proof_pixels()) SDL_RenderPresent(renderer);
}

int test_set_clipboard(const char* text) {
    if (clipboard_failures > 0) {
        --clipboard_failures;
        return SDL_SetError("clipboard unavailable");
    }
    return SDL_SetClipboardText(text);
}

int test_wait_event_blocking(SDL_Event* event) {
    if (!text_input_events.empty()) {
        if (text_event_index == text_input_events.size()) {
            if (full_game_live) {
                text_input_events.clear();
                text_event_index = 0;
                return test_wait_event_blocking(event);
            }
            SDL_SetError("text input exhausted");
            return 0;
        }
        *event = text_input_events[text_event_index++];
        return 1;
    }
    if (!route_combat) return SDL_WaitEvent(event);
    if (++route_combat_actions > (full_game_mode ? 100000 : 2000)) {
        SDL_SetError("fresh route exceeded its combat budget");
        return 0;
    }
    if (!route_player) {
        SDL_SetError("fresh route has no active combat player");
        return 0;
    }
    route_spell.reset();
    route_spell_power = yipe::SpellPower::low;
    route_healing_item = false;
    route_mana_item = false;
    route_dialog_pages = 0;
    *event = {};
    event->type = SDL_KEYDOWN;
    if (route_flee) {
        event->key.keysym.sym = SDLK_r;
        return 1;
    }
    if (route_pending_target_steps > 0) {
        --route_pending_target_steps;
        event->key.keysym.sym = SDLK_RIGHT;
        return 1;
    }
    // Backstab costs three AP and avoids a counterattack on its turn.
    if (route_player->special_ability == yipe::SpecialAbility::backstab
        && route_player->ability_points >= 3) {
        event->key.keysym.sym = SDLK_s;
        return 1;
    }
    if (route_opening_spell_pending) {
        route_opening_spell_pending = false;
        if (route_opening_spell
            && route_player->spells[*route_opening_spell] != 0
            && route_player->spell_points >= yipe::spell_point_cost(
                *route_player, *route_opening_spell, route_opening_power)) {
            route_spell = route_opening_spell;
            route_spell_power = route_opening_power;
            event->key.keysym.sym = SDLK_c;
            return 1;
        }
    }
    // Before learning Small Heal, the opening fights use three scripted food choices.
    event->key.keysym.sym = !full_game_variant && (route_combat_actions == 4
        || route_combat_actions == 11 || route_combat_actions == 58)
        ? SDLK_i : SDLK_RETURN;
    if ((route_player->spells[1] != 0 || full_game_variant)
        && route_player->health <= (route_use_mana_potions
            ? route_player->maximum_health * 2 / 3 : route_player->maximum_health / 2)) {
        if (full_game_live && route_player->spells[route_heal_spell] != 0
            && route_player->spell_points >= yipe::spell_point_cost(
                *route_player, route_heal_spell, route_heal_power)) {
            route_spell = route_heal_spell;
            route_spell_power = route_heal_power;
        } else if (!route_food_before_small_heal && route_player->spells[1] != 0
            && route_player->spell_points >= yipe::spell_point_cost(
                *route_player, 1, yipe::SpellPower::low)) {
            route_spell = 1;
        } else {
            for (std::size_t slot = 0; slot < yipe::Player::carried_inventory_size; ++slot) {
                if (route_player->inventory[slot] == 1 || route_player->inventory[slot] == 2) {
                    route_healing_item = true;
                    event->key.keysym.sym = SDLK_i;
                    break;
                }
            }
            if (!route_healing_item && route_player->spells[1] != 0
                && route_player->spell_points >= yipe::spell_point_cost(
                    *route_player, 1, yipe::SpellPower::low)) {
                route_spell = 1;
            }
        }
    }
    if (full_game_live && !route_attack_spell_enabled) {
        if (route_spell) event->key.keysym.sym = SDLK_c;
        return 1;
    }
    // Focus one opponent with low-power Insect Attack after buying it at level seven.
    const auto attack_spell = full_game_live ? route_attack_spell : 3;
    const auto attack_power = full_game_live ? route_attack_power : yipe::SpellPower::low;
    if (route_use_mana_potions && !route_spell && !route_healing_item
        && route_player->spell_points < yipe::spell_point_cost(
            *route_player, attack_spell, attack_power) + yipe::spell_point_cost(
                *route_player, route_heal_spell, route_heal_power)
        && std::find(route_player->inventory.begin(), route_player->inventory.begin()
            + static_cast<std::ptrdiff_t>(yipe::Player::carried_inventory_size), 3)
                != route_player->inventory.begin()
                    + static_cast<std::ptrdiff_t>(yipe::Player::carried_inventory_size)) {
        route_mana_item = true;
        event->key.keysym.sym = SDLK_i;
        return 1;
    }
    if (!route_spell && !route_healing_item
        && route_player->spells[attack_spell] != 0
        && route_player->spell_points >= yipe::spell_point_cost(
            *route_player, attack_spell, attack_power)) {
        route_spell = attack_spell;
        route_spell_power = attack_power;
    }
    if (full_game_live && !route_spell && !route_healing_item
        && attack_spell > 3 && route_player->spells[3] != 0
        && route_player->spell_points >= yipe::spell_point_cost(
            *route_player, 3, yipe::SpellPower::low)) {
        route_spell = 3;
    }
    if (route_spell) event->key.keysym.sym = SDLK_c;
    return 1;
}

Uint64 test_ticks() {
    return scripted_clock ? scripted_now : SDL_GetTicks64();
}

int test_wait_event(SDL_Event* event, int timeout) {
    if (!scripted_clock) return SDL_WaitEventTimeout(event, timeout);
    if (pending_ticks == 0) {
        if (SDL_PollEvent(event) == 0) {
            if (full_game_continuing) full_game_input(event);
            else {
                SDL_SetError("scripted exploration input exhausted");
                return 0;
            }
        }
        if (event->type != SDL_USEREVENT) return 1;
        pending_ticks = event->user.code;
    }
    scripted_now += static_cast<Uint64>(timeout);
    --pending_ticks;
    return 0;
}
}
namespace yipe {
CombatEncounter observe_route_encounter(const MonsterPlacement& placement, Player& player,
                                        MsvcRandom& random, int environment) {
    // Read player state to choose input; leave encounter setup and RNG to production.
    if (route_combat) route_player = &player;
    auto encounter = start_monster_encounter(placement, player, random, environment);
    if (full_game_live) {
        if (route_flee_above_health > 0) {
            route_flee = std::any_of(encounter.monsters.begin(), encounter.monsters.end(),
                [](const auto& monster) {
                    return monster.type_id != 0 && monster.health > route_flee_above_health;
                });
        }
        route_pending_target_steps = route_target_steps;
        route_opening_spell_pending = route_opening_spell.has_value();
        std::string description = "ENCOUNTER";
        for (const auto& monster : encounter.monsters) {
            if (monster.type_id != 0) description += " | " + std::string(monster.name) + " HP "
                + std::to_string(monster.health);
        }
        full_game_log(description);
    }
    return encounter;
}
}
#define SDL_GetTicks64 test_ticks
#define SDL_WaitEventTimeout test_wait_event
#define SDL_WaitEvent test_wait_event_blocking
#define start_monster_encounter observe_route_encounter
#define SDL_SetClipboardText test_set_clipboard
#define SDL_RenderCopy test_render_copy
#define SDL_RenderClear test_render_clear
#define SDL_RenderFillRect test_render_fill_rect
#define SDL_RenderDrawRect test_render_draw_rect
#define SDL_RenderPresent test_render_present
#define main yipe_application_main
#include "../app/main.cpp"
#undef main
#undef SDL_WaitEventTimeout
#undef SDL_WaitEvent
#undef start_monster_encounter
#undef SDL_SetClipboardText
#undef SDL_RenderCopy
#undef SDL_RenderClear
#undef SDL_RenderFillRect
#undef SDL_RenderDrawRect
#undef SDL_RenderPresent
#undef SDL_GetTicks64

#include <chrono>
#include <deque>
#include <fstream>
#include <iterator>
#include <tuple>

namespace {
std::filesystem::path test_directory;
std::optional<std::filesystem::path> selected_save;
std::deque<std::optional<std::filesystem::path>> save_choices;
std::optional<std::filesystem::path> selected_open;
std::vector<std::filesystem::path> recent_files;
std::vector<std::filesystem::path> remembered_files;
int save_dialogs = 0;
int open_dialogs = 0;
std::vector<std::string> messages;
std::vector<std::string> message_texts;
std::vector<int> high_score_button_counts;
std::string score_code_message;
struct DialogChoice {
    std::string_view title;
    int button;
    std::string text;
};
std::deque<DialogChoice> dialog_choices;
std::vector<yipe::GameState> route_checkpoints;
}

#include "full_game_driver.hpp"

char* SDL_GetPrefPath(const char*, const char*) {
    return SDL_strdup(test_directory.string().c_str());
}

int SDL_ShowSimpleMessageBox(Uint32, const char* title, const char* text, SDL_Window*) {
    if (scripted_clock && (std::string_view(title) == "Save Game Failed"
            || std::string_view(title) == "High Scores Save Failed")) {
        scripted_now += 100000;
    }
    if (scripted_clock && delay_status_dialog
        && std::string_view(title) == "Player Status Details") scripted_now += 100000;
    if (route_combat) {
        if (std::string_view(title) == "Defeat" && !full_game_live) {
            return SDL_SetError("fresh route died at combat input %d: %s", route_combat_actions, text);
        }
        if (std::string_view(title) == "Victory") {
            route_spell.reset();
            route_healing_item = false;
        }
        if (std::string_view(title) == "Game Saved") {
            route_checkpoints.push_back(yipe::load_game(test_directory / "fresh-route.y3r"));
            if (full_game_live) full_game_state(route_checkpoints.back());
        }
    }
    messages.emplace_back(title);
    message_texts.emplace_back(text);
    if (full_game_live && std::string_view(title) != "Game Saved") {
        full_game_log(std::string("MESSAGE ") + title + "\n" + text);
    }
    return 0;
}

int SDL_ShowMessageBox(const SDL_MessageBoxData* data, int* selected) {
    if (scripted_clock && (std::string_view(data->title) == "Recent Games"
            || std::string_view(data->title) == "Profile / Registration")) scripted_now += 100000;
    if (std::string_view(data->title) == "High Scores") {
        high_score_button_counts.push_back(data->numbuttons);
    }
    if (std::string_view(data->title) == "High Score Code") {
        score_code_message = data->message;
        if (scripted_clock && delay_score_code_dialog) scripted_now += 100000;
    }
    if (route_combat && route_spell && std::string_view(data->title) == "Spells") {
        if (++route_dialog_pages > static_cast<int>(yipe::Player::spell_count)) {
            return SDL_SetError("fresh route could not select its combat spell");
        }
        const auto text = std::string_view(data->message);
        *selected = text.substr(0, text.find('\n')) == yipe::spell_definitions()[*route_spell].name
            ? (route_spell_power == yipe::SpellPower::high ? 2 : 1) : 3;
        return 0;
    }
    if (route_combat && (route_healing_item || route_mana_item)
        && std::string_view(data->title) == "Inventory") {
        if (++route_dialog_pages > static_cast<int>(yipe::Player::carried_inventory_size)) {
            return SDL_SetError("fresh route could not select its combat food");
        }
        const auto text = std::string_view(data->message);
        const bool matches = route_mana_item ? text.starts_with("Spell Point Potion\n")
            : text.starts_with("French Fries\n") || text.starts_with("Juicy Cheeseburger\n");
        *selected = matches ? 1 : 2;
        return 0;
    }
    if (std::string_view(data->title) == "Settings") {
        if (scripted_clock && delay_settings_dialog) scripted_now += 100000;
        const bool has_player = std::string_view(data->message).find("Equipment names:")
            != std::string_view::npos;
        if (data->numbuttons != (has_player ? 7 : 6)) {
            return SDL_SetError("mode toggle must only be offered for an active character");
        }
    }
    if (!dialog_choices.empty()) {
        const auto expected = dialog_choices.front();
        if (data->title != expected.title
            || std::string_view(data->message).find(expected.text) == std::string_view::npos) {
            return SDL_SetError("unexpected dialog or text: %s: %s", data->title, data->message);
        }
        if (expected.button == -1) {
            *selected = -1;
            dialog_choices.pop_front();
            return 0;
        }
        for (int i = 0; i < data->numbuttons; ++i) {
            if (data->buttons[i].buttonid == expected.button) {
                *selected = expected.button;
                dialog_choices.pop_front();
                return 0;
            }
        }
        return SDL_SetError("missing expected button in %s", data->title);
    }
    if (std::string_view(data->title) == "Quit Game") {
        for (int i = 0; i < data->numbuttons; ++i) {
            if (std::string_view(data->buttons[i].text) == "Leave Without Saving") {
                *selected = data->buttons[i].buttonid;
                return 0;
            }
        }
    }
    if (full_game_live) return full_game_choice(data, selected);
    return SDL_SetError("unexpected choice dialog: %s", data->title);
}

namespace yipe_app {

std::vector<std::filesystem::path> recent_game_files() { return recent_files; }
void remember_game_file(const std::filesystem::path& path) { remembered_files.push_back(path); }
void clear_recent_game_files() { recent_files.clear(); }
void show_print_dialog(const SDL_Surface& image) {
    ++print_dialogs;
    if (fail_printing) throw std::runtime_error("printing unavailable");
    if (image.w != map_width || image.h != map_height
        || image.format->format != SDL_PIXELFORMAT_RGBA32) {
        throw std::runtime_error("printing captured the window instead of the logical map");
    }
    printed_pixels.clear();
    for (int row = 0; row < image.h; ++row) {
        const auto* pixels = static_cast<const unsigned char*>(image.pixels) + row * image.pitch;
        printed_pixels.insert(printed_pixels.end(), pixels, pixels + image.w * 4);
    }
    if (scripted_clock) scripted_now += 100000;
}

void show_page_setup() {
    ++page_setup_dialogs;
    if (fail_printing) throw std::runtime_error("page setup unavailable");
    if (scripted_clock) scripted_now += 100000;
}

std::optional<std::filesystem::path> choose_open_game_file(
    const std::filesystem::path&) {
    ++open_dialogs;
    return selected_open;
}

std::optional<std::filesystem::path> choose_save_game_file(
    const std::filesystem::path&) {
    ++save_dialogs;
    if (!save_choices.empty()) {
        auto selected = save_choices.front();
        save_choices.pop_front();
        return selected;
    }
    return selected_save;
}
}

int main(int argc, char* argv[]) {
    const bool checkpoint_mode = argc >= 2
        && (std::string_view(argv[1]) == "--checkpoint"
            || std::string_view(argv[1]) == "--checkpoint-variant");
    full_game_mode = argc == 2
        && (std::string_view(argv[1]) == "--full-game" || checkpoint_mode);
    full_game_variant = argc >= 2
        && (std::string_view(argv[1]) == "--full-game-variant"
            || std::string_view(argv[1]) == "--checkpoint-variant");
    full_game_resuming = argc >= 2 && std::string_view(argv[1]) == "--resume-game";
    try {
        int variant_type = 1;
        int variant_difficulty = 2;
        std::uint32_t variant_seed = 1;
        if (full_game_variant) {
            if (argc != 5) throw std::runtime_error("usage: --full-game-variant TYPE DIFFICULTY SEED");
            variant_type = std::stoi(argv[2]);
            variant_difficulty = std::stoi(argv[3]);
            const auto seed = std::stoull(argv[4]);
            if (variant_type < 1 || variant_type > 4 || variant_difficulty < 1
                || variant_difficulty > 3 || seed > std::numeric_limits<std::uint32_t>::max()) {
                throw std::runtime_error("invalid full-game character, difficulty, or seed");
            }
            variant_seed = static_cast<std::uint32_t>(seed);
            full_game_mode = true;
        }
        if (full_game_resuming) {
            if (argc != 3) throw std::runtime_error("usage: --resume-game SAVE.y3r");
            full_game_mode = true;
        }
        if (argc != 1 && !full_game_mode) throw std::runtime_error("unknown test option");
        test_directory = std::filesystem::temp_directory_path()
            / ("yipe-shortcuts-" + std::to_string(
                std::chrono::steady_clock::now().time_since_epoch().count()));
        if (!std::filesystem::create_directory(test_directory)) {
            throw std::runtime_error("could not create isolated test directory");
        }
        if (full_game_mode) std::cerr << "FULL_GAME_DIRECTORY=" << test_directory << '\n';
        const SdlSession sdl;
        // Existing pixel baselines exercise the hidden-bar layout. Dedicated
        // cases below cover the default visible layout and both toggle states.
        if (!status_bar_visible) throw std::runtime_error("status bar must start visible");
        status_bar_visible = false;
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

        const auto require = [](bool condition, const char* message) {
            if (!condition) throw std::runtime_error(message);
        };
        const auto push_key = [](SDL_Keycode key, bool repeat = false, int modifiers = 0) {
            SDL_Event event{};
            event.type = SDL_KEYDOWN;
            event.key.keysym.sym = key;
            event.key.keysym.mod = static_cast<Uint16>(modifiers);
            event.key.repeat = repeat ? 1 : 0;
            if (SDL_PushEvent(&event) != 1) throw std::runtime_error(SDL_GetError());
        };
        const auto push_mouse = [](int x, int y, Uint8 button = SDL_BUTTON_LEFT,
                                    Uint32 type = SDL_MOUSEBUTTONUP) {
            SDL_Event event{};
            event.type = type;
            event.button.button = button;
            // SDL delivers logical coordinates to the game. Synthetic events
            // have no window ID so SDL does not scale them a second time.
            event.button.x = x;
            event.button.y = y;
            if (SDL_PushEvent(&event) != 1) throw std::runtime_error(SDL_GetError());
        };
        if (full_game_resuming) {
            const auto path = test_directory / "fresh-route.y3r";
            std::filesystem::copy_file(argv[2], path);
            selected_open = selected_save = path;
            const auto opened = choose_game_to_open(test_directory);
            require(opened && opened->choice == StartupChoice::load_game && opened->opened_game,
                    "resume did not use the production Open path");
            messages.clear();
            message_texts.clear();
            route_combat = true;
            scripted_clock = true;
            full_game_continuing = true;
            status_bar_visible = true;
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(SDLK_F5);
            auto save_path = opened->opened_save_path;
            const auto exit = run_game(window.get(), renderer.get(), textures,
                *opened->opened_game, save_path, test_directory / "settings.y3r",
                test_directory / "highscores.y3r", scores, settings, audio);
            require(exit.choice == StartupChoice::quit && full_game_live,
                    "resumed game did not save and explicitly quit");
            full_game_state(yipe::load_game(save_path));
            full_game_log("PASS: production native Open, resumed inputs, save and quit verified.");
            return 0;
        }
        if (full_game_variant) {
            prepare_startup_view(window.get(), renderer.get());
            dialog_choices = {{"Create Character", variant_type, ""},
                              {"Character Attributes", 1, "Stat limit 50"},
                              {"Difficulty", variant_difficulty, ""}, {"Show Intro", 0, ""}};
            push_key(SDLK_RETURN); // Accept the default name.
            push_key(SDLK_1);
            push_key(SDLK_RETURN); // Accept the first normal portrait.
            yipe::MsvcRandom creation_random(variant_seed);
            const auto player = choose_character(window.get(), renderer.get(), textures, creation_random);
            constexpr std::array specialties{3, 0, 1, 2};
            require(player && player->specialty == specialties[static_cast<std::size_t>(variant_type - 1)]
                        && player->difficulty == variant_difficulty && dialog_choices.empty(),
                    "variant did not pass through production character creation");
            yipe::GameState game;
            game.player = *player;
            game.random_state = creation_random.state();
            require(game.player.level == 1 && game.player.gold == 0 && game.player.experience == 0
                        && game.player.rescued_princess_count() == 0 && game.player.stamps_found == 0
                        && !game.player.has_rainbow_key && !game.player.has_underworld_key
                        && !game.player.has_waterproof_shoe_spray && !game.player.has_tree_climbing_shoes,
                    "variant did not start with a fresh unfunded character");
            std::cerr << "FULL_GAME_VARIANT type=" << variant_type << " difficulty="
                      << variant_difficulty << " seed=" << variant_seed << '\n';
            messages.clear();
            message_texts.clear();
            selected_save = test_directory / "fresh-route.y3r";
            auto path = *selected_save;
            route_combat = true;
            scripted_clock = true;
            full_game_continuing = true;
            status_bar_visible = true;
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(SDLK_F5);
            const auto exit = run_game(window.get(), renderer.get(), textures, game, path,
                test_directory / "settings.y3r", test_directory / "highscores.y3r", scores, settings, audio);
            require(exit.choice == StartupChoice::quit && full_game_live,
                    "variant exited before its input-only continuation");
            const auto final_game = yipe::load_game(path);
            if (!checkpoint_mode) {
                check_full_game_completion(final_game, player->specialty, variant_difficulty);
            }
            full_game_state(final_game);
            full_game_log(checkpoint_mode
                ? "PASS: earned fresh-character checkpoint saved and explicitly quit; completion not asserted."
                : "PASS: fresh character variant earned every rescue, stamp, prerequisite, and the finale.");
            return 0;
        }
        prepare_game_view(window.get(), renderer.get());
        struct KeyCase { SDL_Keycode sdl; std::uint16_t code; const char* name; };
        const std::array<KeyCase, 17> extra_keys{{
            {SDLK_SEMICOLON, 0xba, "Semicolon"}, {SDLK_EQUALS, 0xbb, "Equals"},
            {SDLK_COMMA, 0xbc, "Comma"}, {SDLK_MINUS, 0xbd, "Minus"},
            {SDLK_PERIOD, 0xbe, "Period"}, {SDLK_SLASH, 0xbf, "Slash"},
            {SDLK_BACKQUOTE, 0xc0, "Backquote"}, {SDLK_LEFTBRACKET, 0xdb, "Left Bracket"},
            {SDLK_BACKSLASH, 0xdc, "Backslash"}, {SDLK_RIGHTBRACKET, 0xdd, "Right Bracket"},
            {SDLK_QUOTE, 0xde, "Quote"}, {SDLK_KP_MULTIPLY, 0x6a, "Numpad Multiply"},
            {SDLK_KP_PLUS, 0x6b, "Numpad Add"}, {SDLK_SEPARATOR, 0x6c, "Numpad Separator"},
            {SDLK_KP_MINUS, 0x6d, "Numpad Subtract"}, {SDLK_KP_PERIOD, 0x6e, "Numpad Decimal"},
            {SDLK_KP_DIVIDE, 0x6f, "Numpad Divide"},
        }};
        const std::string keys_title = SDL_GetWindowTitle(window.get());
        require(key_binding_warnings(settings).empty(), "default bindings have false conflicts");
        for (const auto& key : extra_keys) {
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(key.sdl);
            require(portable_key_code(key.sdl) == key.code
                        && yipe::key_binding_name(key.code) == key.name
                        && capture_key_binding(window.get(), yipe::InputAction::north) == key.code
                        && SDL_GetWindowTitle(window.get()) == keys_title,
                    "punctuation/keypad binding lost its code, label, capture, or window title");
            ++cases;
        }
        for (int modifiers : {KMOD_LSHIFT, KMOD_RSHIFT, KMOD_LCTRL, KMOD_RCTRL,
                               KMOD_LALT, KMOD_RALT, KMOD_LGUI, KMOD_RGUI, KMOD_MODE}) {
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            messages.clear();
            push_key(SDLK_LSHIFT);
            push_key(SDLK_CAPSLOCK);
            push_key(SDLK_x, false, modifiers);
            push_key(SDLK_F2, false, KMOD_CAPS | KMOD_NUM);
            require(capture_key_binding(window.get(), yipe::InputAction::north) == 0x71
                        && messages == std::vector<std::string>{"Edit Keys", "Single Keys Only"}
                        && SDL_GetWindowTitle(window.get()) == keys_title,
                    "key capture stored a modifier combination or rejected lock keys");
            ++cases;
        }
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        messages.clear();
        push_key(SDLK_x, true);
        push_key(SDLK_F13);
        push_key(SDLK_PERIOD);
        require(capture_key_binding(window.get(), yipe::InputAction::north) == 0xbe
                    && messages == std::vector<std::string>{"Edit Keys", "Edit Keys"},
                "unsupported/repeated key capture did not retry explicitly");
        ++cases;
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        push_key(SDLK_ESCAPE);
        require(!capture_key_binding(window.get(), yipe::InputAction::north)
                    && SDL_GetWindowTitle(window.get()) == keys_title,
                "canceling capture changed its binding or window title");
        ++cases;
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        SDL_Event close_capture{};
        close_capture.type = SDL_WINDOWEVENT;
        close_capture.window.windowID = SDL_GetWindowID(window.get());
        close_capture.window.event = SDL_WINDOWEVENT_CLOSE;
        require(SDL_PushEvent(&close_capture) == 1, "could not queue capture window close");
        push_key(SDLK_RETURN);
        require(!capture_key_binding(window.get(), yipe::InputAction::north)
                    && SDL_HasEvent(SDL_QUIT) && SDL_HasEvent(SDL_KEYDOWN)
                    && SDL_GetWindowTitle(window.get()) == keys_title,
                "key capture ignored window close or failed to restore its title");
        ++cases;
        for (std::size_t index = 0; index < yipe::input_action_count; ++index) {
            auto edited = settings;
            const auto path = test_directory / ("edit-key-" + std::to_string(index) + ".y3r");
            dialog_choices = {{"Settings", index < 8 ? 2 : 3, "single keys"},
                              {index < 8 ? "Movement Keys" : "Combat Keys", static_cast<int>(index + 1), ""},
                              {"Settings", 0, extra_keys[index].name}};
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(extra_keys[index].sdl);
            show_settings(window.get(), edited, path, audio);
            auto expected = settings;
            expected.keys[index] = extra_keys[index].code;
            require(edited == expected && yipe::load_settings(path) == expected
                        && dialog_choices.empty() && key_binding_warnings(edited).empty(),
                    "an edited key was not named, saved, or retained independently");
            ++cases;
        }
        struct ConflictCase { yipe::InputAction action; SDL_Keycode key; const char* warning; };
        const std::array<ConflictCase, 12> conflicts{{
            {yipe::InputAction::northwest, SDLK_UP, "invokes North during exploration"},
            {yipe::InputAction::north, SDLK_KP_9, "replaces the fixed/convenience Northeast"},
            {yipe::InputAction::east, SDLK_q, "reserved for Quit"},
            {yipe::InputAction::east, SDLK_F1, "reserved for Help"},
            {yipe::InputAction::east, SDLK_F5, "reserved for Save"},
            {yipe::InputAction::east, SDLK_F10, "reserved for Settings"},
            {yipe::InputAction::attack, SDLK_r, "invokes Run in combat"},
            {yipe::InputAction::cast, SDLK_i, "invokes Use Item during exploration"},
            {yipe::InputAction::use_item, SDLK_w, "invokes North during exploration"},
            {yipe::InputAction::attack, SDLK_LEFT, "replaces target cycling"},
            {yipe::InputAction::cast, SDLK_F1, "reserved for Help"},
            {yipe::InputAction::run, SDLK_RETURN, "invokes Run in combat"},
        }};
        for (const auto& conflict : conflicts) {
            for (bool accept : {false, true}) {
                const auto index = static_cast<std::size_t>(conflict.action);
                auto edited = settings;
                const auto path = test_directory / ("key-conflict-" + std::to_string(cases) + ".y3r");
                dialog_choices = {{"Settings", index < 8 ? 2 : 3, ""},
                                  {index < 8 ? "Movement Keys" : "Combat Keys", static_cast<int>(index + 1), ""},
                                  {"Key Conflicts", accept ? 1 : 0, conflict.warning},
                                  {"Settings", 0, accept ? conflict.warning : "Sound:"}};
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                push_key(conflict.key);
                show_settings(window.get(), edited, path, audio);
                auto expected = settings;
                if (accept) expected.keys[index] = *portable_key_code(conflict.key);
                require(edited == expected && dialog_choices.empty()
                            && std::filesystem::exists(path) == accept
                            && (!accept || yipe::load_settings(path) == expected),
                        "key conflict confirmation lost a binding or saved a canceled change");
                ++cases;
            }
        }
        auto unknown_keys = settings;
        unknown_keys.keys[0] = 0x1234;
        require(key_binding_warnings(unknown_keys).find("unsupported key code 4660") != std::string::npos,
                "existing unsupported key settings were silently presented as usable");
        const auto quit_settings_path = test_directory / "key-quit.y3r";
        auto quit_settings = settings;
        dialog_choices = {{"Settings", 2, ""}, {"Movement Keys", 2, ""},
                          {"Settings", 3, "Semicolon"}, {"Combat Keys", 9, ""}};
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        push_key(SDLK_SEMICOLON);
        SDL_Event quit_capture{};
        quit_capture.type = SDL_QUIT;
        require(SDL_PushEvent(&quit_capture) == 1, "could not queue key capture quit");
        show_settings(window.get(), quit_settings, quit_settings_path, audio);
        require(SDL_HasEvent(SDL_QUIT) && dialog_choices.empty()
                    && quit_settings.keys[1] == 0xba && quit_settings.keys[8] == 'A'
                    && yipe::load_settings(quit_settings_path) == quit_settings
                    && SDL_GetWindowTitle(window.get()) == keys_title,
                "closing capture swallowed quit or lost an earlier settings edit");
        ++cases;
        for (std::size_t index = 8; index < yipe::input_action_count; ++index) {
            auto combat_settings = settings;
            combat_settings.keys[index] = 0xba;
            for (bool repeat : {false, true}) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                push_key(SDLK_SEMICOLON, repeat);
                push_key(SDLK_RETURN);
                require(choose_combat_action(renderer.get(), "Configured keys", combat_settings)
                            == (repeat ? 1 : combat_action_id(static_cast<yipe::InputAction>(index))),
                        "combat ignored a configured punctuation key or accepted a repeat");
                ++cases;
            }
        }
        for (bool silly : {false, true}) {
            for (bool repeat : {false, true}) {
                ++cases;
                yipe::GameState game;
                game.player.silly_mode = silly;
                game.player.inventory[28] = 7;
                auto save_path = test_directory / ("mode-" + std::to_string(cases) + ".y3r");
                const bool expected_mode = repeat ? silly : !silly;
                if (!repeat) {
                    dialog_choices = {
                        {"Settings", 5, silly ? "Equipment names: Silly" : "Equipment names: Normal"},
                        {"Settings", 0, expected_mode ? "Equipment names: Silly" : "Equipment names: Normal"},
                    };
                }
                messages.clear();
                message_texts.clear();
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                push_key(SDLK_F10, repeat);
                push_key(SDLK_F5);
                push_key(SDLK_q);
                const auto result = run_game(
                    window.get(), renderer.get(), textures, game, save_path,
                    test_directory / "settings.y3r", test_directory / "highscores.y3r",
                    scores, settings, audio);
                const auto resumed = yipe::load_game(save_path);
                require(result.choice == StartupChoice::quit && dialog_choices.empty()
                            && resumed.player.silly_mode == expected_mode
                            && resumed.player.inventory[28] == 7
                            && resumed.player.weapon_rank == 2 && resumed.player.armor_rank == 2
                            && resumed.random_state == game.random_state
                            && !std::filesystem::exists(test_directory / "settings.y3r"),
                        "F10 mode toggle/save changed unrelated state or did not persist");
                require(messages == (repeat ? std::vector<std::string>{"Game Saved"}
                                            : std::vector<std::string>{"Silly/Normal Mode", "Game Saved"}),
                        "mode toggle or repeated F10 produced unexpected messages");
                if (!repeat) {
                    require(message_texts.front() == (expected_mode
                                ? "Weapon and Armor names will now be in 'Silly' mode."
                                : "Weapon and Armor names will now be in 'Normal' mode."),
                            "mode toggle did not use the recovered confirmation");
                }
                const auto equipment = equipment_statistics_page(resumed.player);
                require(equipment.find(expected_mode ? "Weapon: Thumbtack  (AC 0, WC 2)"
                                                     : "Weapon: Small Dagger  (AC 0, WC 2)")
                            != std::string::npos
                            && equipment.find(expected_mode ? "Body Armor: T-Shirt with Pocket  (AC 2, WC 0)"
                                                            : "Body Armor: Robe  (AC 2, WC 0)")
                                != std::string::npos,
                        "equipment panel did not use the resumed mode and original ratings");
                auto shop_player = resumed.player;
                dialog_choices = {{"Weapon Shop", 0, expected_mode ? "Current: Thumbtack" : "Current: Small Dagger"}};
                show_equipment_shop(window.get(), shop_player, 0x11, yipe::EquipmentKind::weapon);
                dialog_choices = {{"Armor Shop", 0, expected_mode ? "Current: T-Shirt with Pocket" : "Current: Robe"}};
                show_equipment_shop(window.get(), shop_player, 0x11, yipe::EquipmentKind::armor);
                require(dialog_choices.empty(), "shops did not show the current mode");
            }
        }

        dialog_choices = {{"Settings", 0, "Sound:"}};
        show_settings(window.get(), settings, test_directory / "settings.y3r", audio);
        require(dialog_choices.empty(), "startup settings did not close");
        yipe::Player settings_player;
        settings_player.silly_mode = true;
        dialog_choices = {{"Settings", 4, "Equipment names: Silly"},
                          {"Settings", 0, "Equipment names: Silly"}};
        show_settings(window.get(), settings, test_directory / "settings.y3r", audio,
                      &settings_player);
        require(settings_player.silly_mode && dialog_choices.empty()
                    && yipe::load_settings(test_directory / "settings.y3r").keys
                        == yipe::GameSettings{}.keys,
                "restoring global preferences changed the character's mode");
        cases += 2;

        prepare_startup_view(window.get(), renderer.get());
        const auto check_creation = [&](const std::vector<SDL_Keycode>& keys,
                                        std::uint16_t portrait, int type = 1) {
            ++cases;
            dialog_choices = {{"Create Character", type, ""}, {"Character Attributes", 1, "Stat limit 50"},
                              {"Difficulty", 2, ""}, {"Show Intro", 0, ""}};
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(SDLK_RETURN);
            for (auto key : keys) push_key(key);
            push_key(SDLK_RETURN);
            yipe::MsvcRandom random(1);
            const auto player = choose_character(window.get(), renderer.get(), textures, random);
            require(player && player->inventory[28] == portrait
                        && player->silly_mode == (portrait >= 9)
                        && player->stat_cap_mode == 2 && player->maximum_stat() == 50
                        && dialog_choices.empty(),
                    "character creation did not link the selected portrait and mode");
        };
        for (int index = 0; index < 8; ++index) {
            check_creation({SDLK_1 + index}, static_cast<std::uint16_t>(index + 1));
        }
        for (int index = 0; index < 4; ++index) {
            check_creation({SDLK_s, SDLK_1 + index}, static_cast<std::uint16_t>(index + 9));
        }
        check_creation({SDLK_8, SDLK_s, SDLK_8}, 9);
        check_creation({SDLK_s, SDLK_4, SDLK_s, SDLK_8}, 8);
        for (int type = 2; type <= 4; ++type) {
            check_creation({SDLK_1}, 1, type);
        }
        prepare_game_view(window.get(), renderer.get());
        for (int mode = 0; mode <= 2; ++mode) {
            yipe::Player player;
            player.stat_cap_mode = mode;
            messages.clear();
            message_texts.clear();
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(SDLK_d);
            push_key(SDLK_ESCAPE);
            show_player_status(window.get(), renderer.get(), textures, player,
                               test_directory / "highscores.y3r", scores);
            require(messages == std::vector<std::string>{"Player Status Details"}
                        && message_texts.front().find("Stat limit " + std::to_string(30 + mode * 10))
                            != std::string::npos,
                    "status did not display the character's saved stat limit");
            ++cases;
        }
        prepare_startup_view(window.get(), renderer.get());
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        push_key(SDLK_s);
        SDL_Event hidden_portrait_click{};
        hidden_portrait_click.type = SDL_MOUSEBUTTONUP;
        hidden_portrait_click.button.button = SDL_BUTTON_LEFT;
        hidden_portrait_click.button.windowID = SDL_GetWindowID(window.get());
        SDL_RenderLogicalToWindow(renderer.get(), 20, 182,
                                  &hidden_portrait_click.button.x,
                                  &hidden_portrait_click.button.y);
        require(SDL_PushEvent(&hidden_portrait_click) == 1, "could not queue portrait click");
        push_key(SDLK_ESCAPE);
        require(!choose_portrait(window.get(), renderer.get(), textures),
                "Silly Mode accepted a hidden normal portrait or ignored cancellation");
        ++cases;
        const auto push_wait = [](int ticks) {
            SDL_Event event{};
            event.type = SDL_USEREVENT;
            event.user.code = ticks;
            if (SDL_PushEvent(&event) != 1) throw std::runtime_error(SDL_GetError());
        };
        const auto run_script = [&](yipe::GameState game, const std::string& name) {
            std::cerr << "CI progress: " << name << " case " << cases << '\n';
            auto path = test_directory / (name + ".y3r");
            push_key(SDLK_F5);
            push_key(SDLK_q);
            const auto result = run_game(
                window.get(), renderer.get(), textures, std::move(game), path,
                test_directory / "settings.y3r", test_directory / "highscores.y3r",
                scores, settings, audio);
            require(result.choice == StartupChoice::quit, "scripted exploration did not quit");
            require(pending_ticks == 0, "scripted exploration left unconsumed timer ticks");
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            ++cases;
            return yipe::load_game(path);
        };
        scripted_clock = true;
        // A failed write must keep the live game, its old file, and its save
        // destination until a later Save As succeeds. A directory at .tmp
        // causes a real I/O failure without relying on permission bits.
        const auto recovery_open_target = test_directory / "recovery-open-target.y3r";
        yipe::save_game_atomic(recovery_open_target, yipe::GameState{});
        yipe::GameState recovery_start;
        recovery_start.player.name = "SaveRecovery";
        recovery_start.player.health = 7;
        recovery_start.player.monster_repellent_steps = 5;
        recovery_start.player.random_encounter_threshold = 125;
        recovery_start.random_state = 71;
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        push_key(SDLK_DOWN);
        const auto recovery_baseline = run_script(recovery_start, "save-recovery-baseline");
        for (std::string_view action : {"save", "ctrl-save", "save-as", "quit", "new", "open", "close", "first-save"}) {
            const auto original = test_directory / ("failed-" + std::string(action) + ".y3r");
            const auto alternate = test_directory / ("retry-" + std::string(action) + ".y3r");
            auto game = recovery_start;
            yipe::save_game_atomic(original, game);
            std::ifstream before(original, std::ios::binary);
            const std::string original_bytes{std::istreambuf_iterator<char>(before), {}};
            require(std::filesystem::create_directory(original.string() + ".tmp"),
                    "could not block the temporary save destination");
            auto path = action == "first-save" ? std::filesystem::path{} : original;
            save_choices = action == "save-as" || action == "first-save"
                ? std::deque<std::optional<std::filesystem::path>>{original, alternate}
                : std::deque<std::optional<std::filesystem::path>>{alternate};
            selected_open = recovery_open_target;
            save_dialogs = open_dialogs = 0;
            remembered_files.clear();
            messages.clear();
            message_texts.clear();
            dialog_choices.clear();
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            if (action == "quit" || action == "new" || action == "open") {
                dialog_choices.push_back({action == "quit" ? "Quit Game"
                    : action == "new" ? "New Game" : "Open Game", 2, ""});
            }
            if (action == "save-as") push_key(SDLK_s, false, KMOD_CTRL | KMOD_SHIFT);
            else if (action == "ctrl-save") push_key(SDLK_s, false, KMOD_CTRL);
            else if (action == "quit") push_key(SDLK_q);
            else if (action == "new") push_key(SDLK_n, false, KMOD_CTRL);
            else if (action == "open") push_key(SDLK_o, false, KMOD_CTRL);
            else if (action == "close") {
                SDL_Event close{};
                close.type = SDL_QUIT;
                require(SDL_PushEvent(&close) == 1, "could not queue window close");
            } else push_key(SDLK_F5);
            push_key(SDLK_DOWN);
            push_key(SDLK_s, false, KMOD_CTRL | KMOD_SHIFT);
            push_key(SDLK_q);
            dialog_choices.push_back({"Quit Game", 1, ""});
            const auto result = run_game(window.get(), renderer.get(), textures, game, path,
                test_directory / "settings.y3r", test_directory / "highscores.y3r",
                scores, settings, audio);
            std::ifstream after(original, std::ios::binary);
            const std::string remaining_bytes{std::istreambuf_iterator<char>(after), {}};
            const auto resumed = yipe::load_game(alternate);
            require(result.choice == StartupChoice::quit && path == alternate
                        && dialog_choices.empty() && save_choices.empty()
                        && remembered_files == std::vector<std::filesystem::path>{alternate}
                        && original_bytes == remaining_bytes
                        && messages == std::vector<std::string>{"Save Game Failed", "Game Saved"}
                        && message_texts.front().find("temporary save file") != std::string::npos
                        && resumed.player.name == game.player.name
                        && resumed.player.health == game.player.health
                        && resumed.player.inventory == game.player.inventory
                        && resumed.player.monster_repellent_steps == 5
                        && resumed.random_state == recovery_baseline.random_state
                        && resumed.location.position == yipe::MapPosition{5, 6},
                    ("failed " + std::string(action)
                        + " discarded live progress, changed the old file, or blocked retry").c_str());
            std::filesystem::remove(original.string() + ".tmp");
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            ++cases;
        }
        {
            const auto score_path = test_directory / "failed-highscores.y3r";
            auto recovery_scores = yipe::default_high_scores();
            const auto previous_scores = recovery_scores;
            yipe::save_high_scores_atomic(score_path, recovery_scores);
            require(std::filesystem::create_directory(score_path.string() + ".tmp"),
                    "could not block the temporary high-score destination");
            auto game = recovery_start;
            game.player.gold = 600;
            auto path = test_directory / "after-score-failure.y3r";
            messages.clear();
            message_texts.clear();
            dialog_choices = {{"High Scores", 0, ""}, {"Quit Game", 1, ""}};
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(SDLK_h, false, KMOD_CTRL);
            push_key(SDLK_DOWN);
            push_key(SDLK_F5);
            push_key(SDLK_q);
            const auto result = run_game(window.get(), renderer.get(), textures, game, path,
                test_directory / "settings.y3r", score_path, recovery_scores, settings, audio);
            const auto resumed = yipe::load_game(path);
            require(result.choice == StartupChoice::quit && dialog_choices.empty()
                        && messages == std::vector<std::string>{"High Scores Save Failed", "Game Saved"}
                        && recovery_scores == previous_scores
                        && yipe::load_high_scores(score_path) == previous_scores
                        && resumed.player.gold == 600 && resumed.player.health == 7
                        && resumed.player.monster_repellent_steps == 5
                        && resumed.random_state == recovery_baseline.random_state
                        && resumed.location.position == yipe::MapPosition{5, 6},
                    "high-score write failure discarded gameplay or changed persisted scores");
            submit_high_scores(window.get(), score_path, recovery_scores, resumed.player);
            require(recovery_scores != previous_scores
                        && yipe::load_high_scores(score_path) == recovery_scores,
                    "high-score submission could not retry after a write failure");
            ++cases;
        }
        {
            yipe::GameState game;
            const auto map = *yipe::original_map(game.location.map_id);
            yipe::initialize_map_visibility(game.map_visibility, map, game.location.position);
            const auto pixels = [&](SDL_Rect region) {
                std::vector<unsigned char> data(static_cast<std::size_t>(region.w * region.h * 4));
                require(SDL_RenderReadPixels(renderer.get(), &region, SDL_PIXELFORMAT_RGBA32,
                                             data.data(), region.w * 4) == 0,
                        "could not read status-bar rendering");
                return data;
            };
            prepare_game_view(window.get(), renderer.get());
            SDL_SetWindowSize(window.get(), map_width, map_height);
            render_game(renderer.get(), map, textures, game);
            const auto map_pixels = pixels({0, 0, map_width, map_height});
            toggle_status_bar(renderer.get());
            SDL_SetWindowSize(window.get(), map_width, map_height + status_bar_height);
            const auto original_modifiers = SDL_GetModState();
            SDL_SetModState(KMOD_NONE);
            render_game(renderer.get(), map, textures, game);
            require(pixels({0, 0, map_width, map_height}) == map_pixels,
                    "visible status bar covered, resized, or shifted map pixels");
            const auto inactive = pixels({0, map_height, map_width, status_bar_height});
            for (int lock : {KMOD_CAPS, KMOD_NUM, KMOD_SCROLL}) {
                SDL_SetModState(static_cast<SDL_Keymod>(lock));
                render_game(renderer.get(), map, textures, game);
                require(pixels({0, 0, map_width, map_height}) == map_pixels
                            && pixels({0, map_height, map_width, status_bar_height}) != inactive,
                        "lock indicator did not update or altered the map");
                ++cases;
            }
            SDL_SetModState(original_modifiers);
            render_game(renderer.get(), map, textures, game);
            auto capture = pixels({0, 0, map_width, map_height + status_bar_height});
            Surface frame(SDL_CreateRGBSurfaceWithFormatFrom(capture.data(),
                map_width, map_height + status_bar_height, 32, map_width * 4, SDL_PIXELFORMAT_RGBA32),
                SDL_FreeSurface);
            require(static_cast<bool>(frame), "could not allocate the status-bar capture");
            Surface rgb(SDL_ConvertSurfaceFormat(frame.get(), SDL_PIXELFORMAT_RGB24, 0), SDL_FreeSurface);
            const auto capture_directory = std::filesystem::absolute(argv[0]).parent_path() / "interface-output";
            std::filesystem::create_directories(capture_directory);
            require(rgb && SDL_SaveBMP(rgb.get(), (capture_directory / "status-bar.bmp").string().c_str()) == 0,
                    "could not save the status-bar visual evidence");
            const auto printed = printable_game_view(renderer.get(), map, textures, game);
            int logical_width = 0;
            int logical_height = 0;
            SDL_RenderGetLogicalSize(renderer.get(), &logical_width, &logical_height);
            require(printed->w == map_width && printed->h == map_height
                        && logical_width == map_width && logical_height == map_height + status_bar_height,
                    "printing included the bar or lost the visible-bar viewport");
            for (int row = 0; row < map_height; ++row) {
                require(std::memcmp(static_cast<unsigned char*>(printed->pixels) + row * printed->pitch,
                                    map_pixels.data() + row * map_width * 4, map_width * 4) == 0,
                        "status bar changed the map's printed pixels");
            }
            ++cases;

            const auto saved_bytes = [&](const char* file) {
                std::ifstream input(test_directory / file, std::ios::binary);
                return std::string(std::istreambuf_iterator<char>(input), {});
            };
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            static_cast<void>(run_script(game, "bar-baseline"));
            const auto baseline = saved_bytes("bar-baseline.y3r");
            const auto original_keys = settings.keys;
            settings.keys[0] = 0x75;
            require(key_binding_warnings(settings).find("Player Status") != std::string::npos,
                    "F6 binding conflict was not explained");
            for (int modifiers : std::array<int, 4>{KMOD_NONE, KMOD_LSHIFT, KMOD_RSHIFT,
                                                    KMOD_LSHIFT | KMOD_CAPS}) {
                push_key(SDLK_F6, false, modifiers);
                push_key(SDLK_F6, true, modifiers);
                push_key(SDLK_d);
                push_key(SDLK_F6, false, modifiers);
                messages.clear();
                delay_status_dialog = true;
                static_cast<void>(run_script(game, "pane-switch"));
                delay_status_dialog = false;
                require(messages == std::vector<std::string>{"Player Status Details", "Game Saved"}
                            && saved_bytes("pane-switch.y3r") == baseline,
                        "F6 pane adaptation moved, repeated, or advanced exploration");
            }
            for (const auto [key, modifiers] : std::array<std::pair<SDL_Keycode, int>, 6>{{
                    {SDLK_x, KMOD_LCTRL}, {SDLK_v, KMOD_RCTRL}, {SDLK_z, KMOD_LCTRL},
                    {SDLK_INSERT, KMOD_RCTRL}, {SDLK_INSERT, KMOD_LSHIFT}, {SDLK_DELETE, KMOD_RSHIFT}}}) {
                settings.keys[4] = *portable_key_code(key);
                push_key(key, false, modifiers);
                static_cast<void>(run_script(game, "edit-outside-text"));
                require(saved_bytes("edit-outside-text.y3r") == baseline,
                        "Edit accelerator fell through to movement outside a text field");
            }
            settings.keys = original_keys;
            for (int modifiers : std::array<int, 5>{KMOD_LCTRL, KMOD_RCTRL,
                    KMOD_LCTRL | KMOD_CAPS, KMOD_LCTRL | KMOD_LGUI, KMOD_RCTRL | KMOD_LALT}) {
                for (bool repeat : {false, true}) {
                    status_bar_visible = true;
                    const bool toggles = !repeat && (modifiers & (KMOD_ALT | KMOD_GUI)) == 0;
                    push_key(SDLK_b, repeat, modifiers);
                    static_cast<void>(run_script(game, "bar-toggled"));
                    require(status_bar_visible != toggles
                                && saved_bytes("bar-toggled.y3r") == baseline,
                            "status shortcut ignored modifiers/repeats or changed game state");
                }
            }
            status_bar_visible = true;
            dialog_choices = {{"Settings", 6, "Status bar: Shown"},
                              {"Settings", 0, "Status bar: Hidden"}};
            delay_settings_dialog = true;
            push_key(SDLK_F10);
            static_cast<void>(run_script(game, "bar-settings"));
            delay_settings_dialog = false;
            require(!status_bar_visible && dialog_choices.empty()
                        && saved_bytes("bar-settings.y3r") == baseline,
                    "Settings toggle did not pause exploration or altered saved progress");
            const auto settings_file = test_directory / "bar-only-settings.y3r";
            dialog_choices = {{"Settings", 6, "Status bar: Hidden"}, {"Settings", 0, "Status bar: Shown"}};
            show_settings(window.get(), settings, settings_file, audio);
            require(status_bar_visible && !std::filesystem::exists(settings_file),
                    "session status-bar choice rewrote persistent settings");
            ++cases;
            for (const auto size : {SDL_Point{map_width, map_height + status_bar_height},
                    SDL_Point{map_width * 2, (map_height + status_bar_height) * 2},
                    SDL_Point{map_width * 3, (map_height + status_bar_height) * 2}}) {
                prepare_game_view(window.get(), renderer.get());
                SDL_SetWindowSize(window.get(), size.x, size.y);
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                int window_x = 0;
                int window_y = 0;
                float logical_x = 0;
                float logical_y = 0;
                SDL_RenderLogicalToWindow(renderer.get(), 9 * tile_size + 16,
                                          5 * tile_size + 16, &window_x, &window_y);
                SDL_RenderWindowToLogical(renderer.get(), window_x, window_y,
                                          &logical_x, &logical_y);
                require(static_cast<int>(logical_x) / tile_size == 9
                            && static_cast<int>(logical_y) / tile_size == 5,
                        "scaled window did not preserve the clicked map cell");
                // A bar click must never be a movement command; the next map
                // click still means one eastward step at every scale.
                push_mouse(100, map_height + 10);
                push_mouse(9 * tile_size + 16, 5 * tile_size + 16);
                const auto moved = run_script(game, "bar-mouse");
                require(moved.location.position == yipe::MapPosition{6, 5},
                        "status-bar or scaled-map click changed movement coordinates");
            }
            status_bar_visible = false;
            prepare_game_view(window.get(), renderer.get());
            messages.clear();
        }
        {
            const auto key = [](SDL_Keycode sym, int mods = 0, bool repeat = false) {
                SDL_Event event{};
                event.type = SDL_KEYDOWN;
                event.key.keysym.sym = sym;
                event.key.keysym.mod = static_cast<Uint16>(mods);
                event.key.repeat = repeat;
                return event;
            };
            const auto typed = [](const char* text) {
                SDL_Event event{};
                event.type = SDL_TEXTINPUT;
                SDL_strlcpy(event.text.text, text, sizeof(event.text.text));
                return event;
            };
            const auto clipboard = [] {
                std::unique_ptr<char, decltype(&SDL_free)> text(SDL_GetClipboardText(), SDL_free);
                return std::string(text.get());
            };
            const auto edit = [&](std::vector<SDL_Event> events,
                                  std::optional<std::string> expected) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                const std::string title = SDL_GetWindowTitle(window.get());
                text_input_events = std::move(events);
                text_event_index = 0;
                const auto result = request_text(window.get(), "Editor test", "Enter text.", "name");
                require(result == expected && text_event_index == text_input_events.size()
                            && !SDL_IsTextInputActive() && SDL_GetWindowTitle(window.get()) == title,
                        "text editing produced wrong text, consumed wrong input, or failed cleanup");
                text_input_events.clear();
                ++cases;
            };
            for (int modifier : {KMOD_LCTRL, KMOD_RCTRL, KMOD_LGUI, KMOD_RGUI}) {
                SDL_SetClipboardText("unchanged");
                edit({typed("ABé🙂"), key(SDLK_LEFT), key(SDLK_LEFT, KMOD_LSHIFT),
                      key(SDLK_c, modifier | KMOD_CAPS), key(SDLK_END), key(SDLK_v, modifier),
                      key(SDLK_HOME), key(SDLK_DELETE), key(SDLK_END), key(SDLK_BACKSPACE),
                      key(SDLK_RETURN)}, "Bé🙂");
                require(clipboard() == "é", "copy split a UTF-8 character or copied unselected text");
                edit({typed("Hero"), key(SDLK_a, modifier), key(SDLK_x, modifier),
                      key(SDLK_z, modifier), key(SDLK_z, modifier), key(SDLK_v, modifier),
                      key(SDLK_RETURN)}, "Hero");
                require(clipboard() == "Hero", "cut failed to copy the selection");
                edit({typed("Hero"), key(SDLK_a, modifier), typed("New"), key(SDLK_z, modifier),
                      key(SDLK_RETURN)}, "Hero");
                edit({typed("Hero"), key(SDLK_a, modifier), key(SDLK_x, modifier, true),
                      key(SDLK_v, modifier, true), key(SDLK_RETURN)}, "Hero");
            }
            edit({typed("é🙂"), key(SDLK_LEFT, KMOD_LSHIFT), key(SDLK_BACKSPACE),
                  key(SDLK_z, KMOD_RCTRL), key(SDLK_HOME), key(SDLK_RIGHT, KMOD_RSHIFT),
                  key(SDLK_DELETE), key(SDLK_KP_ENTER)}, "🙂");
            edit({typed("ABCD"), key(SDLK_HOME), key(SDLK_RIGHT, KMOD_LSHIFT),
                  key(SDLK_RIGHT, KMOD_LSHIFT), key(SDLK_LEFT), typed("X"), key(SDLK_END),
                  key(SDLK_LEFT, KMOD_RSHIFT), key(SDLK_RIGHT), typed("Y"), key(SDLK_RETURN)}, "XABCDY");
            edit({typed("Original"), key(SDLK_a, KMOD_LCTRL), key(SDLK_INSERT, KMOD_RCTRL),
                  key(SDLK_DELETE, KMOD_LSHIFT), key(SDLK_INSERT, KMOD_RSHIFT),
                  key(SDLK_BACKSPACE, KMOD_LALT), key(SDLK_RETURN)}, "");
            edit({typed("Keep"), key(SDLK_a, KMOD_LCTRL), key(SDLK_DELETE, KMOD_LSHIFT, true),
                  key(SDLK_INSERT, KMOD_LALT | KMOD_LSHIFT), key(SDLK_RETURN)}, "Keep");
            SDL_SetClipboardText("");
            edit({typed("Keep"), key(SDLK_x, KMOD_LCTRL), key(SDLK_c, KMOD_LCTRL),
                  key(SDLK_a, KMOD_LCTRL), key(SDLK_v, KMOD_LCTRL), key(SDLK_RETURN)}, "Keep");
            messages.clear();
            clipboard_failures = 1;
            edit({typed("Keep"), key(SDLK_a, KMOD_LCTRL), key(SDLK_x, KMOD_LCTRL),
                  key(SDLK_RETURN)}, "Keep");
            require(messages == std::vector<std::string>{"Editor test", "Clipboard Failed"},
                    "failed cut deleted text or suppressed its error");
            for (const auto& paste : {std::string(129, 'A'), std::string("two\nlines"), std::string("tab\t")}) {
                SDL_SetClipboardText(paste.c_str());
                messages.clear();
                edit({typed("Keep"), key(SDLK_a, KMOD_LCTRL), key(SDLK_v, KMOD_LCTRL),
                      key(SDLK_RETURN)}, "Keep");
                require(messages == std::vector<std::string>{"Editor test", "Text Not Inserted"},
                        "invalid paste silently changed the selection");
            }
            SDL_SetClipboardText(std::string(128, 'A').c_str());
            edit({key(SDLK_v, KMOD_LCTRL), key(SDLK_RETURN)}, std::string(128, 'A'));
            dialog_choices = {{"Edit Text", 5, "Menu"}, {"Edit Text", 3, "Menu"},
                              {"Edit Text", 2, "Menu"}, {"Edit Text", 1, "(Empty)"},
                              {"Edit Text", 4, "Menu"}, {"Edit Text", 0, "MenuMenu"}};
            SDL_Event right_click{};
            right_click.type = SDL_MOUSEBUTTONUP;
            right_click.button.button = SDL_BUTTON_RIGHT;
            edit({typed("Menu"), key(SDLK_F2, KMOD_CAPS), key(SDLK_F2), right_click,
                  key(SDLK_F2), key(SDLK_F2), key(SDLK_F2), key(SDLK_RETURN)}, "MenuMenu");
            require(dialog_choices.empty(), "Edit menu failed to dispatch all commands");
            edit({typed("Cancel"), key(SDLK_ESCAPE)}, std::nullopt);
            SDL_Event quit{};
            quit.type = SDL_QUIT;
            edit({typed("Quit"), quit}, std::nullopt);
            require(SDL_HasEvent(SDL_QUIT), "text editor swallowed application quit");
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            text_input_events = {typed("Failure")};
            text_event_index = 0;
            const std::string title = SDL_GetWindowTitle(window.get());
            bool failed = false;
            try { static_cast<void>(request_text(window.get(), "Editor test", "Enter text.")); }
            catch (const std::runtime_error&) { failed = true; }
            text_input_events.clear();
            require(failed && !SDL_IsTextInputActive() && SDL_GetWindowTitle(window.get()) == title,
                    "text input failure left the editor active");
            ++cases;
            messages.clear();
        }
        {
            // Recent-file selection uses the production open/leave logic. The
            // platform list is isolated from the user's native recent files.
            yipe::GameState game;
            game.player.name = "Current";
            game.random_state = 71;
            const auto target = test_directory / "Recent target.y3r";
            auto other = game;
            other.player.name = "Other";
            yipe::save_game_atomic(target, other);
            recent_files.clear();
            remembered_files.clear();
            messages.clear();
            require(!choose_recent_game_to_open(window.get(), test_directory)
                        && messages == std::vector<std::string>{"Recent Games"},
                    "empty history did not explain how to add games");
            ++cases;
            recent_files = {target, test_directory / "missing.y3r"};
            for (int cancel : {0, -1}) {
                dialog_choices = {{"Recent Games", 2, "Game 1 of 2"},
                                  {"Recent Games", 3, "missing.y3r"},
                                  {"Recent Games", cancel, "Recent target.y3r"}};
                require(!choose_recent_game_to_open(window.get(), test_directory)
                            && dialog_choices.empty() && remembered_files.empty(),
                        "recent navigation/cancel changed history or opened a game");
                ++cases;
            }
            dialog_choices = {{"Recent Games", 4, "Game 1 of 2"}};
            require(!choose_recent_game_to_open(window.get(), test_directory)
                        && recent_files.empty() && std::filesystem::exists(target),
                    "clear list removed a save or retained history");
            ++cases;

            for (int control : {KMOD_LCTRL, KMOD_RCTRL}) {
                for (bool same : {false, true}) {
                    for (int leave : {0, 1}) {
                        auto active = same ? target : test_directory / "current.y3r";
                        recent_files = {target};
                        remembered_files.clear();
                        open_dialogs = save_dialogs = 0;
                        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                        dialog_choices = {{"Recent Games", 1, target.string()},
                                          {same ? "Reopen Game" : "Open Game", leave, ""}};
                        push_key(SDLK_l, false, control);
                        push_key(SDLK_q);
                        const auto result = run_game(window.get(), renderer.get(), textures,
                            game, active, test_directory / "settings.y3r",
                            test_directory / "highscores.y3r", scores, settings, audio);
                        require(dialog_choices.empty() && open_dialogs == 0 && save_dialogs == 0
                                    && remembered_files == (leave == 1
                                        ? std::vector<std::filesystem::path>{target}
                                        : std::vector<std::filesystem::path>{}),
                                "recent open bypassed leave checks, chose a file, or remembered a cancellation");
                        require(leave == 1
                                    ? result.choice == StartupChoice::load_game
                                        && result.opened_game->player.name == "Other"
                                        && result.opened_source_path == target
                                    : result.choice == StartupChoice::quit,
                                "recent open returned the wrong game");
                        ++cases;
                    }
                }
            }
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            recent_files = {test_directory / "missing.y3r"};
            remembered_files.clear();
            messages.clear();
            dialog_choices = {{"Recent Games", 1, "missing.y3r"}};
            push_key(SDLK_l, false, KMOD_LCTRL);
            const auto missing = run_script(game, "missing-recent");
            require(missing.player.name == game.player.name
                        && messages == std::vector<std::string>{"Open Game Failed", "Game Saved"}
                        && remembered_files == std::vector<std::filesystem::path>{
                            test_directory / "missing-recent.y3r"},
                    "failed recent open altered the player or remembered a missing file");

            // Failed/canceled native writes must never be added to the list.
            remembered_files.clear();
            auto invalid_destination = test_directory / "absent" / "save.y3r";
            messages.clear();
            require(!save_current_game(window.get(), invalid_destination, game,
                                      yipe::MsvcRandom(71), false)
                        && remembered_files.empty()
                        && messages == std::vector<std::string>{"Save Game Failed"},
                    "failed save escaped recovery or entered recent history");
            selected_save.reset();
            std::filesystem::path unsaved;
            require(!save_current_game(window.get(), unsaved, game, yipe::MsvcRandom(71), false)
                        && remembered_files.empty(), "canceled save entered recent history");
            ++cases;
            selected_save = invalid_destination;
            messages.clear();
            auto active = target;
            save_current_game_as(window.get(), active, game, yipe::MsvcRandom(71));
            require(active == target && remembered_files.empty()
                        && messages == std::vector<std::string>{"Save Game Failed"},
                    "failed Save As replaced the previous save destination");
            messages.clear();
            require(!save_current_game(window.get(), unsaved, game, yipe::MsvcRandom(71), false)
                        && unsaved.empty() && remembered_files.empty()
                        && messages == std::vector<std::string>{"Save Game Failed"},
                    "failed first save adopted an unwritten destination");
            selected_save.reset();
            cases += 2;

            // An imported recent file still selects a separate native output.
            recent_files = {std::filesystem::path(YIPE_TEST_EVIDENCE_DIR)
                / "r08/R08Combat-south.yp3"};
            selected_save = test_directory / "recent-import.y3r";
            dialog_choices = {{"Recent Games", 1, ".yp3"}};
            const auto imported_recent = choose_recent_game_to_open(window.get(), test_directory);
            require(imported_recent && imported_recent->opened_source_path == recent_files[0]
                        && imported_recent->opened_save_path == *selected_save
                        && imported_recent->opened_game->player.name == "R08Combat"
                        && remembered_files.empty() && !std::filesystem::exists(*selected_save),
                    "recent import lost its source, wrote prematurely, or remembered before acceptance");
            selected_save.reset();
            dialog_choices = {{"Recent Games", 1, ".yp3"}};
            require(!choose_recent_game_to_open(window.get(), test_directory)
                        && remembered_files.empty(), "canceled import entered history");
            ++cases;

            // Both dialogs pause timers, even when open for a long time.
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            static_cast<void>(run_script(game, "interface-baseline"));
            const auto bytes = [&](const char* name) {
                std::ifstream file(test_directory / name, std::ios::binary);
                return std::string(std::istreambuf_iterator<char>(file), {});
            };
            const auto baseline = bytes("interface-baseline.y3r");
            recent_files = {target};
            for (int modifiers : std::array<int, 5>{KMOD_LCTRL, KMOD_RCTRL, KMOD_LCTRL | KMOD_CAPS,
                                  KMOD_LCTRL | KMOD_LALT, KMOD_LCTRL | KMOD_LGUI}) {
                for (bool repeat : {false, true}) {
                    const bool opens = !repeat && (modifiers & (KMOD_ALT | KMOD_GUI)) == 0;
                    dialog_choices.clear();
                    if (opens) dialog_choices = {{"Recent Games", 0, target.string()}};
                    push_key(SDLK_l, repeat, modifiers);
                    static_cast<void>(run_script(game, "recent-paused"));
                    require(dialog_choices.empty() && bytes("recent-paused.y3r") == baseline,
                            "recent shortcut modifiers/repeats or dialog advanced exploration");
                }
            }
            for (int mode : {0, 1, 2}) {
                game.player.stat_cap_mode = mode;
                static_cast<void>(run_script(game, "profile-baseline"));
                const auto before = bytes("profile-baseline.y3r");
                dialog_choices = {{"Yipe! III Help", 4, "Ctrl+L: Recent Games"},
                    {"Profile / Registration", 0,
                        "Current character's stat limit: " + std::to_string(30 + mode * 10)},
                    {"Yipe! III Help", 0, ""}};
                push_key(SDLK_F1);
                static_cast<void>(run_script(game, "profile-paused"));
                require(dialog_choices.empty() && bytes("profile-paused.y3r") == before,
                        "profile display changed character limits or advanced exploration");
            }
            dialog_choices = {{"Yipe! III Help", 4, ""},
                {"Profile / Registration", 1, "no original Yipe! III serial number"},
                {"Help and Info", 2, "Registration Options"},
                {"Help and Info", 2, "More Registration Options"},
                {"Help and Info", 0, "Even More Registration Options!"},
                {"Yipe! III Help", 0, ""}};
            show_help(window.get(), settings);
            require(dialog_choices.empty(), "profile did not reach the original registration pages");
            ++cases;
            recent_files.clear();
            remembered_files.clear();
            messages.clear();
        }
        {
            const auto clipboard_text = [] {
                std::unique_ptr<char, decltype(&SDL_free)> text(SDL_GetClipboardText(), SDL_free);
                if (!text) throw std::runtime_error(SDL_GetError());
                return std::string(text.get());
            };
            const auto snapshot = [&](const char* name) {
                std::ifstream input(test_directory / name, std::ios::binary);
                if (!input) throw std::runtime_error("missing score-code state snapshot");
                return std::string(std::istreambuf_iterator<char>(input), {});
            };
            yipe::GameState game;
            game.player.level = 16;
            game.player.gold = 451;
            game.player.monster_defeats[49] = 61;
            game.player.quest_progress_total = 61;
            game.player.difficulty = 3;
            game.player.gatekeeper_state = 2;
            game.player.value_06a4 = 5;
            game.player.monster_repellent_steps = 5;
            game.player.random_encounter_threshold = 125;
            constexpr auto code = "1L0W1L1000W1L0W6W3W3W";
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            static_cast<void>(run_script(game, "score-code-before"));
            const auto before = snapshot("score-code-before.y3r");
            high_score_button_counts.clear();
            dialog_choices = {{"High Scores", 1, "Medium Mode"},
                              {"High Scores", 3, "Easy Mode"},
                              {"High Scores", 0, "Hard Mode"}};
            show_high_scores(window.get(), scores);
            require(dialog_choices.empty() && high_score_button_counts == std::vector<int>{4, 4, 4},
                    "startup offered a score code without an active character");
            ++cases;
            for (int cancel : {0, -1}) {
                require(SDL_SetClipboardText("keep existing clipboard") == 0, SDL_GetError());
                SDL_Event quit{};
                quit.type = SDL_QUIT;
                require(SDL_PushEvent(&quit) == 1, SDL_GetError());
                messages.clear();
                dialog_choices = {{"High Score Code", cancel, code}};
                show_high_score_code(window.get(), game.player);
                require(dialog_choices.empty() && messages.empty() && SDL_HasEvent(SDL_QUIT)
                            && clipboard_text() == "keep existing clipboard"
                            && score_code_message.find("YipeScores@aol.com") != std::string::npos
                            && score_code_message.find("nothing is sent") != std::string::npos
                            && score_code_message.find("Older save") == std::string::npos,
                        "score-code cancellation changed the clipboard, lost quit, or omitted its context");
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                ++cases;
            }
            game.player.monster_defeat_history_complete = false;
            game.player.quest_progress_total = 900;
            dialog_choices = {{"High Score Code", 0, "Earlier defeats are not included."}};
            show_high_score_code(window.get(), game.player);
            require(dialog_choices.empty() && score_code_message.find(code) != std::string::npos,
                    "incomplete-history code omitted its warning or substituted aggregate kills");
            game.player.monster_defeat_history_complete = true;
            game.player.quest_progress_total = 61;
            ++cases;
            for (bool retry : {false, true}) {
                messages.clear();
                message_texts.clear();
                clipboard_failures = 1;
                dialog_choices = {{"High Score Code", 1, code},
                                  {"High Score Code", retry ? 1 : 0, code}};
                show_high_score_code(window.get(), game.player);
                require(dialog_choices.empty() && clipboard_failures == 0
                            && message_texts.front() == "clipboard unavailable"
                            && messages == (retry
                                ? std::vector<std::string>{"Copy Code Failed", "High Score Code"}
                                : std::vector<std::string>{"Copy Code Failed"})
                            && clipboard_text() == (retry ? code : "keep existing clipboard"),
                        "clipboard failure was hidden, falsely reported success, or prevented retry/cancel");
                ++cases;
            }
            delay_score_code_dialog = true;
            for (int path = 0; path < 4; ++path) {
                messages.clear();
                high_score_button_counts.clear();
                dialog_choices = {{"High Scores", 1, "Hard Mode"},
                                  {"High Scores", 4, "Easy Mode"},
                                  {"High Score Code", 1, code},
                                  {"High Scores", 0, "Easy Mode"}};
                if (path < 2) {
                    push_key(SDLK_h, false, path == 0 ? KMOD_LCTRL : KMOD_RCTRL);
                } else {
                    push_key(SDLK_t, false, KMOD_LCTRL);
                    if (path == 2) push_key(SDLK_h);
                    else push_mouse(334, 300);
                    push_key(SDLK_ESCAPE);
                }
                static_cast<void>(run_script(game, "score-code-after"));
                require(dialog_choices.empty()
                            && high_score_button_counts == std::vector<int>{5, 5, 5}
                            && messages == std::vector<std::string>{"High Score Code", "Game Saved"}
                            && clipboard_text() == code
                            && snapshot("score-code-after.y3r") == before,
                        "score-code access/copy changed player/RNG state, used the browsed difficulty, or advanced time");
            }
            delay_score_code_dialog = false;
            for (int modifiers : {KMOD_LCTRL, KMOD_RCTRL}) {
                messages.clear();
                push_key(SDLK_h, true, modifiers);
                static_cast<void>(run_script(game, "score-code-after"));
                require(messages == std::vector<std::string>{"Game Saved"}
                            && snapshot("score-code-after.y3r") == before,
                        "repeated Ctrl+H reopened the code dialog or changed state");
            }
        }
        const auto original_settings = settings;
        {
            yipe::GameState game;
            game.location.position = {10, 5};
            game.player.monster_repellent_steps = 5;
            game.player.random_encounter_threshold = 125;
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            static_cast<void>(run_script(game, "print-before"));
            const auto read_snapshot = [&](const char* name) {
                std::ifstream input(test_directory / name, std::ios::binary);
                if (!input) throw std::runtime_error("missing printing state snapshot");
                return std::string(std::istreambuf_iterator<char>(input), {});
            };
            const auto before = read_snapshot("print-before.y3r");
            settings.keys[static_cast<std::size_t>(yipe::InputAction::north)] = 'P';
            for (int control : {KMOD_LCTRL, KMOD_RCTRL}) {
                for (int shift : {KMOD_NONE, KMOD_LSHIFT, KMOD_RSHIFT, KMOD_SHIFT}) {
                    for (bool repeat : {false, true}) {
                        print_dialogs = page_setup_dialogs = 0;
                        messages.clear();
                        push_key(SDLK_p, repeat, control | shift | KMOD_CAPS | KMOD_NUM);
                        static_cast<void>(run_script(game, "print-after"));
                        require(print_dialogs == (!repeat && shift == 0 ? 1 : 0)
                                    && page_setup_dialogs == (!repeat && shift != 0 ? 1 : 0)
                                    && messages == std::vector<std::string>{"Game Saved"}
                                    && read_snapshot("print-after.y3r") == before,
                                "print/setup shortcut repeated, moved the player, changed state, or advanced modal time");
                    }
                }
            }
            for (int modifier : {KMOD_LALT, KMOD_RALT, KMOD_LGUI, KMOD_RGUI, KMOD_MODE}) {
                print_dialogs = page_setup_dialogs = 0;
                push_key(SDLK_p, false, KMOD_LCTRL | modifier);
                push_key(SDLK_p, false, KMOD_LCTRL | KMOD_LSHIFT | modifier);
                static_cast<void>(run_script(game, "print-after"));
                require(print_dialogs == 0 && page_setup_dialogs == 0
                            && read_snapshot("print-after.y3r") == before,
                        "system-modified printing input reached a dialog or gameplay");
            }
            for (int shift : {KMOD_NONE, KMOD_LSHIFT}) {
                fail_printing = true;
                messages.clear();
                message_texts.clear();
                push_key(SDLK_p, false, KMOD_LCTRL | shift);
                static_cast<void>(run_script(game, "print-after"));
                require(messages == std::vector<std::string>{"Printing Failed", "Game Saved"}
                            && message_texts.front() == (shift ? "page setup unavailable" : "printing unavailable")
                            && read_snapshot("print-after.y3r") == before,
                        "printing failure was hidden, exited the game, or changed state");
                fail_printing = false;
            }
            print_dialogs = page_setup_dialogs = 0;
            push_key(SDLK_p);
            const auto moved = run_script(game, "print-unmodified");
            require(moved.location.position == yipe::MapPosition{10, 4}
                        && print_dialogs == 0 && page_setup_dialogs == 0,
                    "printing reserved unmodified P from configured movement");
            settings = original_settings;

            for (int map_id : {0x11, 0x0b, 0x33, 0x63, 0x75, 0x85}) {
                auto map = *yipe::original_map(map_id);
                game.location.map_id = map_id;
                game.location.position = {5, 5};
                game.player.current_map_id = map_id;
                initialize_map_state(game, map);
                yipe::initialize_map_visibility(game.map_visibility, map, game.location.position);
                if (map_id == 0x75) game.player.hell_gatekeeper_state = yipe::HellGatekeeperState::waiting;
                prepare_game_view(window.get(), renderer.get());
                SDL_SetWindowSize(window.get(), map_width, map_height);
                render_game(renderer.get(), map, textures, game, false);
                std::vector<unsigned char> reference(map_width * map_height * 4);
                require(SDL_RenderReadPixels(renderer.get(), nullptr, SDL_PIXELFORMAT_RGBA32,
                                             reference.data(), map_width * 4) == 0,
                        "could not read the reference game frame");
                for (const auto size : {yipe::MapPosition{map_width, map_height},
                                         yipe::MapPosition{map_width * 2, map_height * 2},
                                         yipe::MapPosition{1000, 600}}) {
                    SDL_SetWindowSize(window.get(), size.x, size.y);
                    SDL_Rect viewport_before{};
                    SDL_RenderGetViewport(renderer.get(), &viewport_before);
                    const Surface image = printable_game_view(renderer.get(), map, textures, game);
                    yipe_app::show_print_dialog(*image);
                    SDL_Rect viewport_after{};
                    SDL_RenderGetViewport(renderer.get(), &viewport_after);
                    require(printed_pixels == reference && SDL_GetRenderTarget(renderer.get()) == nullptr
                                && SDL_RectEquals(&viewport_before, &viewport_after),
                            "printing lost map/fog/sprites, included letterboxing, or damaged the renderer viewport");
                    ++cases;
                }
            }
            prepare_game_view(window.get(), renderer.get());
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        }
        for (std::size_t index = 0; index < 8; ++index) {
            settings.keys[index] = static_cast<std::uint16_t>('0' + index);
        }
        const auto check_key_direction = [&](SDL_Keycode key, yipe::MapPosition delta,
                                              int modifiers = 0, bool repeat = false) {
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            yipe::GameState game;
            game.location.position = {10, 5};
            push_key(key, repeat, modifiers);
            const auto moved = run_script(game, "configured-movement");
            require(moved.location.position == yipe::MapPosition{10 + delta.x, 5 + delta.y},
                    "configured/fixed movement key did not use the recovered direction");
        };
        const std::array<yipe::MapPosition, 8> direction_deltas{{
            {-1, -1}, {0, -1}, {1, -1}, {-1, 0}, {1, 0}, {-1, 1}, {0, 1}, {1, 1},
        }};
        const std::array fixed_directions{SDLK_KP_7, SDLK_UP, SDLK_KP_9, SDLK_LEFT,
                                         SDLK_RIGHT, SDLK_KP_1, SDLK_DOWN, SDLK_KP_3};
        for (std::size_t index = 0; index < 8; ++index) {
            check_key_direction(fixed_directions[index], direction_deltas[index]);
            check_key_direction(SDLK_0 + static_cast<SDL_Keycode>(index), direction_deltas[index]);
        }
        for (const auto [key, delta] : {
                 std::pair{SDLK_KP_8, yipe::MapPosition{0, -1}}, {SDLK_KP_2, {0, 1}},
                 {SDLK_KP_4, {-1, 0}}, {SDLK_KP_6, {1, 0}}, {SDLK_w, {0, -1}},
                 {SDLK_a, {-1, 0}}, {SDLK_s, {0, 1}}, {SDLK_d, {1, 0}}}) {
            check_key_direction(key, delta);
        }
        check_key_direction(SDLK_1, {0, -1}, KMOD_LSHIFT);
        check_key_direction(SDLK_1, {0, -1}, KMOD_RSHIFT);
        check_key_direction(SDLK_1, {0, -1}, 0, true);
        settings.keys[static_cast<std::size_t>(yipe::InputAction::north)] = 0x69;
        check_key_direction(SDLK_KP_9, {0, -1});
        settings.keys[static_cast<std::size_t>(yipe::InputAction::northwest)] = 'X';
        settings.keys[static_cast<std::size_t>(yipe::InputAction::northeast)] = 'X';
        check_key_direction(SDLK_x, {1, -1});
        settings = original_settings;
        settings.keys[static_cast<std::size_t>(yipe::InputAction::use_item)] = 'W';
        check_key_direction(SDLK_w, {0, -1});
        settings = original_settings;
        for (auto action : {yipe::InputAction::use_item, yipe::InputAction::cast}) {
            settings.keys[static_cast<std::size_t>(action)] = 0xba;
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            dialog_choices = {{action == yipe::InputAction::use_item ? "Inventory" : "Spells", 0, ""}};
            push_key(SDLK_SEMICOLON);
            static_cast<void>(run_script({}, "configured-exploration-action"));
            require(dialog_choices.empty(), "configured item/cast key did not reach its exploration dialog");
            settings = original_settings;
        }
        for (int modifiers : {KMOD_LALT, KMOD_RALT, KMOD_LGUI, KMOD_RGUI, KMOD_MODE}) {
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            messages.clear();
            push_key(SDLK_UP, false, modifiers);
            push_key(SDLK_q, false, modifiers);
            push_key(SDLK_s, false, modifiers | KMOD_LCTRL);
            push_key(SDLK_c, false, modifiers);
            push_key(SDLK_i, true);
            push_key(SDLK_c, true);
            const auto ignored_keys = run_script({}, "system-modifiers");
            require(ignored_keys.location.position == yipe::MapPosition{5, 5}
                        && messages == std::vector<std::string>{"Game Saved"},
                    "system-modified or repeated dialog keys leaked into exploration");
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(SDLK_r, false, modifiers);
            push_key(SDLK_RETURN);
            require(choose_combat_action(renderer.get(), "System keys", settings) == 1,
                    "system-modified key leaked into combat");
            ++cases;
        }
        for (const auto [action, key, expected] : {
                 std::tuple{yipe::InputAction::attack, SDLK_r, 4},
                 {yipe::InputAction::cast, SDLK_RETURN, 1},
                 {yipe::InputAction::attack, SDLK_ESCAPE, 4},
                 {yipe::InputAction::run, SDLK_RETURN, 4}}) {
            settings.keys[static_cast<std::size_t>(action)] = *portable_key_code(key);
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            push_key(key);
            require(choose_combat_action(renderer.get(), "Duplicate keys", settings) == expected,
                    "combat picker did not use recovered duplicate/fixed key precedence");
            settings = original_settings;
            ++cases;
        }
        const Texture menu = load_bitmap(renderer.get(), assets.bitmaps / "379-1033.bmp");
        const Texture copyright = load_bitmap(renderer.get(), assets.bitmaps / "326-1033.bmp");
        for (int modifiers : std::array<int, 4>{KMOD_LCTRL, KMOD_RCTRL,
                KMOD_RCTRL | KMOD_CAPS, KMOD_LCTRL | KMOD_LGUI}) {
            for (bool repeat : {false, true}) {
                status_bar_visible = true;
                prepare_startup_view(window.get(), renderer.get());
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                push_key(SDLK_b, repeat, modifiers);
                push_key(SDLK_ESCAPE);
                const bool toggles = !repeat && (modifiers & KMOD_GUI) == 0;
                require(run_startup_menu(renderer.get(), menu.get(), copyright.get()) == StartupChoice::quit
                            && status_bar_visible != toggles,
                        "startup status-bar toggle ignored modifiers/repeats");
                int width = 0;
                int height = 0;
                SDL_RenderGetLogicalSize(renderer.get(), &width, &height);
                require(width == startup_width && height == startup_height
                            + (status_bar_visible ? status_bar_height : 0),
                        "startup status bar failed to update logical geometry");
                ++cases;
            }
        }
        status_bar_visible = false;
        for (SDL_Keycode key : {SDLK_r, SDLK_l}) {
            for (int modifiers : std::array<int, 5>{KMOD_NONE, KMOD_LCTRL, KMOD_RCTRL,
                    KMOD_LCTRL | KMOD_LGUI, KMOD_RCTRL | KMOD_LALT}) {
                for (bool repeat : {false, true}) {
                    SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                    prepare_startup_view(window.get(), renderer.get());
                    push_key(key, repeat, modifiers);
                    push_key(SDLK_ESCAPE);
                    const auto expected = key == SDLK_l && modifiers == 0
                        ? StartupChoice::load_game
                        : !repeat && (modifiers & (KMOD_ALT | KMOD_GUI)) == 0
                            ? StartupChoice::recent_game : StartupChoice::quit;
                    require(run_startup_menu(renderer.get(), menu.get(), copyright.get()) == expected,
                            "startup recent shortcut ignored modifiers, repeated, or replaced plain Load");
                    ++cases;
                }
            }
        }
        for (const auto size : {SDL_Point{map_width, map_height},
                                SDL_Point{map_width * 2, map_height * 2},
                                SDL_Point{map_width * 3 + 200, map_height * 2}}) {
            for (int dy = -1; dy <= 1; ++dy) {
                for (int dx = -1; dx <= 1; ++dx) {
                    if (dx == 0 && dy == 0) continue;
                    prepare_game_view(window.get(), renderer.get());
                    SDL_SetWindowSize(window.get(), size.x, size.y);
                    SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                    yipe::GameState game;
                    game.location.position = {10, 5};
                    // Unequal distances still select a single diagonal step, not a path.
                    const int x = dx < 0 ? 0 : dx > 0 ? 12 : 10;
                    const int y = dy < 0 ? 0 : dy > 0 ? 11 : 5;
                    push_mouse(x * tile_size + 16, y * tile_size + 16);
                    const auto moved = run_script(game, "mouse-direction");
                    require(moved.location.map_id == 0x11
                                && moved.location.position == yipe::MapPosition{10 + dx, 5 + dy},
                            "mouse direction did not take exactly one step at the rendered scale");
                }
            }
            prepare_game_view(window.get(), renderer.get());
            SDL_SetWindowSize(window.get(), size.x, size.y);
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            yipe::GameState idle;
            idle.location.position = {10, 5};
            push_mouse(10 * tile_size, 5 * tile_size);
            push_mouse(11 * tile_size - 1, 6 * tile_size - 1);
            push_mouse(-10, 5 * tile_size);
            push_mouse(map_width + 10, 5 * tile_size);
            push_mouse(10 * tile_size, -10);
            push_mouse(10 * tile_size, map_height + 10);
            push_mouse(8 * tile_size, 5 * tile_size, SDL_BUTTON_RIGHT);
            push_mouse(8 * tile_size, 5 * tile_size, SDL_BUTTON_MIDDLE);
            push_mouse(8 * tile_size, 5 * tile_size, SDL_BUTTON_LEFT, SDL_MOUSEBUTTONDOWN);
            const auto ignored = run_script(idle, "mouse-ignored");
            require(ignored.location.position == idle.location.position
                        && ignored.random_state == idle.random_state,
                    "own-tile, outside-map, or non-left-release mouse input advanced the game");

            prepare_startup_view(window.get(), renderer.get());
            SDL_SetWindowSize(window.get(), size.x, size.y);
            for (const auto [modifiers, repeat, expected] : {
                     std::tuple{KMOD_LCTRL | KMOD_LSHIFT, false, StartupChoice::page_setup},
                     {KMOD_RCTRL | KMOD_RSHIFT | KMOD_CAPS | KMOD_NUM, false, StartupChoice::page_setup},
                     {static_cast<int>(KMOD_LCTRL), false, StartupChoice::quit},
                     {static_cast<int>(KMOD_LSHIFT), false, StartupChoice::quit},
                     {KMOD_LCTRL | KMOD_LSHIFT, true, StartupChoice::quit},
                     {KMOD_LCTRL | KMOD_LSHIFT | KMOD_LALT, false, StartupChoice::quit},
                     {KMOD_LCTRL | KMOD_LSHIFT | KMOD_LGUI, false, StartupChoice::quit},
                     {KMOD_LCTRL | KMOD_LSHIFT | KMOD_MODE, false, StartupChoice::quit}}) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                push_key(SDLK_p, repeat, modifiers);
                push_key(SDLK_ESCAPE);
                require(run_startup_menu(renderer.get(), menu.get(), copyright.get()) == expected,
                        "startup page setup ignored modifiers/repeats or offered printing without a game");
                ++cases;
            }
            for (const auto [y, choice] : {std::pair{280, StartupChoice::new_game},
                                           std::pair{315, StartupChoice::load_game},
                                           std::pair{345, StartupChoice::quit}}) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                push_mouse(150, y);
                push_key(SDLK_ESCAPE);
                require(run_startup_menu(renderer.get(), menu.get(), copyright.get()) == choice
                            && SDL_HasEvent(SDL_KEYDOWN),
                        "startup mouse selection did not use SDL logical coordinates");
                ++cases;
            }
            for (bool silly : {false, true}) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                if (silly) push_key(SDLK_s);
                push_mouse(240, silly ? 100 : 190);
                push_key(SDLK_ESCAPE);
                require(choose_portrait(window.get(), renderer.get(), textures) == (silly ? 12 : 8),
                        "portrait mouse selection did not use SDL logical coordinates");
                ++cases;
            }
            prepare_game_view(window.get(), renderer.get());
            SDL_SetWindowSize(window.get(), size.x, size.y);
            for (int action = 1; action <= 6; ++action) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                push_mouse(300, 155 + 33 * (action - 1));
                push_key(SDLK_ESCAPE);
                require(choose_combat_action(renderer.get(), "Mouse input", settings) == action
                            && SDL_HasEvent(SDL_KEYDOWN),
                        "combat mouse selection did not use SDL logical coordinates");
                ++cases;
            }
            const std::string original_title = SDL_GetWindowTitle(window.get());
            yipe::Player status_player;
            const auto status_scores_path = test_directory / "status-highscores.y3r";
            auto status_scores = yipe::default_high_scores();
            const auto show_status = [&] {
                show_player_status(window.get(), renderer.get(), textures, status_player,
                                   status_scores_path, status_scores);
                require(SDL_GetWindowTitle(window.get()) == original_title,
                        "statistics did not restore the game window title");
                ++cases;
            };
            const auto portrait_pixels = [&] {
                int x1, y1, x2, y2, output_width, output_height, window_width, window_height;
                SDL_RenderLogicalToWindow(renderer.get(), 24, 46, &x1, &y1);
                SDL_RenderLogicalToWindow(renderer.get(), 88, 110, &x2, &y2);
                SDL_GetWindowSize(window.get(), &window_width, &window_height);
                require(SDL_GetRendererOutputSize(renderer.get(), &output_width, &output_height) == 0,
                        "could not get statistics renderer size");
                const SDL_Rect region{x1 * output_width / window_width,
                                      y1 * output_height / window_height,
                                      (x2 - x1) * output_width / window_width,
                                      (y2 - y1) * output_height / window_height};
                std::vector<Uint32> pixels(static_cast<std::size_t>(region.w * region.h));
                require(SDL_RenderReadPixels(renderer.get(), &region, SDL_PIXELFORMAT_RGBA8888,
                                            pixels.data(), region.w * sizeof(Uint32)) == 0,
                        "could not read statistics portrait");
                return pixels;
            };
            for (int portrait = 0; portrait <= 13; ++portrait) {
                status_player.inventory[28] = static_cast<std::uint16_t>(portrait);
                const SDL_Rect image{24, 46, 64, 64};
                SDL_SetRenderDrawColor(renderer.get(), 224, 216, 184, 255);
                SDL_RenderFillRect(renderer.get(), &image);
                copy_texture(renderer.get(), textures.find(
                    405 + (portrait >= 1 && portrait <= 12 ? portrait : 1)), nullptr, &image);
                const auto expected_pixels = portrait_pixels();
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                for (Uint8 kind : {SDL_WINDOWEVENT_EXPOSED, SDL_WINDOWEVENT_FOCUS_LOST,
                                    SDL_WINDOWEVENT_FOCUS_GAINED, SDL_WINDOWEVENT_SIZE_CHANGED}) {
                    SDL_Event event{};
                    event.type = SDL_WINDOWEVENT;
                    event.window.windowID = SDL_GetWindowID(window.get());
                    event.window.event = kind;
                    require(SDL_PushEvent(&event) == 1, "could not queue statistics redraw");
                }
                push_key(SDLK_ESCAPE);
                show_status();
                require(portrait_pixels() == expected_pixels,
                        ("statistics portrait " + std::to_string(portrait)
                            + " differed at width " + std::to_string(size.x)).c_str());
            }
            for (int action = 0; action < 6; ++action) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                messages.clear();
                message_texts.clear();
                if (action == 1) dialog_choices = {{"Monster Kills", 0, "Page 1 of 5"}};
                if (action == 2) dialog_choices = {{"High Scores", 0, "Medium Mode"}};
                push_mouse(30 + (action % 3) * 126, 300 + (action / 3) * 35);
                push_key(SDLK_ESCAPE);
                show_status();
                const std::array<std::string, 3> titles{
                    "Special Statistics", "Equipment Statistics", "Player Status Details"};
                require(dialog_choices.empty()
                            && messages == (action >= 3 ? std::vector<std::string>{titles[action - 3]}
                                                       : std::vector<std::string>{})
                            && (action != 0 || SDL_HasEvent(SDL_KEYDOWN)),
                        "statistics mouse buttons did not open/return from the selected page");
            }
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            messages.clear();
            push_mouse(-10, 300);
            push_mouse(map_width + 10, 300);
            push_mouse(300, 335, SDL_BUTTON_RIGHT);
            push_mouse(300, 335, SDL_BUTTON_LEFT, SDL_MOUSEBUTTONDOWN);
            push_key(SDLK_d, true);
            push_key(SDLK_d, false, KMOD_RCTRL);
            push_key(SDLK_d, false, KMOD_LALT);
            push_key(SDLK_d, false, KMOD_LGUI);
            push_key(SDLK_RETURN);
            push_key(SDLK_ESCAPE);
            show_status();
            require(messages.empty() && SDL_HasEvent(SDL_KEYDOWN),
                    "statistics accepted an outside/non-left click or a repeated key");

            status_player.name = "Full name \xc3\xa9 " + std::string(120, 'x');
            status_player.level = 4;
            status_player.experience = 123;
            status_player.gold = 456;
            status_player.health = 7;
            status_player.maximum_health = 34;
            status_player.spell_points = 8;
            status_player.maximum_spell_points = 20;
            status_player.ability_points = 2;
            status_player.maximum_ability_points = 3;
            status_player.strength = 17;
            status_player.dexterity = 12;
            status_player.intelligence = 9;
            status_player.inventory.fill(0);
            status_player.inventory[0] = 1;
            status_player.inventory[12] = 10;
            status_player.inventory[13] = 1;
            const std::string expected_values =
                "Level 4  XP 123\nGold 456\nHealth Points 7/34\nSpell Points 8/20\n"
                "Ability Points 2/3\nStrength 17  Dexterity 12\nIntelligence 9\n"
                "Stat limit 50\nWC 2  AC 3\nCarried items 2/13";
            require(player_status_values(status_player) == expected_values,
                    "statistics values omitted or changed a numeric field");
            for (const auto& keys : {std::vector<SDL_Keycode>{SDLK_d},
                                     std::vector<SDL_Keycode>{SDLK_LEFT, SDLK_RETURN},
                                     std::vector<SDL_Keycode>{SDLK_DOWN, SDLK_RIGHT, SDLK_RIGHT,
                                                             SDLK_KP_ENTER}}) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                messages.clear();
                message_texts.clear();
                for (auto key : keys) push_key(key);
                push_key(SDLK_ESCAPE);
                show_status();
                require(messages == std::vector<std::string>{"Player Status Details"}
                            && message_texts == std::vector<std::string>{
                                status_player.name + "\n\n" + expected_values},
                        "statistics keyboard navigation lost the complete name or numeric details");
            }
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            messages.clear();
            for (int shift : {KMOD_LSHIFT, KMOD_RSHIFT}) {
                push_key(SDLK_TAB, false, shift | KMOD_CAPS | KMOD_NUM);
                push_key(SDLK_RETURN);
                push_key(SDLK_RIGHT);
            }
            push_key(SDLK_ESCAPE);
            show_status();
            require(messages == std::vector<std::string>{"Player Status Details", "Player Status Details"},
                    "reverse statistics navigation lost a Shift side or treated lock keys as commands");
            SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
            messages.clear();
            dialog_choices = {{"Monster Kills", 0, "Page 1 of 5"},
                              {"Monster Kills", 0, "Page 1 of 5"},
                              {"High Scores", 0, "Medium Mode"}};
            push_key(SDLK_TAB);
            push_key(SDLK_RETURN);
            push_key(SDLK_k);
            push_key(SDLK_h);
            push_key(SDLK_s);
            push_key(SDLK_e);
            push_key(SDLK_b);
            auto expected_scores = status_scores;
            require(yipe::update_high_scores(expected_scores, status_player, false),
                    "statistics score fixture did not qualify");
            show_status();
            require(dialog_choices.empty()
                        && messages == std::vector<std::string>{"Special Statistics", "Equipment Statistics"}
                        && status_scores == expected_scores
                        && yipe::load_high_scores(status_scores_path) == status_scores,
                    "statistics navigation or high-score submission changed");
            for (bool close_window : {false, true}) {
                SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
                SDL_Event event{};
                event.type = close_window ? SDL_WINDOWEVENT : SDL_QUIT;
                event.window.event = SDL_WINDOWEVENT_CLOSE;
                require(SDL_PushEvent(&event) == 1, "could not queue statistics close");
                show_status();
                require(SDL_HasEvent(SDL_QUIT), "statistics swallowed a quit request");
            }
        }
        prepare_game_view(window.get(), renderer.get());
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        // Compare complete resulting snapshots: mouse movement must dispatch the same
        // collision, shop, transition, chest, and keyed-door behavior as keyboard input.
        for (int scenario = 0; scenario < 6; ++scenario) {
            yipe::GameState game;
            yipe::MapPosition clicked;
            SDL_Keycode key = SDLK_UP;
            switch (scenario) {
            case 0:
                game.location.position = {1, 0};
                clicked = {0, 0};
                key = SDLK_LEFT;
                break;
            case 1:
                game.location.position = {6, 5};
                clicked = {6, 6};
                key = SDLK_DOWN;
                break;
            case 2:
                game.location.position = {1, 2};
                clicked = {1, 1};
                break;
            case 3:
                game.location = {{2, 7}, 0, 0, 1, false, 0x39};
                clicked = {2, 8};
                key = SDLK_DOWN;
                break;
            default:
                game.location = {{1, 3}, -1, -3, 1, false, 0x4a};
                game.player.has_rainbow_key = scenario == 5;
                clicked = {1, 2};
                break;
            }
            std::vector<std::string> mouse_messages;
            for (bool mouse : {true, false}) {
                messages.clear();
                if (scenario == 1) dialog_choices = {{"Fred's Miscellaneous Shop", 0, ""}};
                if (mouse) push_mouse(clicked.x * tile_size + 16, clicked.y * tile_size + 16);
                else push_key(key);
                static_cast<void>(run_script(game, mouse ? "mouse-action" : "keyboard-action"));
                require(dialog_choices.empty(), "mouse/keyboard action left an unconsumed dialog");
                if (mouse) mouse_messages = messages;
                else require(messages == mouse_messages, "mouse and keyboard dispatched different actions");
            }
            std::ifstream mouse_file(test_directory / "mouse-action.y3r", std::ios::binary);
            std::ifstream keyboard_file(test_directory / "keyboard-action.y3r", std::ios::binary);
            require(mouse_file.good() && keyboard_file.good(), "mouse action snapshots are missing");
            const std::string mouse_bytes{std::istreambuf_iterator<char>(mouse_file), {}};
            const std::string keyboard_bytes{std::istreambuf_iterator<char>(keyboard_file), {}};
            require(mouse_bytes == keyboard_bytes, "mouse action changed gameplay relative to keyboard input");
        }
        const auto walk_route = [&](std::string_view directions) {
            for (char direction : directions) {
                switch (direction) {
                case '1': push_key(SDLK_KP_1); break;
                case '2': push_key(SDLK_DOWN); break;
                case '3': push_key(SDLK_KP_3); break;
                case '4': push_key(SDLK_LEFT); break;
                case '6': push_key(SDLK_RIGHT); break;
                case '7': push_key(SDLK_KP_7); break;
                case '8': push_key(SDLK_UP); break;
                case '9': push_key(SDLK_KP_9); break;
                default: throw std::runtime_error("invalid fresh-route direction");
                }
            }
        };
        const auto heal_at_shop = [&](int apples) {
            for (int i = 0; i < apples; ++i) {
                dialog_choices.push_back({"Heal Shop", 1, "Tasty Apple"});
            }
            dialog_choices.push_back({"Heal Shop", 0, ""});
        };
        // Both walks start/end beside the town's healing shop at (5,4).
        constexpr std::string_view border_loop = "88888886242222226";
        constexpr std::string_view field_loop = "88888888866222442222226";
        const auto train_route = [&](int trips, std::string_view path, int apples, int ticks) {
            for (int trip = 0; trip < trips; ++trip) {
                if (ticks > 0) push_wait(ticks);
                walk_route(path);
                heal_at_shop(apples);
            }
            push_key(SDLK_F5);
        };
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        route_combat = true;
        messages.clear();
        message_texts.clear();
        // Observation saves never feed state back into this single run_game invocation.
        push_key(SDLK_F5);
        // The first cave's two early chests fund food and rank-three equipment.
        walk_route("7777");
        walk_route("3222222");
        push_key(SDLK_F5);
        dialog_choices = {{"Inventory", 1, "French Fries"}, {"Inventory", 1, "French Fries"}};
        walk_route("99896966698");
        walk_route("9");
        push_key(SDLK_F5);
        walk_route("12144444777");
        walk_route("33366");
        heal_at_shop(3);
        walk_route("369");
        dialog_choices.push_back({"Weapon Shop", 3, ""});
        dialog_choices.push_back({"Weapon Shop", 2, ""});
        dialog_choices.push_back({"Weapon Shop", 1, "(rank 3)"});
        dialog_choices.push_back({"Weapon Shop", 0, ""});
        walk_route("3");
        dialog_choices.push_back({"Armor Shop", 3, ""});
        dialog_choices.push_back({"Armor Shop", 2, ""});
        dialog_choices.push_back({"Armor Shop", 1, "(rank 3)"});
        dialog_choices.push_back({"Armor Shop", 0, ""});
        walk_route("1");
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        push_key(SDLK_F5);
        // Defeat the north-field beetle and use its chest's strength potion.
        walk_route("4488888888884442221");
        push_key(SDLK_F5);
        push_key(SDLK_i);
        dialog_choices.push_back({"Inventory", 2, "Juicy Cheeseburger"});
        dialog_choices.push_back({"Inventory", 2, "French Fries"});
        dialog_choices.push_back({"Inventory", 1, "Strength Potion"});
        push_key(SDLK_i);
        dialog_choices.push_back({"Inventory", 1, "Juicy Cheeseburger"});
        dialog_choices.push_back({"Inventory", 1, "French Fries"});
        walk_route("9889662222222226");
        heal_at_shop(4);
        walk_route("32");
        for (int i = 0; i < 7; ++i) dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, ""});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "Wet Mittens"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, ""});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, ""});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "Sandals"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        walk_route("7");
        push_key(SDLK_F5);
        train_route(6, field_loop, 6, 0);
        walk_route("32");
        for (int i = 0; i < 8; ++i) dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, ""});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "Cheap Ring"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, ""});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, ""});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "Plain Necklace"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        walk_route("7");
        train_route(40, border_loop, 6, 0);
        walk_route("369");
        dialog_choices.push_back({"Weapon Shop", 3, ""});
        dialog_choices.push_back({"Weapon Shop", 2, ""});
        dialog_choices.push_back({"Weapon Shop", 2, ""});
        dialog_choices.push_back({"Weapon Shop", 1, "(rank 4)"});
        dialog_choices.push_back({"Weapon Shop", 0, ""});
        walk_route("47");
        train_route(70, border_loop, 8, 0);
        walk_route("333333");
        dialog_choices.push_back({"Magic Shop", 3, "Small Heal"});
        dialog_choices.push_back({"Magic Shop", 0, ""});
        walk_route("778");
        dialog_choices.push_back({"Armor Shop", 3, ""});
        dialog_choices.push_back({"Armor Shop", 2, ""});
        dialog_choices.push_back({"Armor Shop", 2, ""});
        dialog_choices.push_back({"Armor Shop", 1, "(rank 4)"});
        dialog_choices.push_back({"Armor Shop", 0, ""});
        walk_route("777");
        train_route(30, border_loop, 8, 532);
        walk_route("32");
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        walk_route("7");
        train_route(8, field_loop, 10, 532);
        const auto rest_route = [&] {
            // Real exploration ticks regenerate SP; all three heal casts consume RNG/SP.
            push_wait(760);
            for (int i = 0; i < 3; ++i) {
                push_key(SDLK_c);
                dialog_choices.push_back({"Spells", 3, "Return to Surface"});
                dialog_choices.push_back({"Spells", 1, "Small Heal"});
            }
            push_wait(760);
        };
        // Clear more chests on 0x39/0x3f and search the 180-gold coin pile on 0x45.
        push_wait(532);
        walk_route("27777");
        walk_route("333" "666666" "31" "4444");
        push_key(SDLK_F5);
        rest_route();
        walk_route("2222444442");
        walk_route("6666");
        walk_route("2398");
        push_key(SDLK_F5);
        rest_route();
        walk_route("23333");
        walk_route("44444" "13" "66666");
        push_key(SDLK_F5);
        rest_route();
        walk_route("112");
        walk_route("22222266222");
        SDL_Event other{};
        other.type = SDL_KEYDOWN;
        other.key.keysym.sym = SDLK_o;
        other.key.keysym.mod = KMOD_CTRL | KMOD_SHIFT;
        require(SDL_PushEvent(&other) == 1, "could not queue cave treasure search");
        push_key(SDLK_F5);
        push_wait(532);
        push_key(SDLK_c);
        dialog_choices.push_back({"Spells", 1, "Return to Surface"});
        walk_route("63366");
        heal_at_shop(4);
        walk_route("369");
        dialog_choices.push_back({"Weapon Shop", 3, ""});
        for (int i = 0; i < 5; ++i) dialog_choices.push_back({"Weapon Shop", 2, ""});
        dialog_choices.push_back({"Weapon Shop", 1, "(rank 7)"});
        dialog_choices.push_back({"Weapon Shop", 0, ""});
        walk_route("3");
        dialog_choices.push_back({"Armor Shop", 3, ""});
        for (int i = 0; i < 3; ++i) dialog_choices.push_back({"Armor Shop", 2, ""});
        dialog_choices.push_back({"Armor Shop", 1, "(rank 5)"});
        dialog_choices.push_back({"Armor Shop", 0, ""});
        walk_route("1");
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        walk_route("47");
        push_key(SDLK_F5);
        train_route(20, field_loop, 16, 836);
        // Buy the prerequisite Gobbler spell, Insect Attack, armor, and food with earned gold.
        walk_route("333333");
        dialog_choices.push_back({"Magic Shop", 3, "Gobbler Attack"});
        dialog_choices.push_back({"Magic Shop", 3, "Insect Attack"});
        dialog_choices.push_back({"Magic Shop", 0, ""});
        walk_route("778");
        dialog_choices.push_back({"Armor Shop", 3, ""});
        for (int i = 0; i < 4; ++i) dialog_choices.push_back({"Armor Shop", 2, ""});
        dialog_choices.push_back({"Armor Shop", 1, "(rank 6)"});
        dialog_choices.push_back({"Armor Shop", 0, ""});
        walk_route("74");
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        walk_route("77");
        push_key(SDLK_F5);
        // Pass the Mouth Men on 0x17 and the first Stone Golem in cave 0x3a.
        rest_route();
        walk_route("2" "112222" "3333336");
        push_key(SDLK_F5);
        rest_route();
        walk_route("69998879989998");
        push_key(SDLK_F5);
        rest_route();
        walk_route("787");
        push_key(SDLK_F5);
        // Return for food via the temporary flower passage. Reenter by the southern route:
        // the mountains reset when leaving 0x12, while defeated authored guards stay cleared.
        rest_route();
        push_key(SDLK_c);
        dialog_choices.push_back({"Spells", 1, "Return to Surface"});
        walk_route("11");
        require(SDL_PushEvent(&other) == 1, "could not queue flower passage search");
        walk_route("44444" "77441");
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "Juicy Cheeseburger"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 3, "Juicy Cheeseburger"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        push_key(SDLK_F5);
        walk_route("47" "2" "112222" "3333336" "69998879989998" "787");
        rest_route();
        // Defeat the paired golems, enter 0x33, and choose the princess's Mystery reward.
        walk_route("447" "666699");
        push_key(SDLK_F5);
        rest_route();
        walk_route("74447777" "1147744");
        dialog_choices.push_back({"Rescued Princess", 4, ""});
        push_key(SDLK_F5);
        // Continue from slot 2: collect its chest and use the three earned stat potions.
        walk_route("88");
        push_key(SDLK_F5);
        for (const auto potion : {"Intelligence Potion", "Dexterity Potion", "Intelligence Potion"}) {
            push_key(SDLK_i);
            dialog_choices.push_back({"Inventory", 1, potion});
        }
        push_key(SDLK_c);
        dialog_choices.push_back({"Spells", 1, "Return to Surface"});
        walk_route("11");
        require(SDL_PushEvent(&other) == 1, "could not queue return flower passage");
        walk_route("44444" "77441");
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "Juicy Cheeseburger"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        // Descend through 0x39/0x3f/0x45, collect both 0x45 chests, and enter 0x4b.
        walk_route("447777");
        walk_route("333222222441" "33333133632");
        push_key(SDLK_F5);
        rest_route();
        walk_route("222447744422226");
        push_key(SDLK_F5);
        rest_route();
        walk_route("66822");
        push_key(SDLK_F5);
        walk_route("47123396993222");
        push_key(SDLK_F5);
        rest_route();
        walk_route("44222444");
        push_key(SDLK_F5);
        rest_route();
        walk_route("4478966");
        dialog_choices.push_back({"Special Ability Training Center", 1, "Backstab\nPrice: 150 gold"});
        push_key(SDLK_F5);
        // Collect the 0x4b chest and all three 0x73 chests before reaching 0x6d.
        rest_route();
        walk_route("11266666226662266");
        push_key(SDLK_F5);
        rest_route();
        walk_route("4444422444478866");
        push_key(SDLK_F5);
        rest_route();
        walk_route("3666662266");
        push_key(SDLK_F5);
        rest_route();
        walk_route("77888866");
        push_key(SDLK_F5);
        rest_route();
        walk_route("7744444411");
        push_key(SDLK_F5);
        rest_route();
        walk_route("9997");
        push_key(SDLK_F5);
        push_key(SDLK_c);
        dialog_choices.push_back({"Spells", 1, "Return to Surface"});
        // Twenty town-to-bat outings fund level-nine equipment before facing Wormoid.
        for (int trip = 0; trip < 20; ++trip) {
            push_wait(114);
            walk_route("8" "33366666688");
            rest_route();
            push_key(SDLK_c);
            dialog_choices.push_back({"Spells", 1, "Return to Surface"});
        }
        push_key(SDLK_F5);
        walk_route("6333669");
        dialog_choices.push_back({"Weapon Shop", 3, ""});
        for (int i = 0; i < 8; ++i) dialog_choices.push_back({"Weapon Shop", 2, ""});
        dialog_choices.push_back({"Weapon Shop", 1, "(rank 10)"});
        dialog_choices.push_back({"Weapon Shop", 0, ""});
        walk_route("3");
        dialog_choices.push_back({"Armor Shop", 3, ""});
        for (int i = 0; i < 6; ++i) dialog_choices.push_back({"Armor Shop", 2, ""});
        dialog_choices.push_back({"Armor Shop", 1, "(rank 8)"});
        dialog_choices.push_back({"Armor Shop", 0, ""});
        walk_route("1");
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 2, "French Fries"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "Juicy Cheeseburger"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 1, "Juicy Cheeseburger"});
        dialog_choices.push_back({"Fred's Miscellaneous Shop", 0, ""});
        push_key(SDLK_F5);
        // Revisit the cleared caves, defeat Wormoid at (5,5), and rescue slot 1 at (8,8).
        rest_route();
        walk_route("447777" "333222222441");
        rest_route();
        walk_route("33333133632");
        rest_route();
        walk_route("2222222222");
        rest_route();
        walk_route("133211111444799");
        rest_route();
        walk_route("933999777477");
        rest_route();
        push_key(SDLK_F5);
        walk_route("8888866");
        push_key(SDLK_F5);
        rest_route();
        walk_route("66223");
        dialog_choices.push_back({"Rescued Princess", 4, ""});
        status_bar_visible = true;
        yipe::GameState completed_route;
        if (full_game_mode) {
            full_game_continuing = true;
            push_key(SDLK_F5);
            auto path = test_directory / "fresh-route.y3r";
            const auto exit = run_game(window.get(), renderer.get(), textures, {}, path,
                test_directory / "settings.y3r", test_directory / "highscores.y3r", scores, settings, audio);
            require(exit.choice == StartupChoice::quit && full_game_live,
                    "full-game driver exited before reaching its continuation");
            const auto final_game = yipe::load_game(path);
            if (!checkpoint_mode) check_full_game_completion(final_game, 3, 2);
            full_game_state(final_game);
            full_game_log(checkpoint_mode
                ? "CHECKPOINT SAVED; checking the unchanged opening route next."
                : "FINALE VERIFIED; checking the unchanged opening route next.");
            completed_route = route_checkpoints.at(41);
            route_checkpoints.resize(42);
            messages = full_game_opening_messages;
            message_texts = full_game_opening_texts;
        } else {
            completed_route = run_script({}, "fresh-route");
        }
        status_bar_visible = false;
        route_combat = false;
        route_player = nullptr;
        require(dialog_choices.empty(), "fresh route left unconsumed dialog choices");
        struct RouteCheckpoint {
            int map;
            yipe::MapPosition position;
            int level;
            int gold;
            int weapon;
            int armor;
        };
        constexpr std::array<RouteCheckpoint, 42> expected_checkpoints{{
            {0x11, {5, 5}, 1, 0, 2, 2},
            {0x39, {2, 8}, 1, 53, 2, 2},
            {0x39, {11, 1}, 1, 216, 2, 2},
            {0x11, {7, 5}, 1, 7, 3, 3},
            {0x0b, {1, 9}, 2, 175, 3, 3},
            {0x11, {5, 4}, 2, 119, 3, 3},
            {0x11, {5, 4}, 2, 227, 3, 3},
            {0x11, {5, 4}, 3, 214, 3, 3},
            {0x11, {5, 4}, 4, 290, 4, 3},
            {0x11, {5, 4}, 4, 176, 4, 4},
            {0x11, {5, 4}, 5, 359, 4, 4},
            {0x39, {6, 6}, 5, 507, 4, 4},
            {0x3f, {7, 1}, 5, 677, 4, 4},
            {0x3f, {11, 8}, 5, 848, 4, 4},
            {0x45, {11, 10}, 5, 1028, 4, 4},
            {0x11, {5, 4}, 5, 63, 7, 5},
            {0x11, {5, 4}, 7, 869, 7, 5},
            {0x11, {5, 4}, 7, 19, 7, 6},
            {0x17, {10, 7}, 7, 205, 7, 6},
            {0x3a, {7, 4}, 7, 205, 7, 6},
            {0x3a, {5, 1}, 7, 326, 7, 6},
            {0x11, {7, 5}, 7, 1, 7, 6},
            {0x34, {8, 8}, 8, 247, 7, 6},
            {0x33, {4, 3}, 8, 349, 7, 6},
            {0x33, {4, 1}, 8, 434, 7, 6},
            {0x45, {9, 1}, 8, 265, 7, 6},
            {0x45, {3, 6}, 8, 463, 7, 6},
            {0x45, {5, 7}, 8, 615, 8, 6},
            {0x4b, {9, 1}, 8, 691, 8, 6},
            {0x4b, {4, 4}, 8, 830, 8, 6},
            {0x4b, {3, 1}, 8, 728, 8, 6},
            {0x4b, {11, 8}, 8, 844, 8, 6},
            {0x73, {3, 7}, 8, 991, 8, 6},
            {0x73, {11, 10}, 8, 1138, 8, 6},
            {0x73, {11, 4}, 8, 1293, 8, 6},
            {0x73, {1, 4}, 8, 1472, 8, 7},
            {0x6d, {3, 10}, 8, 1472, 8, 7},
            {0x11, {1, 2}, 9, 2671, 8, 7},
            {0x11, {7, 5}, 9, 721, 10, 8},
            {0x6d, {3, 10}, 9, 794, 10, 8},
            {0x6d, {5, 5}, 9, 1378, 10, 8},
            {0x6d, {8, 8}, 10, 1738, 10, 8},
        }};
        require(route_checkpoints.size() == expected_checkpoints.size(),
                "fresh route recorded the wrong number of observation checkpoints");
        for (std::size_t i = 0; i < expected_checkpoints.size(); ++i) {
            const auto& expected = expected_checkpoints[i];
            const auto& actual = route_checkpoints[i];
            const auto& player = actual.player;
            if (actual.location.map_id != expected.map
                || actual.location.position != expected.position
                || player.level != expected.level || player.gold != expected.gold
                || player.weapon_rank != expected.weapon || player.armor_rank != expected.armor) {
                std::ostringstream error;
                error << "fresh-route checkpoint " << i << ": got map " << std::hex
                      << actual.location.map_id << std::dec << " at "
                      << actual.location.position.x << ',' << actual.location.position.y
                      << ", level " << player.level << ", gold " << player.gold
                      << ", weapon/armor " << player.weapon_rank << '/' << player.armor_rank;
                throw std::runtime_error(error.str());
            }
            require(player.death_count == 0 && player.health > 0
                        && player.specialty == 3 && player.difficulty == 2
                        && player.stat_cap_mode == 2 && player.random_encounter_threshold == 85
                        && player.monster_repellent_steps == 0,
                    "fresh route changed its starting rules or failed to survive");
        }
        const auto& start = route_checkpoints.front();
        const auto& fresh_route = route_checkpoints[23];
        require(start.random_state == 1 && start.player.experience == 0
                    && start.player.strength == 16 && start.player.dexterity == 13
                    && start.player.intelligence == 10 && start.player.health == 13
                    && start.player.spell_points == 10 && start.player.ability_points == 5
                    && start.player.inventory[0] == 1
                    && std::count(start.player.inventory.begin(), start.player.inventory.end(), 0) == 29
                    && start.player.rescued_princess_count() == 0
                    && start.defeated_monster_placements.empty() && start.map_tile_changes.empty(),
                "fresh route did not begin with the default Combat character");
        require(route_checkpoints[5].player.strength == route_checkpoints[4].player.strength + 1
                    && route_checkpoints[5].player.boots_rank == 1
                    && route_checkpoints[5].player.gloves_rank == 1
                    && route_checkpoints[7].player.ring_rank >= 1
                    && route_checkpoints[7].player.necklace_rank >= 1
                    && route_checkpoints[9].player.spells[1] == 1
                    && route_checkpoints[14].player.cave_coins_collected
                    && route_checkpoints[17].player.spells[2] == 1
                    && route_checkpoints[17].player.spells[3] == 1,
                "fresh route failed to earn its potion, accessories, spells, or cave coins");
        constexpr std::array<std::pair<int, yipe::MapPosition>, 14> route_chests{{
            {0x39, {2, 8}}, {0x39, {11, 1}}, {0x0b, {1, 9}}, {0x39, {6, 6}},
            {0x3f, {5, 1}}, {0x3f, {7, 1}}, {0x3f, {11, 8}},
            {0x33, {4, 1}}, {0x45, {5, 5}}, {0x45, {5, 7}}, {0x4b, {11, 8}},
            {0x73, {11, 10}}, {0x73, {11, 4}}, {0x73, {1, 4}},
        }};
        std::array<std::uint8_t, 128> opened_chests{};
        for (std::size_t i = 0; i < route_chests.size(); ++i) {
            const auto& [map, position] = route_chests[i];
            const auto chests = yipe::original_chests_for_map(map);
            const auto found = std::find(chests.begin(), chests.end(), position);
            require(found != chests.end(), "fresh-route chest is not an original placement");
            opened_chests[static_cast<std::size_t>(map - 1)] |=
                static_cast<std::uint8_t>(1u << std::distance(chests.begin(), found));
            if (i == 6) {
                require(fresh_route.player.opened_chests == opened_chests,
                        "fresh route collected the wrong chests before its first rescue");
            }
        }
        require(completed_route.player.opened_chests == opened_chests,
                "fresh route collected the wrong chests");
        constexpr std::array<std::pair<int, yipe::MapPosition>, 3> route_guards{{
            {0x17, {10, 7}}, {0x3a, {5, 1}}, {0x34, {8, 8}},
        }};
        for (const auto& [map, position] : route_guards) {
            const auto placements = yipe::original_monsters_for_map(map);
            const auto found = std::find_if(placements.begin(), placements.end(),
                [&](const auto& placement) { return placement.position == position; });
            require(found != placements.end(), "fresh-route guard is not an original placement");
            require(std::find(fresh_route.defeated_monster_placements.begin(),
                              fresh_route.defeated_monster_placements.end(), found->placement_index)
                        != fresh_route.defeated_monster_placements.end(),
                    "fresh route did not defeat a required guard");
        }
        require(fresh_route.player.inventory_values[2] == 1
                    && fresh_route.player.princesses_found == 1
                    && fresh_route.player.rescued_princess_count() == 1
                    && fresh_route.player.princess_mystery_uses == 1
                    && fresh_route.player.health == 59 && fresh_route.player.maximum_health == 59
                    && fresh_route.player.spell_points == fresh_route.player.maximum_spell_points
                    && !fresh_route.player.has_rainbow_key && !fresh_route.player.has_underworld_key,
                "fresh route did not rescue princess two and receive the Mystery healing reward");
        const auto first_reward = std::find(messages.begin(), messages.end(), "Princess Reward");
        require(std::count(messages.begin(), first_reward, "Victory") == 71
                    && std::count(messages.begin(), first_reward, "Treasure Chest") == 7
                    && std::none_of(message_texts.begin(), message_texts.end(), [](const auto& text) {
                        return text.find("don't have enough") != std::string::npos;
                    }),
                "fresh route missed encounters/rewards or attempted an unfunded purchase");
        require(route_checkpoints[25].player.strength == route_checkpoints[24].player.strength
                    && route_checkpoints[25].player.dexterity == route_checkpoints[24].player.dexterity + 1
                    && route_checkpoints[25].player.intelligence == route_checkpoints[24].player.intelligence + 2
                    && route_checkpoints[29].player.special_ability == yipe::SpecialAbility::none
                    && route_checkpoints[30].player.special_ability == yipe::SpecialAbility::backstab
                    && std::count(message_texts.begin(), message_texts.end(), "Ability training complete.") == 1,
                "fresh route did not use its earned potions or learn Backstab");
        const auto wormoid_placements = yipe::original_monsters_for_map(0x6d);
        const auto wormoid = std::find_if(wormoid_placements.begin(), wormoid_placements.end(),
            [](const auto& placement) {
                return placement.type_id == 35 && placement.position == yipe::MapPosition{5, 5};
            });
        require(wormoid != wormoid_placements.end(), "fresh-route Wormoid placement is missing");
        const auto& before_wormoid = route_checkpoints[39];
        const auto& after_wormoid = route_checkpoints[40];
        require(before_wormoid.player.monster_defeats[34] == 0
                    && after_wormoid.player.monster_defeats[34] == 1
                    && after_wormoid.player.has_defeated_wormoid
                    && before_wormoid.player.stamps_found == 0 && after_wormoid.player.stamps_found == 1
                    && std::find(after_wormoid.defeated_monster_placements.begin(),
                                 after_wormoid.defeated_monster_placements.end(), wormoid->placement_index)
                        != after_wormoid.defeated_monster_placements.end(),
                "fresh route did not permanently defeat Wormoid and earn its stamp");
        require(completed_route.player.inventory_values[1] == 1
                    && completed_route.player.inventory_values[2] == 1
                    && completed_route.player.princesses_found == 2
                    && completed_route.player.rescued_princess_count() == 2
                    && completed_route.player.princess_mystery_uses == 2
                    && completed_route.player.experience == 5008
                    && completed_route.player.health == 65 && completed_route.player.maximum_health == 71
                    && completed_route.player.spell_points == 18 && completed_route.player.maximum_spell_points == 54
                    && completed_route.player.strength == 19 && completed_route.player.dexterity == 15
                    && completed_route.player.intelligence == 12
                    && completed_route.player.special_ability == yipe::SpecialAbility::backstab
                    && completed_route.player.monster_defeats[34] == 1 && completed_route.player.stamps_found == 1
                    && completed_route.player.gatekeeper_state == 0
                    && !completed_route.player.has_rainbow_key && !completed_route.player.has_underworld_key,
                "fresh route did not finish its second rescue with the earned resources and stamp");
        require(std::count(messages.begin(), messages.end(), "Victory") == 116
                    && std::count(messages.begin(), messages.end(), "Treasure Chest") == 14
                    && std::count(messages.begin(), messages.end(), "Princess Reward") == 2
                    && std::count(message_texts.begin(), message_texts.end(),
                        "The Princess makes some kind of motion with her hand.  Your health and spell points have been healed.") == 1
                    && std::count(message_texts.begin(), message_texts.end(),
                        "The Princess gives you 300 gold pieces and wishes you luck in your journey.") == 1,
                "fresh route missed encounters, chests, or either Mystery reward");
        if (full_game_mode) {
            full_game_log(checkpoint_mode
                ? "PASS: unchanged opening and earned checkpoint saved and explicitly quit; completion not asserted."
                : "PASS: unchanged opening checkpoints and connected full-game finale verified.");
            return 0;
        }
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        yipe::GameState rider;
        rider.location = {{4, 9}, -1, -3, 1, false, 0x4a};
        auto expected_map = *yipe::original_map(0x4a);
        auto expected_position = rider.location.position;
        yipe::MapVisibility visible;
        yipe::initialize_map_visibility(visible, expected_map, expected_position);
        yipe::MsvcRandom expected_random(rider.random_state);
        for (int i = 0; i < 10; ++i) {
            static_cast<void>(expected_map.advance_overlay(visible, expected_position, expected_random));
            yipe::reveal_map_from(visible, expected_map, expected_position);
        }
        push_wait(30);
        const auto carried = run_script(rider, "idle-platform");
        if (carried.location.position != expected_position
            || carried.random_state != expected_random.state()) {
            throw std::runtime_error(
                "idle platform: expected " + std::to_string(expected_position.x) + ","
                + std::to_string(expected_position.y) + " RNG "
                + std::to_string(expected_random.state()) + ", got "
                + std::to_string(carried.location.position.x) + ","
                + std::to_string(carried.location.position.y) + " RNG "
                + std::to_string(carried.random_state));
        }
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        push_wait(3);
        const auto resumed_rider = run_script(carried, "resumed-platform");
        require(resumed_rider.location.map_id == 0x4a && resumed_rider.map_tile_changes.empty(),
                "resuming a platform rider persisted temporary terrain");

        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        yipe::GameState platform_route;
        platform_route.player.has_rainbow_key = true;
        platform_route.player.random_encounter_threshold = 32767;
        platform_route.location = {{9, 10}, -1, -2, 1, false, 0x44};
        for (const auto key : {SDLK_DOWN, SDLK_KP_1, SDLK_KP_1, SDLK_KP_1,
                               SDLK_KP_1, SDLK_DOWN, SDLK_DOWN, SDLK_DOWN, SDLK_KP_1}) {
            push_key(key);
        }
        platform_route = run_script(platform_route, "platform-route-boarded");
        require(platform_route.location.map_id == 0x4a
                    && platform_route.location.position == yipe::MapPosition{4, 9},
                "SDL route did not enter from 0x44 and board the platform");
        const auto ride_to = [&](yipe::GameState game, yipe::MapPosition target,
                                 const std::string& name) {
            auto map = *yipe::original_map(game.location.map_id);
            auto position = game.location.position;
            map.restore_overlay_for_saved_position(position);
            yipe::MapVisibility visibility;
            yipe::initialize_map_visibility(visibility, map, position);
            yipe::MsvcRandom random(game.random_state);
            yipe::ExplorationTimer timer;
            int ticks = 0;
            while (position != target && ticks < 10000) {
                ++ticks;
                if (timer.advance(game.player, map, position) % 3 == 0) {
                    static_cast<void>(map.advance_overlay(visibility, position, random));
                    yipe::reveal_map_from(visibility, map, position);
                }
            }
            require(position == target && ticks > 0, "platform route has no seeded landing");
            push_wait(ticks);
            const auto arrived = run_script(game, name);
            require(arrived.location.position == target && arrived.random_state == random.state(),
                    "SDL platform route failed to reach its landing after save/reload");
            return arrived;
        };
        platform_route = ride_to(platform_route, {3, 3}, "platform-route-landing");
        dialog_choices = {{"Rescued Princess", 3, ""}};
        for (const auto key : {SDLK_LEFT, SDLK_LEFT, SDLK_UP, SDLK_UP, SDLK_RIGHT,
                               SDLK_LEFT, SDLK_DOWN, SDLK_DOWN, SDLK_RIGHT, SDLK_RIGHT}) {
            push_key(key);
        }
        platform_route = run_script(platform_route, "platform-route-rescued");
        require(platform_route.player.inventory_values[11] == 1
                    && platform_route.player.princesses_found == 1
                    && platform_route.location.position == yipe::MapPosition{3, 3},
                "SDL platform route did not rescue princess 11 and return aboard");
        platform_route = ride_to(platform_route, {4, 5}, "platform-route-return");
        push_key(SDLK_RIGHT);
        platform_route = run_script(platform_route, "platform-route-exit");
        require(platform_route.location.position == yipe::MapPosition{5, 5}
                    && platform_route.player.inventory_values[11] == 1
                    && platform_route.map_tile_changes.size() == 1
                    && platform_route.map_tile_changes.front().position == yipe::MapPosition{1, 2},
                "platform route failed to exit or saved terrain other than the opened key door");

        yipe::GameState fire;
        fire.player.health = fire.player.maximum_health = 100;
        fire.player.random_encounter_threshold = 32767;
        fire.location = {{6, 8}, 0, -3, 0, true, 0x85};
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        push_wait(4);
        push_key(SDLK_a);
        push_key(SDLK_a);
        const auto crossed = run_script(fire, "timed-fire-opening");
        require(crossed.location.position == yipe::MapPosition{4, 8}
                    && crossed.player.health == 100 && crossed.map_tile_changes.empty(),
                "idle SDL fire-wall opening was not traversable");
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        push_wait(4);
        push_key(SDLK_a);
        push_wait(4);
        const auto burned = run_script(fire, "timed-fire-closing");
        require(burned.location.position == yipe::MapPosition{5, 8}
                    && burned.player.health < 100 && burned.player.health >= 1,
                "SDL hazard did not burn a player standing in a closing fire wall");

        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        fire.location = {{8, 10}, 0, -2, 0, true, 0x7f};
        for (const auto key : {SDLK_DOWN, SDLK_KP_1, SDLK_KP_1, SDLK_DOWN,
                               SDLK_DOWN, SDLK_DOWN, SDLK_DOWN, SDLK_DOWN}) {
            push_key(key);
        }
        push_wait(4);
        dialog_choices = {{"Rescued Princess", 3, ""}};
        for (const auto key : {SDLK_LEFT, SDLK_LEFT, SDLK_LEFT, SDLK_LEFT, SDLK_KP_1}) {
            push_key(key);
        }
        push_wait(4);
        for (const auto key : {SDLK_KP_9, SDLK_RIGHT, SDLK_RIGHT}) push_key(key);
        push_wait(4);
        push_key(SDLK_RIGHT);
        push_key(SDLK_RIGHT);
        const auto fire_rescued = run_script(fire, "fire-route-rescued");
        require(fire_rescued.location.map_id == 0x85
                    && fire_rescued.location.position == yipe::MapPosition{6, 8}
                    && fire_rescued.player.inventory_values[20] == 1
                    && fire_rescued.player.princesses_found == 1
                    && fire_rescued.player.health == 100
                    && fire_rescued.map_tile_changes.empty(),
                "SDL timed-fire route did not enter, rescue princess 20, and leave safely");

        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        yipe::GameState spray_chest;
        spray_chest.location = {{9, 2}, -4, -1, 2, false, 0x63};
        spray_chest.player.random_encounter_threshold = 32767;
        auto chest_map = *yipe::original_map(0x63);
        auto chest_view = spray_chest;
        yipe::initialize_map_visibility(chest_view.map_visibility, chest_map, {9, 2});
        const auto chest_pixels = [&] {
            render_game(renderer.get(), chest_map, textures, chest_view);
            int width = 0;
            int height = 0;
            require(SDL_GetRendererOutputSize(renderer.get(), &width, &height) == 0
                        && width % map_width == 0 && height % map_height == 0,
                    "could not determine the rendered chest scale");
            const int cell_width = tile_size * (width / map_width);
            const int cell_height = tile_size * (height / map_height);
            std::vector<Uint32> pixels(static_cast<std::size_t>(cell_width * cell_height));
            const SDL_Rect rectangle{9 * cell_width, cell_height, cell_width, cell_height};
            require(SDL_RenderReadPixels(renderer.get(), &rectangle, SDL_PIXELFORMAT_RGBA8888,
                                        pixels.data(), cell_width * sizeof(Uint32)) == 0,
                    "could not read the rendered chest tile");
            return pixels;
        };
        const auto unopened_pixels = chest_pixels();
        yipe::MsvcRandom chest_random(1);
        require(yipe::open_chest(chest_view.player, chest_map, {9, 1}, 1, chest_random).opened,
                "render fixture failed to collect its chest");
        const auto opened_pixels = chest_pixels();
        require(unopened_pixels != opened_pixels, "chest sprite did not disappear on collection");
        chest_map = *yipe::original_map(0x63);
        yipe::initialize_map_actions(chest_view.player, chest_map, {9, 2});
        require(chest_pixels() == opened_pixels, "opened chest sprite reappeared on map re-entry");
        messages.clear();
        message_texts.clear();
        push_key(SDLK_UP);
        const auto spray_collected = run_script(spray_chest, "spray-chest");
        require(spray_collected.player.has_waterproof_shoe_spray
                    && spray_collected.player.opened_chests[0x62] == 2
                    && spray_collected.player.gold == 72 && spray_collected.player.experience == 29
                    && std::count(messages.begin(), messages.end(), "Treasure Chest") == 1
                    && message_texts.front().find("Waterproof Shoe Spray") != std::string::npos,
                "SDL movement did not collect and display the actual spray chest");
        messages.clear();
        push_key(SDLK_DOWN);
        push_key(SDLK_UP);
        const auto spray_revisited = run_script(spray_collected, "spray-chest-revisit");
        require(spray_revisited.player.gold == spray_collected.player.gold
                    && spray_revisited.player.experience == spray_collected.player.experience
                    && std::count(messages.begin(), messages.end(), "Treasure Chest") == 0,
                "SDL map re-entry duplicated a chest reward");

        // Win the actual four authored groups through core combat, then drive
        // both shop entrances through SDL. Combat stats are supplied; shoes and
        // victory records are earned. This is an acquisition test, not a route.
        yipe::GameState quizilog;
        quizilog.location = {{8, 4}, -3, 1, 0, false, 8};
        quizilog.player.random_encounter_threshold = 32767;
        messages.clear();
        push_key(SDLK_LEFT);
        const auto uncleared = run_script(quizilog, "quizilog-uncleared");
        require(!uncleared.player.has_tree_climbing_shoes
                    && std::count(messages.begin(), messages.end(), "Quizilog") == 1,
                "Quizilog weapon shop did not require clearing the town");
        quizilog.player.level = 40;
        quizilog.player.strength = quizilog.player.dexterity = 50;
        quizilog.player.weapon_rank = 38;
        quizilog.player.armor_rank = 30;
        quizilog.player.health = quizilog.player.maximum_health = 10000;
        const auto guards = yipe::original_monsters_for_map(8);
        for (std::size_t i = 0; i < guards.size(); ++i) {
            yipe::MsvcRandom random(1);
            auto encounter = yipe::start_monster_encounter(guards[i], quizilog.player, random, 0);
            yipe::CombatTurn turn;
            for (int n = 0; n < 10000 && turn.outcome == yipe::CombatOutcome::ongoing; ++n) {
                turn = yipe::resolve_attack_turn(quizilog.player, encounter, random);
            }
            require(turn.outcome == yipe::CombatOutcome::victory, "Quizilog group did not end in victory");
            quizilog.defeated_monster_placements.push_back(guards[i].placement_index);
            if (i + 1 == guards.size() - 1) {
                quizilog.location.position = {6, 8};
                messages.clear();
                push_key(SDLK_LEFT);
                const auto partial = run_script(quizilog, "quizilog-one-guard-left");
                require(!partial.player.has_tree_climbing_shoes
                            && partial.player.miscellaneous_shop_stage == 0
                            && std::count(messages.begin(), messages.end(), "Quizilog") == 1,
                        "Quizilog miscellaneous shop awarded shoes with one group remaining");
            }
        }
        for (bool weapon_shop : {false, true}) {
            auto visit = quizilog;
            visit.location.position = weapon_shop ? yipe::MapPosition{8, 4}
                                                 : yipe::MapPosition{6, 8};
            const char* shop_title = weapon_shop ? "Weapon Shop" : "Quizilog Random Items Shop";
            messages.clear();
            dialog_choices = {{shop_title, 0, ""}};
            push_key(SDLK_LEFT);
            auto rewarded = run_script(visit, weapon_shop ? "shoe-weapon" : "shoe-misc");
            require(rewarded.player.has_tree_climbing_shoes
                        && std::count(messages.begin(), messages.end(), "Quizilog Reward") == 1,
                    "clearing the actual groups did not unlock the shop's shoe award");
            messages.clear();
            dialog_choices = {{shop_title, 0, ""}};
            push_key(SDLK_LEFT);
            rewarded = run_script(rewarded, weapon_shop ? "shoe-weapon-repeat" : "shoe-misc-repeat");
            require(std::count(messages.begin(), messages.end(), "Quizilog Reward") == 0,
                    "Quizilog repeated its one-time shoe award");
            auto tree_map = *yipe::original_map(8);
            auto from = yipe::MapPosition{9, 3};
            require(yipe::try_move(tree_map, from, 1, 0,
                                  {false, false, false, rewarded.player.has_tree_climbing_shoes})
                        .position_changed,
                    "earned shoes did not unlock a real tree tile");
        }

        struct ShopVisit {
            int map_id;
            int region_x;
            int region_y;
            yipe::MapPosition from;
            SDL_Keycode direction;
            const char* title;
            int first_price;
        };
        constexpr std::array<ShopVisit, 4> shop_visits{{
            {0x11, 0, 0, {5, 6}, SDLK_RIGHT, "Fred's Miscellaneous Shop", 75},
            {0x1b, 4, -1, {3, 6}, SDLK_RIGHT, "Maynard Supplies Shop", 80},
            {8, -3, 1, {6, 8}, SDLK_LEFT, "Quizilog Random Items Shop", 75},
            {0x0c, 1, 1, {7, 3}, SDLK_RIGHT, "Outback Shop", 70},
        }};
        for (const auto& visit : shop_visits) {
            auto customer = visit.map_id == 8 ? quizilog : yipe::GameState{};
            customer.location = {visit.from, visit.region_x, visit.region_y, 0, false, visit.map_id};
            customer.player.gold = visit.first_price;
            customer.player.random_encounter_threshold = 32767;
            const auto first_row = "French Fries\nPrice: " + std::to_string(visit.first_price) + " gold";
            dialog_choices = {{visit.title, 3, first_row}, {visit.title, 2, "Item 11 of 11"},
                              {visit.title, 1, first_row}, {visit.title, 0, "Your gold: 0"}};
            messages.clear();
            message_texts.clear();
            push_key(visit.direction);
            const auto bought = run_script(customer, "misc-shop-" + std::to_string(visit.map_id));
            require(dialog_choices.empty() && bought.player.gold == 0
                        && bought.player.inventory[1] == 1 && bought.location.position == visit.from,
                    "a real miscellaneous entrance failed navigation, exact-gold purchase, or exit");
            require(std::find(message_texts.begin(), message_texts.end(),
                              "You purchased French Fries.") != message_texts.end(),
                    "miscellaneous shop did not present the purchase result");
        }

        yipe::GameState key_customer;
        key_customer.location = {{7, 3}, 1, 1, 0, false, 0x0c};
        key_customer.player.random_encounter_threshold = 32767;
        key_customer.player.level = 26;
        key_customer.player.strength = key_customer.player.dexterity = key_customer.player.intelligence = 20;
        key_customer.player.gold = 8000;
        std::fill_n(key_customer.player.inventory.begin(), yipe::Player::carried_inventory_size, 1);
        for (int i = 0; i < 8; ++i) dialog_choices.push_back({"Outback Shop", 2, ""});
        dialog_choices.push_back({"Outback Shop", 1, "Rainbow Key\nPrice: 4000 gold"});
        dialog_choices.push_back({"Outback Shop", 1, "Your gold: 4000"});
        dialog_choices.push_back({"Outback Shop", 0, "Your gold: 4000"});
        messages.clear();
        message_texts.clear();
        push_key(SDLK_RIGHT);
        auto keyed_customer = run_script(key_customer, "outback-key-purchase");
        require(dialog_choices.empty() && keyed_customer.player.has_rainbow_key
                    && keyed_customer.player.gold == 4000 && keyed_customer.player.miscellaneous_shop_stage == 4
                    && keyed_customer.player.inventory == key_customer.player.inventory,
                "Outback failed to sell the key once with full inventory and finish its shop stage");
        constexpr std::string_view shortage_notice =
            "Due to a worldwide shortage of supplies the price for certain items has increased.";
        require(std::count(message_texts.begin(), message_texts.end(), shortage_notice) == 1
                    && std::find(message_texts.begin(), message_texts.end(),
                                 "You already have this item or something better equipped.") != message_texts.end(),
                "Outback did not announce the price increase or reject a repeat key purchase");
        for (int i = 0; i < 8; ++i) dialog_choices.push_back({"Outback Shop", 2, ""});
        dialog_choices.push_back({"Outback Shop", 1, "Wet Mittens\nPrice: 25 gold"});
        dialog_choices.push_back({"Outback Shop", 0, "Your gold: 3975"});
        message_texts.clear();
        push_key(SDLK_RIGHT);
        keyed_customer = run_script(keyed_customer, "outback-after-key");
        require(dialog_choices.empty() && keyed_customer.player.gold == 3975
                    && keyed_customer.player.gloves_rank == 1 && keyed_customer.player.has_rainbow_key
                    && keyed_customer.player.miscellaneous_shop_stage == 4
                    && std::count(message_texts.begin(), message_texts.end(), shortage_notice) == 0,
                "Outback re-entry failed to retain progression or restore its ordinary equipment row");
        const auto door_map = *yipe::original_map(0x4a);
        bool used_purchased_key = false;
        for (int x = 1; x < yipe::Map::width; ++x) {
            for (int y = 0; y < yipe::Map::height; ++y) {
                if (door_map.tile({x, y}) != 0x3f) continue;
                auto from = yipe::MapPosition{x - 1, y};
                require(!yipe::try_move(door_map, from, 1, 0).position_changed
                            && yipe::try_move(door_map, from, 1, 0,
                                             {false, keyed_customer.player.has_rainbow_key}).position_changed,
                        "purchased Rainbow Key did not unlock an original keyed door");
                used_purchased_key = true;
            }
        }
        require(used_purchased_key, "key acquisition case did not exercise a keyed door");

        yipe::CombatTurn key_victory;
        key_victory.reward.underworld_key = true;
        std::ostringstream victory_text;
        append_victory_details(victory_text, key_victory, quizilog.player);
        require(victory_text.str().find("Underworld Key") != std::string::npos,
                "combat victory presentation omitted the newly awarded key");

        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        push_key(SDLK_F10);
        dialog_choices = {{"Settings", 0, ""}};
        delay_settings_dialog = true;
        yipe::GameState paused;
        paused.player.monster_repellent_steps = 5;
        paused.player.random_encounter_threshold = 125;
        const auto after_modal = run_script(paused, "paused-timer");
        require(after_modal.player.monster_repellent_steps == 5,
                "modal UI advanced exploration time or queued catch-up ticks");
        delay_settings_dialog = false;
        for (int control : {KMOD_LCTRL, KMOD_RCTRL}) {
            for (bool repeat : {false, true}) {
                messages.clear();
                SDL_Event event{};
                event.type = SDL_KEYDOWN;
                event.key.keysym.sym = SDLK_t;
                event.key.keysym.mod = static_cast<Uint16>(control);
                event.key.repeat = repeat ? 1 : 0;
                require(SDL_PushEvent(&event) == 1, "could not queue status shortcut");
                if (!repeat) {
                    push_key(SDLK_d);
                    push_key(SDLK_ESCAPE);
                }
                delay_status_dialog = true;
                const auto status_paused = run_script(paused, "status-paused");
                delay_status_dialog = false;
                require(status_paused.player.monster_repellent_steps == 5
                            && status_paused.player.health == after_modal.player.health
                            && status_paused.player.spell_points == after_modal.player.spell_points
                            && status_paused.random_state == after_modal.random_state
                            && status_paused.location.position == after_modal.location.position
                            && messages == (repeat ? std::vector<std::string>{"Game Saved"}
                                : std::vector<std::string>{"Player Status Details", "Game Saved"}),
                        "Ctrl+T repeated, advanced exploration time, or changed player state");
            }
        }
        scripted_clock = false;

        selected_open = std::filesystem::path(YIPE_TEST_EVIDENCE_DIR)
            / "r08/R08Combat-south.yp3";
        std::ifstream source_before(*selected_open, std::ios::binary);
        const std::string original_bytes{std::istreambuf_iterator<char>(source_before), {}};
        require(original_bytes.size() == 5074, "original save fixture is incomplete");
        selected_save = test_directory / "real-original-import.y3r";
        const auto imported = choose_game_to_open(test_directory);
        require(imported && imported->opened_game
                    && imported->opened_save_path == *selected_save,
                "real original save did not select a separate native destination");
        SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
        auto native_path = imported->opened_save_path;
        push_key(SDLK_F5);
        push_key(SDLK_q);
        static_cast<void>(run_game(
            window.get(), renderer.get(), textures, *imported->opened_game, native_path,
            test_directory / "settings.y3r", test_directory / "highscores.y3r",
            scores, settings, audio));
        const auto converted = yipe::load_game(native_path);
        std::ifstream source_after(*selected_open, std::ios::binary);
        const std::string remaining_bytes{std::istreambuf_iterator<char>(source_after), {}};
        require(converted.player.name == "R08Combat"
                    && converted.location.position == yipe::MapPosition{5, 6}
                    && converted.player.health == 13
                    && yipe::load_legacy_save(*selected_open).validation_passed
                    && remaining_bytes == original_bytes,
                "SDL original import and native save changed the reference state");
        ++cases;
        std::filesystem::remove_all(test_directory);
        std::cout << cases << " shortcut, mode, creation, timer, progression, and import cases passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
