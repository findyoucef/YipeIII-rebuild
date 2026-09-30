// Input-only fresh-game continuation or explicit production Open verification.
// Fresh-game modes never reload observation checkpoints into play. Resume mode
// opens a native save in a separate process; stdin still supplies only inputs.
#pragma once

namespace {
std::ofstream full_game_commands;
std::ofstream full_game_trace;
std::vector<std::string> full_game_opening_messages;
std::vector<std::string> full_game_opening_texts;
int full_game_command_count = 0;

void check_full_game_completion(const yipe::GameState& game, int specialty, int difficulty) {
    const auto& player = game.player;
    if (player.rescued_princess_count() != 20 || player.stamps_found != 6
        || player.gatekeeper_state != 3) {
        throw std::runtime_error("full-game driver exited before earning all rescues, stamps, and the finale");
    }
    if (player.specialty != specialty || player.difficulty != difficulty
        || !player.has_rainbow_key || !player.has_underworld_key
        || !player.has_waterproof_shoe_spray || !player.has_tree_climbing_shoes || player.strength < 40) {
        throw std::runtime_error("full-game driver changed creation choices or did not earn movement prerequisites");
    }
    if (game.location.map_id != 0x11 || player.health <= 0
        || std::find(message_texts.begin(), message_texts.end(),
            "You've rescued all the princesses!!\n\nYou are the hero of the entire world!!") == message_texts.end()) {
        throw std::runtime_error("full-game driver did not reach the live town-entry victory message");
    }
}

void full_game_log(const std::string& text) {
    std::cout << text << std::endl;
    full_game_trace << text << std::endl;
}

std::string full_game_line() {
    std::string line;
    while (std::getline(std::cin, line)) {
        full_game_commands << line << std::endl;
        if (line.empty() || line.front() == '#') continue;
        if (++full_game_command_count > 100000) throw std::runtime_error("full-game input budget exceeded");
        full_game_log("INPUT " + line);
        return line;
    }
    throw std::runtime_error("full-game input ended before an explicit quit");
}

void full_game_state(const yipe::GameState& game) {
    // Copy completed production saves for later, separate-process Open checks.
    if (const char* checkpoints = std::getenv("YIPE_CHECKPOINT_COMMANDS")) {
        std::istringstream selected(checkpoints);
        int command = 0;
        while (selected >> command) {
            if (command == full_game_command_count) {
                std::filesystem::copy_file(test_directory / "fresh-route.y3r",
                    test_directory / ("checkpoint-" + std::to_string(command) + ".y3r"),
                    std::filesystem::copy_options::overwrite_existing);
            }
        }
    }
    const auto& p = game.player;
    std::ostringstream s;
    s << "STATE {\"map\":" << game.location.map_id << ",\"x\":" << game.location.position.x
      << ",\"y\":" << game.location.position.y << ",\"level\":" << p.level
      << ",\"xp\":" << p.experience << ",\"gold\":" << p.gold
      << ",\"hp\":[" << p.health << ',' << p.maximum_health << "],\"sp\":[" << p.spell_points
      << ',' << p.maximum_spell_points << "],\"ap\":[" << p.ability_points << ',' << p.maximum_ability_points
      << "],\"stats\":[" << p.strength << ',' << p.dexterity << ',' << p.intelligence
      << "],\"equipment\":[" << p.weapon_rank << ',' << p.armor_rank << ',' << p.helmet_rank
      << ',' << p.gloves_rank << ',' << p.ring_rank << ',' << p.boots_rank << ',' << p.necklace_rank
      << "],\"rescues\":" << p.rescued_princess_count() << ",\"stamps\":" << p.stamps_found
      << ",\"gatekeeper\":" << p.gatekeeper_state << ",\"rainbow\":" << p.has_rainbow_key
      << ",\"underworld\":" << p.has_underworld_key << ",\"spray\":" << p.has_waterproof_shoe_spray
      << ",\"shoes\":" << p.has_tree_climbing_shoes << ",\"deaths\":" << p.death_count
      << ",\"healing_ring\":" << p.has_bonus_healing_ring
      << ",\"shop_stage\":" << p.miscellaneous_shop_stage << ",\"rng\":" << game.random_state
      << ",\"inventory\":[";
    for (std::size_t i = 0; i < yipe::Player::carried_inventory_size; ++i) s << (i ? "," : "") << p.inventory[i];
    s << "],\"spells\":[";
    for (std::size_t i = 0; i < p.spells.size(); ++i) s << (i ? "," : "") << p.spells[i];
    s << "]";
    if (full_game_variant || full_game_resuming) {
        s << ",\"specialty\":" << p.specialty << ",\"difficulty\":" << p.difficulty;
    }
    s << "}";
    full_game_log(s.str());
}

void full_game_key(SDL_Keycode key, Uint16 modifiers = 0) {
    SDL_Event event{};
    event.type = SDL_KEYDOWN;
    event.key.keysym.sym = key;
    event.key.keysym.mod = modifiers;
    if (SDL_PushEvent(&event) != 1) throw std::runtime_error(SDL_GetError());
}

void full_game_input(SDL_Event* event) {
    if (!full_game_live) {
        if (route_checkpoints.size() != (full_game_variant || full_game_resuming ? 1u : 42u)) {
            throw std::runtime_error("fresh-game opening observations are incomplete: got "
                + std::to_string(route_checkpoints.size())
                + (messages.empty() ? "" : "; last message " + messages.back() + ": " + message_texts.back()));
        }
        full_game_commands.open(test_directory / "commands.txt");
        full_game_trace.open(test_directory / "trace.txt");
        if (!full_game_commands || !full_game_trace) throw std::runtime_error("cannot open proof logs");
        full_game_opening_messages = messages;
        full_game_opening_texts = message_texts;
        full_game_live = true;
        full_game_log(full_game_resuming ? "RESUMING through production native Open."
            : full_game_variant ? "CONTINUING fresh production character creation."
                                       : "CONTINUING E066 from the same fresh-game run; commands: walk DIGITS, wait TICKS, spell INDEX, combat INDEX low/high, heal INDEX low/high, target STEPS, opening INDEX low/high|off, flee on/off, mana on/off, type TEXT, key i/c/other, snapshot, quit. Dialogs accept choice ID.");
        full_game_state(route_checkpoints.back());
    }
    route_spell.reset();
    route_spell_power = yipe::SpellPower::low;
    route_healing_item = false;
    const std::string line = full_game_line();
    std::istringstream input(line);
    std::string command;
    input >> command;
    if (command == "walk") {
        std::string directions;
        input >> directions;
        if (directions.empty() || directions.size() > 10000) throw std::runtime_error("invalid walk length");
        for (char direction : directions) {
            switch (direction) {
            case '1': full_game_key(SDLK_KP_1); break;
            case '2': full_game_key(SDLK_DOWN); break;
            case '3': full_game_key(SDLK_KP_3); break;
            case '4': full_game_key(SDLK_LEFT); break;
            case '6': full_game_key(SDLK_RIGHT); break;
            case '7': full_game_key(SDLK_KP_7); break;
            case '8': full_game_key(SDLK_UP); break;
            case '9': full_game_key(SDLK_KP_9); break;
            default: throw std::runtime_error("invalid walk direction");
            }
        }
    } else if (command == "wait") {
        int ticks = 0;
        input >> ticks;
        if (ticks < 1 || ticks > 50000) throw std::runtime_error("invalid wait length");
        SDL_Event wait{};
        wait.type = SDL_USEREVENT;
        wait.user.code = ticks;
        if (SDL_PushEvent(&wait) != 1) throw std::runtime_error(SDL_GetError());
    } else if (command == "spell") {
        std::size_t index = yipe::Player::spell_count;
        input >> index;
        if (index >= yipe::Player::spell_count) throw std::runtime_error("invalid spell index");
        route_spell = index;
        route_dialog_pages = 0;
        full_game_key(SDLK_c);
    } else if (command == "combat") {
        std::string value;
        input >> value;
        if (value == "off") {
            route_attack_spell_enabled = false;
        } else {
            const auto index = static_cast<std::size_t>(std::stoul(value));
            std::string power;
            input >> power;
            if ((index != 2 && index != 3 && index != 6 && index != 7)
                || (power != "low" && power != "high")) throw std::runtime_error("invalid combat spell policy");
            route_attack_spell = index;
            route_attack_spell_enabled = true;
            route_attack_power = power == "high" ? yipe::SpellPower::high : yipe::SpellPower::low;
        }
    } else if (command == "heal") {
        std::size_t index = yipe::Player::spell_count;
        std::string power;
        input >> index >> power;
        if ((index != 1 && index != 5 && index != 9)
            || (power != "low" && power != "high")) throw std::runtime_error("invalid healing spell policy");
        route_heal_spell = index;
        route_heal_power = power == "high" ? yipe::SpellPower::high : yipe::SpellPower::low;
        route_food_before_small_heal = true;
    } else if (command == "target") {
        input >> route_target_steps;
        if (route_target_steps < 0 || route_target_steps > 2) {
            throw std::runtime_error("invalid target step count");
        }
    } else if (command == "opening") {
        std::string value;
        input >> value;
        if (value == "off") {
            route_opening_spell.reset();
        } else {
            std::string power;
            input >> power;
            const auto index = static_cast<std::size_t>(std::stoul(value));
            if ((index != 10 && index != 11)
                || (power != "low" && power != "high")) throw std::runtime_error("invalid opening spell policy");
            route_opening_spell = index;
            route_opening_power = power == "high" ? yipe::SpellPower::high : yipe::SpellPower::low;
        }
    } else if (command == "flee") {
        std::string setting;
        input >> setting;
        if (setting == "above") {
            if (!(input >> route_flee_above_health) || route_flee_above_health < 1
                || route_flee_above_health > 100000) throw std::runtime_error("invalid flee health limit");
        } else {
            if (setting != "on" && setting != "off") throw std::runtime_error("invalid flee policy");
            route_flee_above_health = 0;
            route_flee = setting == "on";
        }
    } else if (command == "mana") {
        std::string setting;
        input >> setting;
        if (setting != "on" && setting != "off") throw std::runtime_error("invalid mana potion policy");
        route_use_mana_potions = setting == "on";
    } else if (command == "type") {
        std::string value;
        input >> value;
        if (value.empty() || value.size() >= SDL_TEXTINPUTEVENT_TEXT_SIZE) {
            throw std::runtime_error("invalid typed text length");
        }
        SDL_Event text{};
        text.type = SDL_TEXTINPUT;
        std::copy(value.begin(), value.end(), text.text.text);
        SDL_Event enter{};
        enter.type = SDL_KEYDOWN;
        enter.key.keysym.sym = SDLK_RETURN;
        text_input_events = {text, enter};
        text_event_index = 0;
    } else if (command == "key") {
        std::string key;
        input >> key;
        if (key == "other") full_game_key(SDLK_o, KMOD_CTRL | KMOD_SHIFT);
        else if (key == "i") full_game_key(SDLK_i);
        else if (key == "c") full_game_key(SDLK_c);
        else throw std::runtime_error("unsupported input key");
    } else if (command == "quit") {
        full_game_key(SDLK_q);
        if (SDL_PollEvent(event) != 1) throw std::runtime_error("could not read quit input");
        return;
    } else if (command != "snapshot") throw std::runtime_error("unknown full-game command");
    full_game_key(SDLK_F5);
    if (SDL_PollEvent(event) != 1) throw std::runtime_error("could not read queued full-game input");
}

int full_game_choice(const SDL_MessageBoxData* data, int* selected) {
    std::ostringstream display;
    display << "DIALOG " << data->title << '\n' << data->message << '\n';
    for (int i = 0; i < data->numbuttons; ++i) display << data->buttons[i].buttonid << ": " << data->buttons[i].text << '\n';
    full_game_log(display.str());
    std::istringstream input(full_game_line());
    std::string command;
    int choice = -100;
    input >> command >> choice;
    if (command != "choice") throw std::runtime_error("expected a dialog choice");
    for (int i = 0; i < data->numbuttons; ++i) {
        if (data->buttons[i].buttonid == choice) {
            *selected = choice;
            return 0;
        }
    }
    throw std::runtime_error("dialog choice is unavailable");
}
} // namespace
