#include "yipe/audio.hpp"
#include "yipe/auxiliary_data.hpp"
#include "yipe/casino.hpp"
#include "yipe/encounter.hpp"
#include "yipe/exploration.hpp"
#include "yipe/item_names.hpp"
#include "yipe/legacy_save.hpp"
#include "yipe/map.hpp"
#include "yipe/player.hpp"
#include "yipe/quest.hpp"
#include "yipe/random.hpp"
#include "yipe/save.hpp"
#include "yipe/settings.hpp"
#include "yipe/shop.hpp"
#include "yipe/spell.hpp"

#include <algorithm>
#include <array>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace {

int failures = 0;

void check(bool condition, const std::string& description) {
    if (!condition) {
        ++failures;
        std::cerr << "FAIL: " << description << '\n';
    }
}

std::uint32_t test_crc32(const std::vector<std::uint8_t>& bytes,
                         std::size_t begin) {
    std::uint32_t crc = 0xffffffffu;
    for (std::size_t index = begin; index < bytes.size(); ++index) {
        crc ^= bytes[index];
        for (int bit = 0; bit < 8; ++bit) {
            const std::uint32_t mask = 0u - (crc & 1u);
            crc = (crc >> 1u) ^ (0xedb88320u & mask);
        }
    }
    return ~crc;
}

void write_u32(std::vector<std::uint8_t>& bytes, std::size_t position,
               std::uint32_t value) {
    for (unsigned shift = 0; shift < 32; shift += 8) {
        bytes[position++] = static_cast<std::uint8_t>(value >> shift);
    }
}

void append_u16(std::vector<std::uint8_t>& bytes, std::uint16_t value) {
    bytes.push_back(static_cast<std::uint8_t>(value));
    bytes.push_back(static_cast<std::uint8_t>(value >> 8u));
}

void append_i32(std::vector<std::uint8_t>& bytes, std::int32_t value) {
    const auto encoded = static_cast<std::uint32_t>(value);
    for (unsigned shift = 0; shift < 32; shift += 8) {
        bytes.push_back(static_cast<std::uint8_t>(encoded >> shift));
    }
}

void write_bytes(const std::filesystem::path& path,
                 const std::vector<std::uint8_t>& bytes) {
    std::ofstream output(path, std::ios::binary | std::ios::trunc);
    output.write(reinterpret_cast<const char*>(bytes.data()),
                 static_cast<std::streamsize>(bytes.size()));
}

void remove_save_extension(const std::filesystem::path& path, std::size_t extension_size,
                           std::uint16_t version) {
    std::ifstream input(path, std::ios::binary);
    std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                    std::istreambuf_iterator<char>()};
    constexpr std::size_t header_size = 14;
    bytes.resize(bytes.size() - extension_size);
    bytes[4] = static_cast<std::uint8_t>(version);
    bytes[5] = static_cast<std::uint8_t>(version >> 8u);
    write_u32(bytes, 6, static_cast<std::uint32_t>(bytes.size() - header_size));
    write_u32(bytes, 10, test_crc32(bytes, header_size));
    std::ofstream output(path, std::ios::binary | std::ios::trunc);
    output.write(reinterpret_cast<const char*>(bytes.data()),
                 static_cast<std::streamsize>(bytes.size()));
}

void test_random() {
    yipe::MsvcRandom random(1);
    check(random.next() == 41, "MSVC random value 1");
    check(random.next() == 18467, "MSVC random value 2");
    check(random.next() == 6334, "MSVC random value 3");
}

void test_player_defaults() {
    const yipe::Player player;
    check(!player.silly_mode, "new players default to Normal Mode");
    check(player.name == "Yipie", "default player name");
    check(player.strength == 16 && player.dexterity == 13 && player.intelligence == 10,
          "warrior default stats");
    check(player.health == 13 && player.maximum_health == 13, "warrior default health");
    check(player.spell_points == 10 && player.maximum_spell_points == 10
              && player.specialty == 3 && player.combat_style == 0,
          "Combat preset starts with its recovered SP and separate specialty");
    check(player.ability_points == 5 && player.maximum_ability_points == 5,
          "warrior starts with five ability points");
    check(player.inventory[0] == 1, "new player owns item 1");
    check(player.spells[0] == 1
              && std::count(player.spells.begin() + 1, player.spells.end(), 0) == 14,
          "new player knows only Return to Surface");

    const yipe::Player rogue(yipe::CharacterType::rogue);
    check(rogue.strength == 11 && rogue.dexterity == 15 && rogue.intelligence == 12,
          "rogue default stats");
    check(rogue.health == 13 && rogue.maximum_health == 13,
          "rogue default health");
    check(rogue.spell_points == 12 && rogue.maximum_spell_points == 12
              && rogue.specialty == 0 && rogue.combat_style == 0,
          "Quickness preset uses recovered SP and specialty");
    check(rogue.ability_points == 6 && rogue.maximum_ability_points == 6,
          "rogue starts with six ability points");

    const yipe::Player wizard(yipe::CharacterType::wizard);
    check(wizard.strength == 11 && wizard.dexterity == 12 && wizard.intelligence == 16,
          "wizard default stats");
    check(wizard.health == 10 && wizard.maximum_health == 10,
          "wizard default health");
    check(wizard.spell_points == 18 && wizard.maximum_spell_points == 18
              && wizard.specialty == 1 && wizard.combat_style == 0,
          "Magic preset uses recovered SP and specialty");
    check(wizard.ability_points == 5 && wizard.maximum_ability_points == 5,
          "wizard starts with five ability points");
    for (const auto* preset : {&player, &rogue, &wizard}) {
        check(preset->stat_cap_mode == 2 && preset->maximum_stat() == 50,
              "every new preset uses the rebuild's supplied-content stat limit");
    }
}

void test_random_character_creation() {
    yipe::Player player;
    yipe::MsvcRandom random(1);
    player.reroll_random_character(random);
    check(player.specialty == 2 && player.combat_style == 0
              && player.dexterity == 13 && player.strength == 13
              && player.intelligence == 12 && player.health == 12
              && player.spell_points == 13 && player.ability_points == 5
              && player.maximum_health == 12 && player.maximum_spell_points == 13
              && player.maximum_ability_points == 5 && random.state() == 10044473u
              && player.stat_cap_mode == 2 && player.maximum_stat() == 50,
          "random setup follows the recovered roll order and stat bounds");
    player.reroll_random_character(random);
    check(player.dexterity == 13 && player.strength == 13
              && player.intelligence == 11 && player.health == 12
              && player.spell_points == 14 && player.ability_points == 5
              && random.state() == 2652862833u && player.stat_cap_mode == 2,
          "reroll consumes the next original RNG sequence without resetting it");
}

void test_princess_statistics() {
    yipe::Player player;
    check(player.rescued_princess_count() == 0, "a fresh player has no rescued princesses");
    player.princesses_found = 20;
    player.inventory.fill(1);
    player.quest_flags.fill(true);
    check(player.rescued_princess_count() == 0,
          "rescue statistics do not use the quest counter, carried items, or conversation flags");
    for (std::size_t slot = 0; slot < player.inventory_values.size(); ++slot) {
        for (const std::uint16_t value : std::array<std::uint16_t, 6>{0, 1, 2, 0x7fff, 0x8000, 0xffff}) {
            player.inventory_values.fill(0);
            player.inventory_values[slot] = value;
            check(player.rescued_princess_count() == (value > 0 && value < 0x8000 ? 1 : 0),
                  "every rescue slot uses the recovered positive signed-short comparison");
        }
    }
    player.inventory_values.fill(1);
    check(player.rescued_princess_count() == 30,
          "rescue statistics count all 30 slots without clamping to the 20 placed princesses");
}

void test_health_change() {
    yipe::Player player;
    check(player.adjust_health(20), "positive health remains alive");
    check(player.health == player.maximum_health, "health increase is capped");
    check(!player.adjust_health(-player.maximum_health), "zero health is dead");
    check(player.health == 0, "damage reaches zero");
}

void test_level_progression() {
    check(yipe::experience_required_for_next_level(1) == 201
              && yipe::experience_required_for_next_level(27) == 42001
              && yipe::experience_required_for_next_level(28) == 47001
              && yipe::experience_required_for_next_level(33) == 90001,
          "early and transition level thresholds preserve original comparisons");
    check(yipe::experience_required_for_next_level(34) == 119001
              && yipe::experience_required_for_next_level(55) == 220001
              && yipe::experience_required_for_next_level(80) == 400001
              && yipe::experience_required_for_next_level(99) == 495001
              && !yipe::experience_required_for_next_level(100),
          "formula thresholds extend progression through level 100");

    yipe::Player below_threshold;
    below_threshold.experience = 200;
    yipe::MsvcRandom unchanged_random(1);
    check(!yipe::try_level_up(below_threshold, unchanged_random).leveled_up
              && below_threshold.level == 1 && unchanged_random.state() == 1,
          "level progression requires experience strictly above the first threshold");

    yipe::Player warrior;
    warrior.experience = 201;
    warrior.restoration_uses = 2;
    yipe::MsvcRandom warrior_random(1);
    const yipe::LevelUpResult warrior_level = yipe::try_level_up(warrior, warrior_random);
    check(warrior_level.leveled_up && warrior.level == 2
              && warrior_level.health_increase == 6
              && warrior.health == 19 && warrior.maximum_health == 19
              && warrior_level.spell_point_increase == 6
              && warrior.spell_points == 16 && warrior.maximum_spell_points == 16
              && warrior.restoration_uses == 1
              && warrior_level.stat_increase == yipe::StatIncrease::none,
          "ordinary level growth applies gains and restores one restoration use");

    yipe::Player milestone(yipe::CharacterType::wizard);
    milestone.combat_style = 1;
    milestone.level = 4;
    milestone.experience = 1501;
    yipe::MsvcRandom milestone_random(1);
    const yipe::LevelUpResult milestone_level =
        yipe::try_level_up(milestone, milestone_random);
    check(milestone.level == 5
              && milestone_level.stat_increase == yipe::StatIncrease::intelligence
              && milestone.intelligence == 17,
          "milestone levels use the separate saved stat bias");

    yipe::Player high_level;
    high_level.level = 41;
    high_level.experience = 41 * 3500 + 1;
    yipe::MsvcRandom high_level_random(1);
    const yipe::LevelUpResult high_level_growth =
        yipe::try_level_up(high_level, high_level_random);
    check(high_level_growth.leveled_up && high_level.level == 42
              && high_level_growth.health_increase == 4
              && high_level_growth.spell_point_increase == 5,
          "late-level health and spell-point gains preserve their caps");

    yipe::Player one_level_only;
    one_level_only.experience = 100000;
    yipe::MsvcRandom one_level_random(1);
    check(yipe::try_level_up(one_level_only, one_level_random).leveled_up
              && one_level_only.level == 2,
          "one progression check grants at most one level");

    yipe::Player victorious_player;
    victorious_player.experience = 200;
    victorious_player.health = 1000;
    victorious_player.maximum_health = 1000;
    victorious_player.strength = 100;
    victorious_player.weapon_rank = 42;
    yipe::MsvcRandom victory_random(1);
    yipe::Monster defeated_monster =
        yipe::start_single_monster_encounter(2, victory_random);
    defeated_monster.health = 1;
    const yipe::CombatTurn victory = yipe::resolve_attack_turn(
        victorious_player, defeated_monster, victory_random);
    check(victory.outcome == yipe::CombatOutcome::victory
              && victory.level_up.leveled_up && victorious_player.level == 2,
          "completed encounter rewards trigger one progression check");
}

void test_healing_item() {
    yipe::Player player;
    yipe::MsvcRandom random(1);

    const auto at_full = player.use_inventory_item(0, random);
    check(!at_full.consumed && player.inventory[0] == 1, "full-health use keeps healing item");
    check(random.state() == 1, "full-health use consumes no random value");

    player.health = 1;
    const auto healed = player.use_inventory_item(0, random);
    check(healed.consumed && healed.amount == 11, "item 1 uses 10 + rand modulo intelligence");
    check(player.health == 12 && player.inventory[0] == 0, "item 1 heals and is consumed");

    yipe::Player enhanced;
    enhanced.health = -20;
    enhanced.has_bonus_healing_ring = true;
    yipe::MsvcRandom enhanced_random(1);
    const auto enhanced_heal = enhanced.use_inventory_item(0, enhanced_random, false);
    check(enhanced_heal.amount == 30 && enhanced.health == 10,
          "enhanced item 1 uses both random healing rolls");
    check(enhanced_random.state() == 0xc823f683u,
          "enhanced item 1 consumes exactly two random values");
}

void test_stat_and_maximum_items() {
    yipe::Player player;
    yipe::MsvcRandom random(1);

    player.inventory[1] = 6;
    check(player.use_inventory_item(1, random, false).consumed && player.strength == 17,
          "strength item increments strength");

    player.strength = player.maximum_stat();
    player.inventory[1] = 6;
    check(!player.use_inventory_item(1, random, false).consumed && player.inventory[1] == 6,
          "stat item is retained at cap");

    player.inventory[2] = 9;
    const int old_maximum = player.maximum_health;
    const auto maximum = player.use_inventory_item(2, random, false);
    check(maximum.amount == 4, "maximum-health item uses rand modulo 3 plus 2");
    check(player.maximum_health == old_maximum + 4 && player.health == old_maximum + 4,
          "maximum-health item raises current and maximum equally");
}

void test_stat_caps() {
    struct StatCase {
        int yipe::Player::* field;
        std::uint16_t item;
        int combat_style;
        yipe::StatIncrease increase;
    };
    constexpr std::array stats{
        StatCase{&yipe::Player::strength, 6, 0, yipe::StatIncrease::strength},
        StatCase{&yipe::Player::dexterity, 7, 3, yipe::StatIncrease::dexterity},
        StatCase{&yipe::Player::intelligence, 8, 1, yipe::StatIncrease::intelligence},
    };
    for (int mode = 0; mode <= 2; ++mode) {
        const int cap = 30 + mode * 10;
        for (const auto& stat : stats) {
            for (int value : {cap - 1, cap, cap + 1}) {
                yipe::Player player;
                player.stat_cap_mode = mode;
                player.*stat.field = value;
                player.inventory[1] = stat.item;
                yipe::MsvcRandom random(1);
                const auto used = player.use_inventory_item(1, random, false);
                check(player.maximum_stat() == cap && used.consumed == (value < cap)
                          && player.*stat.field == std::max(value, cap)
                          && player.inventory[1] == (value < cap ? 0 : stat.item)
                          && random.state() == 1,
                      "stat potions respect each saved cap and retain items at/above it");

                player.*stat.field = value;
                player.level = 4;
                player.experience = 1501;
                player.combat_style = stat.combat_style;
                const auto growth = yipe::try_level_up(player, random);
                check(growth.leveled_up && player.level == 5
                          && growth.stat_increase == (value < cap ? stat.increase
                                                                 : yipe::StatIncrease::none)
                          && player.*stat.field == std::max(value, cap),
                      "milestone growth respects each saved cap without reducing higher stats");
            }
        }

        const int weapon_cap = mode == 2 ? 29 : 23;
        for (int rank : {22, 23, 28, 29}) {
            yipe::Player player;
            player.stat_cap_mode = mode;
            player.weapon_rank = rank;
            yipe::MsvcRandom random(15); // Rolls 87/101 and weapon choice 1/11.
            const auto loot = yipe::roll_treasure(player, -1, 0, random);
            check(player.weapon_rank == rank + (rank < weapon_cap ? 1 : 0)
                      && loot.equipment_upgrade == (rank < weapon_cap
                          ? yipe::EquipmentUpgrade::weapon : yipe::EquipmentUpgrade::none),
                  "random weapon loot preserves mode 0/1's rank 23 and mode 2's rank 29 limits");
        }
    }
}

void test_relocation_item() {
    yipe::Player player;
    player.inventory[1] = 5;
    player.surface_return_map_id = 0x11;
    player.surface_return_region_x = 0;
    player.surface_return_region_y = 0;
    yipe::MsvcRandom random(1);

    const yipe::ItemUseResult used =
        player.use_inventory_item(1, random, false, false, 0x39, false);
    check(used.consumed && player.inventory[1] == 0
              && player.pending_relocation
                  == static_cast<std::uint16_t>(yipe::PendingRelocation::surface),
          "Return to Surface Potion is consumed underground and queues relocation");

    yipe::WorldLocation cave{{7, 4}, 0, 0, 1, false, 0x39};
    check(yipe::apply_pending_relocation(player, cave)
                  == yipe::PendingRelocation::surface
              && cave.map_id == 0x11 && cave.map_layer == 0
              && player.pending_relocation == 0,
          "Return to Surface Potion restores the recorded surface map");

    yipe::Map::Tiles tiles{};
    tiles[static_cast<std::size_t>(3 * yipe::Map::height + 4)] = 0x36;
    const yipe::Map return_map(0x11, 0, tiles);
    check(yipe::surface_return_position(return_map)
                  == yipe::MapPosition{3, 5},
          "surface relocation places the player below the recovered entrance tile");

    player.inventory[1] = 5;
    const yipe::ItemUseResult on_surface =
        player.use_inventory_item(1, random, false, false, 0x11, false);
    check(!on_surface.consumed && player.inventory[1] == 5,
          "Return to Surface Potion is retained while already on the surface");

    const yipe::ItemUseResult alternate =
        player.use_inventory_item(1, random, false, false, 0x9b, true);
    check(!alternate.consumed && player.inventory[1] == 5,
          "Return to Surface Potion is retained in the alternate map set");

    const yipe::ItemUseResult combat_on_surface =
        player.use_inventory_item(1, random, false, true, 0x11, false);
    check(combat_on_surface.message == "You are already on the surface",
          "surface restriction precedes the combat restriction");
    const yipe::ItemUseResult combat_in_alternate =
        player.use_inventory_item(1, random, false, true, 0x9b, true);
    check(combat_in_alternate.message == "The surface potion has no effect here",
          "alternate-map restriction precedes the combat restriction");
    const yipe::ItemUseResult combat_underground =
        player.use_inventory_item(1, random, false, true, 0x39, false);
    check(combat_underground.message
              == "You cannot use this potion while fighting",
          "valid underground combat reports the fighting restriction");
}

void test_item_names() {
    const auto supplied = yipe::load_item_names(
        std::filesystem::path(YIPE_TEST_ASSET_DIR) / "Yipe3Items.txt");
    check(!supplied.used_embedded_fallback, "supplied item file parses");
    check(supplied.armor.front() == "T-Shirt" && supplied.armor.back() == "Dragonscale Armor",
          "armor ordering");
    check(supplied.weapons.front() == "Hands" && supplied.weapons.back() == "Almighty Longsword",
          "weapon ordering");

    const auto missing = yipe::load_item_names("file-that-does-not-exist.txt");
    check(missing.used_embedded_fallback, "missing item file uses embedded names");
    check(missing.weapons[36] == "Manynard Power Sword",
          "fallback preserves embedded resource spelling");

    const auto silly = yipe::embedded_item_names(true);
    check(silly.weapons.front() == "Hands" && silly.weapons[1] == "Thumbtack"
              && silly.weapons[22] == "Universal TV Remote"
              && silly.weapons.back() == "Bottle Rockets",
          "Silly Mode weapon names preserve resource 1201-1238 ordering");
    check(silly.armor.front() == "T-Shirt" && silly.armor[1] == "T-Shirt with Pocket"
              && silly.armor[23] == "Birthday Suit" && silly.armor.back() == "Magic Yipe! Armor",
          "Silly Mode armor names preserve resource 1251-1280 ordering");
}

void test_native_save() {
    const std::filesystem::path path =
        std::filesystem::temp_directory_path() / "yipe3-rebuild-save-test.y3r";
    yipe::Player original(yipe::CharacterType::wizard);
    original.name = "Save Test";
    original.health = 7;
    original.maximum_spell_points = 19;
    original.inventory[4] = 9;
    original.has_bonus_healing_ring = true;
    original.monster_repellent_steps = 77;

    yipe::save_player_atomic(path, original);
    const yipe::Player loaded = yipe::load_player(path);
    check(loaded.name == original.name && loaded.intelligence == original.intelligence,
          "native save restores identity and stats");
    check(loaded.health == 7 && loaded.maximum_spell_points == 19,
          "native save restores health and spell points");
    check(loaded.inventory == original.inventory, "native save restores inventory");
    check(loaded.has_bonus_healing_ring && loaded.monster_repellent_steps == 77,
          "native save preserves recovered item-effect state");
    check(!loaded.monster_defeat_history_complete,
          "player-only saves report missing defeat history");
    const yipe::GameState upgraded_from_v1 = yipe::load_game(path);
    check(upgraded_from_v1.player.name == original.name
              && upgraded_from_v1.location.map_id == 0x11
              && upgraded_from_v1.defeated_monster_placements.empty(),
          "version 1 player save loads with default world state");

    for (int mode = 0; mode <= 2; ++mode) {
        original.stat_cap_mode = mode;
        yipe::save_player_atomic(path, original);
        const auto older = yipe::load_game(path);
        check(older.player.stat_cap_mode == mode && older.player.maximum_stat() == 30 + mode * 10,
              "version 1 saves retain their stored stat/loot mode despite the new-player default");
        yipe::save_game_atomic(path, older);
        const auto current = yipe::load_player(path);
        check(current.stat_cap_mode == mode && current.maximum_stat() == 30 + mode * 10,
              "current native saves retain every mode after version 1 migration");
    }

    yipe::Player replacement(yipe::CharacterType::rogue);
    replacement.name = "Replacement";
    yipe::save_player_atomic(path, replacement);
    check(yipe::load_player(path).name == "Replacement",
          "native save atomically replaces an existing file");

    yipe::Player invalid = replacement;
    invalid.name.assign(65536, 'x');
    bool failed_write = false;
    try {
        yipe::save_player_atomic(path, invalid);
    } catch (const std::runtime_error&) {
        failed_write = true;
    }
    check(failed_write && yipe::load_player(path).name == "Replacement",
          "failed save leaves the previous valid file usable");

    std::vector<std::uint8_t> valid;
    {
        std::ifstream input(path, std::ios::binary);
        valid.assign(std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>());
    }
    for (const std::uint16_t version : {0, 16, 65535}) {
        auto unsupported = valid;
        unsupported[4] = static_cast<std::uint8_t>(version);
        unsupported[5] = static_cast<std::uint8_t>(version >> 8u);
        write_bytes(path, unsupported);
        bool rejected_version = false;
        try {
            static_cast<void>(yipe::load_game(path));
        } catch (const std::runtime_error&) {
            rejected_version = true;
        }
        check(rejected_version, "native game saves still reject versions outside 1 through 15");
    }
    write_bytes(path, valid);

    {
        std::fstream corrupt(path, std::ios::binary | std::ios::in | std::ios::out);
        corrupt.seekp(-1, std::ios::end);
        const char changed = 0x55;
        corrupt.write(&changed, 1);
    }
    bool rejected = false;
    try {
        static_cast<void>(yipe::load_player(path));
    } catch (const std::runtime_error&) {
        rejected = true;
    }
    check(rejected, "native save rejects checksum corruption");
    std::filesystem::remove(path);
}

void test_game_save() {
    const std::filesystem::path path =
        std::filesystem::temp_directory_path() / "yipe3-rebuild-game-save-test.y3r";
    yipe::GameState original;
    original.player.name = "World Save";
    original.player.experience = 41;
    original.player.gold = 43;
    original.player.weapon_rank = 3;
    original.player.gloves_rank = 1;
    original.player.difficulty = 3;
    original.player.combat_style = 1;
    original.player.specialty = 0;
    original.player.silly_mode = true;
    original.player.inventory[28] = 12;
    original.player.equipment_upgrade_count = 2;
    original.player.loot_roll_penalty = 7;
    original.player.ability_points = 2;
    original.player.maximum_ability_points = 5;
    original.player.special_ability = yipe::SpecialAbility::enrage;
    original.player.special_ability_duration = 8;
    original.player.enrage_base_dexterity = 13;
    original.player.enrage_base_intelligence = 10;
    original.player.enrage_base_strength = 16;
    original.player.restoration_uses = 3;
    original.player.has_waterproof_shoe_spray = true;
    original.player.has_rainbow_key = true;
    original.player.has_underworld_key = true;
    original.player.has_tree_climbing_shoes = true;
    original.player.spells[0] = 1;
    original.player.spells[11] = 1;
    original.player.quest_score = 1500;
    original.player.quest_progress_total = 73;
    original.player.monster_defeats[0] = 10;
    original.player.monster_defeats[34] = 3;
    original.player.monster_defeats[40] = 20;
    original.player.monster_defeats[49] = 40;
    original.player.gatekeeper_state = 2;
    original.player.stamps_found = 6;
    original.player.princesses_found = 14;
    original.player.princess_mystery_uses = 7;
    original.player.miscellaneous_shop_stage = 4;
    original.player.value_06a4 = 5;
    original.player.opened_chests[7] = 9;
    original.player.has_lucky_rabbit_foot = true;
    original.player.surface_return_map_id = 0x21;
    original.player.surface_return_region_x = -4;
    original.player.surface_return_region_y = 2;
    original.player.quest_flags[8] = true;
    original.player.inventory_values[0] = 0x8000;
    original.player.inventory_values[13] = 9;
    original.player.inventory_values[29] = 0xffff;
    original.player.encountered_monster_types[36] = true;
    original.player.encountered_monster_types[38] = true;
    original.player.encountered_monster_types[40] = true;
    original.player.death_count = 12;
    original.player.hell_gatekeeper_state = yipe::HellGatekeeperState::paid;
    original.player.entered_hell_through_secret_passage = true;
    original.player.bridge_treasure_available = false;
    original.player.healing_water_available = false;
    original.player.cave_coins_collected = true;
    original.player.snow_search_count = 3;
    original.player.has_defeated_wormoid = true;
    original.player.pending_relocation = 4;
    original.location = {{2, 8}, 0, 1, 0, false, 0x0b};
    original.defeated_monster_placements = {5, 14, 38, 185};
    original.random_state = 0x12345678u;
    original.map_tile_changes = {{0x2b, {6, 1}, 40}, {0x2b, {5, 8}, 34}};

    yipe::save_game_atomic(path, original);
    const yipe::GameState loaded = yipe::load_game(path);
    check(loaded.player.value_06a4 == 5
              && yipe::high_score_code(loaded.player) == yipe::high_score_code(original.player),
          "version 20 preserves the original High Score Code suffix field");
    check(loaded.player.miscellaneous_shop_stage == 4,
          "version 19 preserves miscellaneous-shop progression");
    check(loaded.player.name == original.player.name && loaded.player.experience == 41
              && loaded.player.gold == 43 && loaded.player.weapon_rank == 3
              && loaded.player.gloves_rank == 1,
          "version 17 save restores progression and equipment");
    check(loaded.player.difficulty == 3 && loaded.player.combat_style == 1
              && loaded.player.specialty == 0 && loaded.player.inventory[28] == 12
              && loaded.player.equipment_upgrade_count == 2
              && loaded.player.loot_roll_penalty == 7,
          "version 17 save restores specialty, portrait, and stat bias independently");
    check(loaded.player.silly_mode, "version 17 saves preserve Silly Mode");
    check(loaded.location.position == yipe::MapPosition{2, 8}
              && loaded.location.region_y == 1 && loaded.location.map_id == 0x0b,
          "version 17 save restores world location");
    check(loaded.defeated_monster_placements == original.defeated_monster_placements
              && loaded.random_state == 0x12345678u,
          "version 17 save restores encounter and random state");
    check(loaded.player.ability_points == 2
              && loaded.player.maximum_ability_points == 5
              && loaded.player.special_ability == yipe::SpecialAbility::enrage
              && loaded.player.special_ability_duration == 8
              && loaded.player.enrage_base_dexterity == 13
              && loaded.player.enrage_base_intelligence == 10
              && loaded.player.enrage_base_strength == 16,
          "version 17 save restores ability and active status state");
    check(loaded.player.restoration_uses == 3
              && loaded.player.has_waterproof_shoe_spray
              && loaded.player.has_rainbow_key
              && loaded.player.has_underworld_key
              && loaded.player.has_tree_climbing_shoes,
          "version 17 save restores special movement state");
    check(loaded.map_tile_changes == original.map_tile_changes,
          "version 17 save restores persistent map tile changes");
    check(loaded.player.spells == original.player.spells,
          "version 17 save restores learned spells");
    check(loaded.player.quest_score == 1500
              && loaded.player.quest_progress_total == 73
              && loaded.player.gatekeeper_state == 2
              && loaded.player.stamps_found == 6
              && loaded.player.princesses_found == 14
              && loaded.player.princess_mystery_uses == 7
              && loaded.player.has_lucky_rabbit_foot
              && loaded.player.quest_flags[8]
              && loaded.player.inventory_values[13] == 9,
          "version 17 save restores quest state");
    check(loaded.player.inventory_values == original.player.inventory_values
              && loaded.player.rescued_princess_count() == 1,
          "native saves preserve signed rescue-slot statistics independently of the quest counter");
    check(loaded.player.surface_return_map_id == 0x21
              && loaded.player.surface_return_region_x == -4
              && loaded.player.surface_return_region_y == 2,
          "version 17 save restores the surface-return anchor");
    check(loaded.player.encountered_monster_types
              == original.player.encountered_monster_types,
          "version 17 save restores per-monster encounter history");
    check(loaded.player.death_count == 12
              && loaded.player.hell_gatekeeper_state
                  == yipe::HellGatekeeperState::paid
              && loaded.player.entered_hell_through_secret_passage,
          "version 17 save restores death and Hell recovery state");
    check(!loaded.player.bridge_treasure_available
              && !loaded.player.healing_water_available
              && loaded.player.cave_coins_collected
              && loaded.player.snow_search_count == 3
              && loaded.player.pending_relocation == 4,
          "version 17 save restores Other action state and deferred travel");
    check(loaded.player.has_defeated_wormoid,
          "version 17 saves preserve the Wormoid-defeat hint flag");
    check(loaded.player.monster_defeats == original.player.monster_defeats
              && loaded.player.monster_defeat_history_complete,
          "version 17 preserves all 50 defeat counters, including reserved entries");

    {
        std::ifstream input(path, std::ios::binary);
        std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                        std::istreambuf_iterator<char>()};
        write_u32(bytes, bytes.size() - sizeof(std::int32_t) - 1 - 128 - 4, 4);
        write_u32(bytes, 10, test_crc32(bytes, 14));
        write_bytes(path, bytes);
        bool rejected_specialty = false;
        try {
            static_cast<void>(yipe::load_game(path));
        } catch (const std::runtime_error&) {
            rejected_specialty = true;
        }
        check(rejected_specialty, "version 17 rejects an invalid saved specialty");
        yipe::save_game_atomic(path, original);
    }

    {
        std::ifstream input(path, std::ios::binary);
        std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                       std::istreambuf_iterator<char>()};
        bytes[bytes.size() - 1 - 128 - 4] = 2;
        write_u32(bytes, 10, test_crc32(bytes, 14));
        write_bytes(path, bytes);
        bool rejected_mode = false;
        try {
            static_cast<void>(yipe::load_game(path));
        } catch (const std::runtime_error&) {
            rejected_mode = true;
        }
        check(rejected_mode, "version 17 rejects an invalid saved mode with a valid checksum");
        yipe::save_game_atomic(path, original);
        remove_save_extension(path, 133, 17);
        bool rejected_truncated_mode = false;
        try {
            static_cast<void>(yipe::load_game(path));
        } catch (const std::runtime_error&) {
            rejected_truncated_mode = true;
        }
        check(rejected_truncated_mode, "version 17 requires its mode flag");
        original.player.silly_mode = false;
        yipe::save_game_atomic(path, original);
        check(!yipe::load_game(path).player.silly_mode,
              "saving Normal Mode does not infer Silly Mode from the portrait");
    }

    constexpr std::size_t defeat_extension_size =
        yipe::Player::monster_defeat_table_size * sizeof(std::int32_t) + sizeof(std::uint8_t);
    for (const std::uint16_t value : {0, 4, 5, 0x8000, 0xffff}) {
        auto with_suffix = original;
        with_suffix.player.value_06a4 = value;
        yipe::save_game_atomic(path, with_suffix);
        check(yipe::load_game(path).player.value_06a4 == value,
              "version 20 retains the raw suffix field without inventing a value restriction");
    }
    for (const std::size_t missing : {1, 2}) {
        yipe::save_game_atomic(path, original);
        remove_save_extension(path, missing, 20);
        bool rejected = false;
        try { static_cast<void>(yipe::load_game(path)); }
        catch (const std::runtime_error&) { rejected = true; }
        check(rejected, "version 20 requires both suffix-field bytes with a valid checksum");
    }
    yipe::save_game_atomic(path, original);
    {
        std::ifstream input(path, std::ios::binary);
        std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                       std::istreambuf_iterator<char>()};
        bytes[bytes.size() - 4] = 5;
        write_u32(bytes, 10, test_crc32(bytes, 14));
        write_bytes(path, bytes);
        bool rejected = false;
        try { static_cast<void>(yipe::load_game(path)); }
        catch (const std::runtime_error&) { rejected = true; }
        check(rejected, "version 19 rejects an invalid shop stage with a valid checksum");
        yipe::save_game_atomic(path, original);
        remove_save_extension(path, 3, 19);
        rejected = false;
        try { static_cast<void>(yipe::load_game(path)); }
        catch (const std::runtime_error&) { rejected = true; }
        check(rejected, "version 19 requires both shop-stage bytes");
        yipe::save_game_atomic(path, original);
        auto invalid = original;
        invalid.player.miscellaneous_shop_stage = 5;
        rejected = false;
        try { yipe::save_game_atomic(path, invalid); }
        catch (const std::runtime_error&) { rejected = true; }
        check(rejected && yipe::load_game(path).player.miscellaneous_shop_stage == 4,
              "invalid shop state cannot overwrite a valid native save");
    }
    remove_save_extension(path, 2, 19);
    const auto version_19 = yipe::load_game(path);
    check(version_19.player.value_06a4 == 0
              && version_19.player.miscellaneous_shop_stage == 4
              && version_19.player.monster_defeats == original.player.monster_defeats,
          "version 19 preserves shop and defeat state while defaulting the unavailable suffix field");
    remove_save_extension(path, 2, 18);
    const auto version_18 = yipe::load_game(path);
    check(version_18.player.miscellaneous_shop_stage == 0
              && version_18.player.opened_chests == original.player.opened_chests,
          "version 18 preserves chest flags and defaults the newly recovered shop stage");
    remove_save_extension(path, 128, 17);
    check(yipe::load_game(path).player.opened_chests == std::array<std::uint8_t, 128>{},
          "version 17 defaults to unopened chests because it had no chest gameplay");
    remove_save_extension(path, 1, 16);
    const yipe::GameState loaded_version_16 = yipe::load_game(path);
    check(!loaded_version_16.player.silly_mode && loaded_version_16.player.specialty == 0
              && loaded_version_16.player.inventory[28] == 12
              && loaded_version_16.player.monster_defeats == original.player.monster_defeats,
          "version 16 keeps specialty and portrait while defaulting to Normal Mode");
    remove_save_extension(path, sizeof(std::int32_t), 15);
    const yipe::GameState loaded_version_15 = yipe::load_game(path);
    check(loaded_version_15.player.specialty == 1
              && loaded_version_15.player.combat_style == 1
              && loaded_version_15.player.inventory[28] == 12
              && loaded_version_15.player.monster_defeats == original.player.monster_defeats,
          "version 15 infers its former class while preserving portrait and defeat counts");

    remove_save_extension(path, defeat_extension_size, 14);
    yipe::GameState upgraded_version_14 = yipe::load_game(path);
    check(!upgraded_version_14.player.monster_defeat_history_complete
              && upgraded_version_14.player.monster_defeats
                  == std::array<std::int32_t, 50>{}
              && upgraded_version_14.player.quest_progress_total == 73
              && upgraded_version_14.player.has_defeated_wormoid,
          "version 14 retains the aggregate and Wormoid predicate without inventing counts");

    remove_save_extension(path, sizeof(std::uint8_t), 13);
    const yipe::GameState loaded_version_13 = yipe::load_game(path);
    check(!loaded_version_13.player.has_defeated_wormoid
              && loaded_version_13.player.cave_coins_collected
              && loaded_version_13.player.snow_search_count == 3,
          "version 13 saves retain Other state without inventing missing defeat history");

    remove_save_extension(path, 3 * sizeof(std::uint8_t) + sizeof(std::uint16_t), 12);
    const yipe::GameState loaded_version_12 = yipe::load_game(path);
    check(loaded_version_12.player.death_count == 12
              && loaded_version_12.player.hell_gatekeeper_state == yipe::HellGatekeeperState::paid
              && loaded_version_12.player.entered_hell_through_secret_passage
              && loaded_version_12.player.bridge_treasure_available
              && loaded_version_12.player.healing_water_available
              && !loaded_version_12.player.cave_coins_collected
              && loaded_version_12.player.snow_search_count == 0,
          "version 12 saves retain death state and default Other action state");

    constexpr std::size_t death_extension_size = sizeof(std::int32_t)
        + sizeof(std::uint16_t) + sizeof(std::uint8_t);
    remove_save_extension(path, death_extension_size, 11);
    const yipe::GameState loaded_version_11 = yipe::load_game(path);
    check(loaded_version_11.player.encountered_monster_types
              == original.player.encountered_monster_types
              && loaded_version_11.player.death_count == 0
              && loaded_version_11.player.hell_gatekeeper_state
                  == yipe::HellGatekeeperState::absent
              && !loaded_version_11.player.entered_hell_through_secret_passage,
          "version 11 saves retain encounter history and default death state");

    remove_save_extension(
        path,
        yipe::Player::monster_history_size * sizeof(std::uint8_t), 10);
    const yipe::GameState loaded_version_10 = yipe::load_game(path);
    check(loaded_version_10.player.surface_return_region_x == -4
              && loaded_version_10.player.surface_return_region_y == 2
              && std::none_of(
                  loaded_version_10.player.encountered_monster_types.begin(),
                  loaded_version_10.player.encountered_monster_types.end(),
                  [](bool encountered) { return encountered; }),
          "version 10 saves retain the return region and default encounter history");

    remove_save_extension(path, 2 * sizeof(std::uint16_t), 9);
    const yipe::GameState loaded_version_9 = yipe::load_game(path);
    check(loaded_version_9.defeated_monster_placements
                 == std::vector<std::uint16_t>({5, 38, 185})
              && loaded_version_9.temporarily_defeated_monster_placements
                 == std::vector<std::uint16_t>({14})
              && loaded_version_9.player.surface_return_region_x == 0
              && loaded_version_9.player.surface_return_region_y == 1,
          "version 9 saves migrate repeatable encounters and derive the missing return region");

    remove_save_extension(path, sizeof(std::uint32_t)
                                    + original.defeated_monster_placements.size()
                                        * sizeof(std::uint16_t),
                          8);
    const yipe::GameState loaded_version_8 = yipe::load_game(path);
    check(loaded_version_8.defeated_monster_placements
                  == std::vector<std::uint16_t>{5}
              && loaded_version_8.player.princess_mystery_uses == 7,
          "version 8 saves migrate the first encounter and retain princess state");

    remove_save_extension(path, sizeof(std::uint16_t), 7);
    const yipe::GameState loaded_version_7 = yipe::load_game(path);
    check(loaded_version_7.player.quest_score == 1500
              && loaded_version_7.player.princesses_found == 14
              && loaded_version_7.player.princess_mystery_uses == 0,
          "version 7 saves retain King state and default the mystery counter");

    constexpr std::size_t quest_extension_size = 2 * sizeof(std::int32_t)
        + sizeof(std::uint8_t) + yipe::Player::quest_flag_count * sizeof(std::uint8_t)
        + yipe::Player::inventory_size * sizeof(std::uint16_t)
        + 3 * sizeof(std::uint16_t);
    remove_save_extension(path, quest_extension_size, 6);
    const yipe::GameState loaded_version_6 = yipe::load_game(path);
    check(loaded_version_6.player.spells == original.player.spells
              && loaded_version_6.player.quest_score == 0
              && !loaded_version_6.player.has_lucky_rabbit_foot,
          "version 6 saves retain learned spells and default quest state");

    remove_save_extension(path,
                          yipe::Player::spell_count * sizeof(std::uint16_t), 5);
    const yipe::GameState loaded_version_5 = yipe::load_game(path);
    check(loaded_version_5.map_tile_changes == original.map_tile_changes
              && loaded_version_5.player.spells[0] == 1,
          "version 5 saves retain map changes and default learned spells");

    constexpr std::size_t encoded_change_size = 3 * sizeof(std::int32_t)
                                                + sizeof(std::uint16_t);
    remove_save_extension(path, sizeof(std::uint32_t)
                                    + original.map_tile_changes.size() * encoded_change_size,
                          4);
    const yipe::GameState loaded_version_4 = yipe::load_game(path);
    check(loaded_version_4.player.restoration_uses == 3
              && loaded_version_4.player.has_rainbow_key
              && loaded_version_4.map_tile_changes.empty(),
          "version 4 saves retain movement state and default map changes");

    remove_save_extension(path, sizeof(std::int32_t) + 4, 3);
    const yipe::GameState loaded_version_3 = yipe::load_game(path);
    check(loaded_version_3.player.special_ability == yipe::SpecialAbility::enrage
              && loaded_version_3.player.special_ability_duration == 8
              && loaded_version_3.player.restoration_uses == 0
              && !loaded_version_3.player.has_rainbow_key,
          "version 3 saves retain ability state and default new movement state");

    remove_save_extension(path, 7 * sizeof(std::int32_t), 2);
    const yipe::GameState loaded_version_2 = yipe::load_game(path);
    check(loaded_version_2.player.name == original.player.name
              && loaded_version_2.player.experience == 41
              && loaded_version_2.location.map_id == 0x0b
              && loaded_version_2.random_state == 0x12345678u,
          "version 2 game saves remain readable");
    check(loaded_version_2.player.ability_points == 5
              && loaded_version_2.player.maximum_ability_points == 5
              && loaded_version_2.player.special_ability == yipe::SpecialAbility::none
              && !loaded_version_2.player.monster_defeat_history_complete,
          "version 2 saves receive default ability state");
    upgraded_version_14.player.record_monster_defeat(2);
    yipe::save_game_atomic(path, upgraded_version_14);
    const yipe::GameState resumed_upgrade = yipe::load_game(path);
    check(!resumed_upgrade.player.monster_defeat_history_complete
              && resumed_upgrade.player.monster_defeats[1] == 1
              && resumed_upgrade.player.quest_progress_total == 74
              && resumed_upgrade.player.has_defeated_wormoid
              && resumed_upgrade.player.monster_defeats[34] == 0,
          "resaving an upgraded game keeps partial counts and the historical hint predicate");
    original.player.monster_defeats[49] = std::numeric_limits<std::int32_t>::min();
    yipe::save_game_atomic(path, original);
    check(yipe::load_game(path).player.monster_defeats[49]
              == std::numeric_limits<std::int32_t>::min(),
          "native saves retain the signed bit pattern of a wrapped defeat counter");
    remove_save_extension(path, sizeof(std::int32_t) + 2 + 128 + 4, 15);
    bool rejected_truncated_counts = false;
    try {
        static_cast<void>(yipe::load_game(path));
    } catch (const std::runtime_error&) {
        rejected_truncated_counts = true;
    }
    check(rejected_truncated_counts,
          "version 15 rejects an incomplete extension even with a valid checksum");
    std::filesystem::remove(path);
}

std::vector<std::uint8_t> legacy_save_bytes(const std::string& name,
                                          std::int32_t wormoid_defeats = 1034,
                                          std::int32_t ability = 2,
                                          std::uint16_t shop_stage = 2,
                                          std::uint16_t value_06a4 = 0) {
    std::vector<std::uint8_t> bytes;
    if (name.size() < 0xff) {
        bytes.push_back(static_cast<std::uint8_t>(name.size()));
    } else {
        bytes.push_back(0xff);
        append_u16(bytes, static_cast<std::uint16_t>(name.size()));
    }
    bytes.insert(bytes.end(), name.begin(), name.end());

    for (const std::int32_t value : std::array<std::int32_t, 13>{
             13, 12, 11, 7, 13, 4, 9, 2, 6, 8, 123, 6, 7}) {
        append_i32(bytes, value);
    }
    append_i32(bytes, 533);
    for (const std::uint16_t value : std::array<std::uint16_t, 5>{1, 2, 3, 4, 5}) {
        append_u16(bytes, value);
    }
    append_i32(bytes, 2);
    append_u16(bytes, 9);
    append_i32(bytes, 3);
    append_u16(bytes, 1);
    append_u16(bytes, 2);
    append_i32(bytes, ability);
    append_i32(bytes, 55);

    for (const bool value : std::array{false, true, true, false, true, true, true}) {
        append_i32(bytes, value ? 1 : 0);
    }
    for (std::size_t index = 0; index < 512; ++index) {
        append_i32(bytes, index == 7 ? 1 : 0);
        append_i32(bytes, index == 8 ? 1 : 0);
    }
    for (std::uint16_t index = 0; index < 15; ++index) {
        append_u16(bytes, static_cast<std::uint16_t>(index + 1));
    }
    for (std::size_t index = 0; index < 10; ++index) {
        append_i32(bytes, index == 4 ? 1 : 0);
        append_u16(bytes, index < 2 ? 1 : 0);
    }
    for (std::size_t index = 0; index < 30; ++index) {
        append_u16(bytes, index == 0 ? 5 : index == 29 ? 9 : 0);
        append_u16(bytes, static_cast<std::uint16_t>(index + 10));
        append_i32(bytes, index == 29 ? 1 : 0);
    }
    for (std::size_t index = 0; index < 50; ++index) {
        std::uint16_t value = 0;
        switch (index) {
        case 2: value = 2; break;
        case 3: value = 1; break;
        case 4: value = 0xfffc; break;
        case 5: value = 0xffff; break;
        case 6: value = 125; break;
        case 8: value = 4; break;
        case 9: value = 11; break;
        case 11: value = 1; break;
        case 14: value = 1; break;
        case 15: value = 3; break;
        case 16: value = 85; break;
        case 17: value = 22; break;
        case 18: value = 7; break;
        case 19: value = 0xffff; break;
        case 20: value = shop_stage; break;
        case 26: value = 2; break;
        case 27: value = 1; break;
        case 30: value = 3; break;
        case 31: value = 1; break;
        case 35: value = value_06a4; break;
        default: break;
        }
        append_u16(bytes, value);
        append_i32(bytes, index == 34 ? wormoid_defeats
                                     : static_cast<std::int32_t>(1000 + index));
        append_i32(bytes, index == 4 || index == 36 || index == 38 ? 1 : 0);
    }
    for (const std::uint16_t value : std::array<std::uint16_t, 5>{2, 8, 0, 1, 0}) {
        append_u16(bytes, value);
    }
    append_i32(bytes, 797);
    append_i32(bytes, 4321);
    append_i32(bytes, 1);
    return bytes;
}

void test_legacy_save_import() {
    const std::filesystem::path path =
        std::filesystem::temp_directory_path() / "yipe3-legacy-save-test.yp3";
    std::vector<std::uint8_t> bytes = legacy_save_bytes("Legacy");
    check(bytes.size() == 1 + 6 + 5064,
          "synthetic legacy fixture has the statically recovered payload size");
    write_bytes(path, bytes);

    const yipe::LegacySave imported = yipe::load_legacy_save(path);
    const yipe::Player& player = imported.game.player;
    check(!player.silly_mode, "legacy saves omit the mode flag and import in Normal Mode");
    check(player.stat_cap_mode == 2 && player.maximum_stat() == 50,
          "legacy saves omit the profile and deliberately adopt the rebuild's supplied-content limits");
    check(player.name == "Legacy" && player.dexterity == 13
              && player.intelligence == 12 && player.strength == 11
              && player.health == 7 && player.maximum_health == 13,
          "legacy import restores identity and primary stats");
    check(player.spell_points == 4 && player.maximum_spell_points == 9
              && player.ability_points == 2 && player.maximum_ability_points == 6
              && player.level == 8,
          "legacy import restores resource pools and level");
    check(player.weapon_rank == 6 && player.armor_rank == 7
              && player.boots_rank == 1 && player.gloves_rank == 2
              && player.helmet_rank == 3 && player.ring_rank == 4
              && player.necklace_rank == 5,
          "legacy import restores equipment ranks");
    check(player.gold == 1000 && player.experience == 4321
              && imported.stored_validation == 533 && imported.validation_passed,
          "legacy import reverses the gold transform and verifies validation data");
    check(imported.game.location.position == yipe::MapPosition{2, 8}
              && imported.game.location.region_y == 1
              && imported.game.location.map_id == 0x0b,
          "legacy import derives the original world location");
    check(player.difficulty == 2 && player.restoration_uses == 3
              && player.combat_style == 3 && player.has_waterproof_shoe_spray
              && player.has_rainbow_key && player.has_underworld_key
              && player.has_tree_climbing_shoes && player.death_count == 2
              && player.entered_hell_through_secret_passage
              && player.hell_gatekeeper_state
                  == yipe::HellGatekeeperState::paid,
          "legacy import maps recovered difficulty, movement, and combat state");
    check(player.bridge_treasure_available && player.healing_water_available
              && player.cave_coins_collected && player.snow_search_count == 3,
          "legacy import maps the Other action flags and snow-search counter");
    check(player.inventory[0] == 5 && player.inventory[29] == 9
              && player.spells == imported.values_010c
              && player.has_bonus_healing_ring && player.monster_repellent_steps == 22
              && player.surface_return_map_id == 1
              && player.surface_return_region_x == -4
              && player.surface_return_region_y == -1,
          "legacy import maps inventory, spells, and recovered extended fields");
    check(player.quest_score == 123 && player.has_lucky_rabbit_foot
              && player.quest_flags[29] && player.inventory_values[29] == 39
              && player.quest_progress_total == 51225
              && player.gatekeeper_state == 2 && player.stamps_found == 4
              && player.princesses_found == 11
              && player.princess_mystery_uses == 7
              && player.encountered_monster_types[36]
              && player.encountered_monster_types[38],
          "legacy import maps recovered King quest state");
    check(player.rescued_princess_count() == 30,
          "legacy rescue statistics count the imported slots, not the separate quest counter");
    check(player.special_ability == yipe::SpecialAbility::frighten && imported.unknown_0074 == 2,
          "legacy imports map the learned ability while retaining the raw field");
    check(player.has_defeated_wormoid && !player.encountered_monster_types[35],
          "legacy import reads Wormoid defeat index 34 independently of encounter flags");
    check(player.monster_defeat_history_complete,
          "legacy saves contain a complete defeat table");
    for (std::size_t index = 0; index < player.monster_defeats.size(); ++index) {
        check(player.monster_defeats[index] == static_cast<std::int32_t>(1000 + index),
              "legacy import preserves defeat counter " + std::to_string(index));
    }
    check(imported.flags_0152[7] && imported.flags_0352[8]
              && imported.values_010c[14] == 15
              && imported.flag_values[4].flag_0148
              && imported.inventory_entries[29].value_00d0 == 39
              && imported.inventory_entries[29].flag_012a
              && imported.extended_entries[4].value_0584 == 1004
              && imported.extended_entries[4].flag_0552
              && imported.trailing_healing_flag,
          "legacy import retains unresolved serialized fields");
    std::array<std::uint8_t, 128> legacy_chests{};
    legacy_chests[1] = 8;
    check(player.opened_chests == legacy_chests,
          "legacy chest index 7 maps to map 2 slot 3 independently of the interleaved table");
    check(player.miscellaneous_shop_stage == 2,
          "legacy +0x686 maps the shop stage independently of the adjacent Hell state");

    const std::filesystem::path native_path =
        std::filesystem::temp_directory_path() / "yipe3-legacy-ability-test.y3r";
    for (const std::uint16_t value : {0, 4, 5, 0x8000, 0xffff}) {
        write_bytes(path, legacy_save_bytes("Legacy", 1034, 2, 2, value));
        const auto imported_code = yipe::load_legacy_save(path);
        yipe::save_game_atomic(native_path, imported_code.game);
        const auto converted = yipe::load_game(native_path);
        check(imported_code.extended_entries[35].value_065e == value
                  && imported_code.game.player.value_06a4 == value
                  && converted.player.value_06a4 == value
                  && yipe::high_score_code(converted.player)
                      == std::string("0L7W2L901W853L44W6W855W2") + (value == 5 ? "W" : ""),
              "legacy +0x6a4 and all 50 defeat counters produce the same code after native conversion");
    }
    for (int ability = 0; ability <= 4; ++ability) {
        write_bytes(path, legacy_save_bytes("Legacy", 1034, ability));
        const auto learned = yipe::load_legacy_save(path);
        check(static_cast<int>(learned.game.player.special_ability) == ability
                  && learned.unknown_0074 == ability,
              "legacy imports restore each supported learned ability");
        yipe::save_game_atomic(native_path, learned.game);
        const auto converted = yipe::load_game(native_path);
        check(converted.player.special_ability == learned.game.player.special_ability
                  && converted.player.miscellaneous_shop_stage == 2,
              "the imported learned ability and shop stage survive conversion to native saves");
    }
    std::filesystem::remove(native_path);
    for (std::uint16_t stage = 0; stage <= 4; ++stage) {
        write_bytes(path, legacy_save_bytes("Legacy", 1034, 2, stage));
        const auto imported_shop = yipe::load_legacy_save(path).game;
        yipe::save_game_atomic(native_path, imported_shop);
        check(imported_shop.player.miscellaneous_shop_stage == stage
                  && yipe::load_game(native_path).player.miscellaneous_shop_stage == stage
                  && yipe::load_game(native_path).player.stat_cap_mode == 2,
              "all original shop stages survive legacy-to-native conversion");
    }
    std::filesystem::remove(native_path);
    write_bytes(path, legacy_save_bytes("Legacy", 1034, 2, 5));
    bool rejected_shop = false;
    try { static_cast<void>(yipe::load_legacy_save(path)); }
    catch (const std::runtime_error&) { rejected_shop = true; }
    check(rejected_shop, "legacy imports explicitly reject unsupported shop stages");
    for (const int ability : {-1, 5, 256}) {
        write_bytes(path, legacy_save_bytes("Legacy", 1034, ability));
        bool rejected_ability = false;
        try {
            static_cast<void>(yipe::load_legacy_save(path));
        } catch (const std::runtime_error&) {
            rejected_ability = true;
        }
        check(rejected_ability, "legacy imports reject unsupported ability IDs without narrowing them");
    }

    write_bytes(path, legacy_save_bytes("Legacy", 0));
    check(!yipe::load_legacy_save(path).game.player.has_defeated_wormoid,
          "other legacy defeat counters do not suppress the giant hint");
    write_bytes(path, legacy_save_bytes("Legacy", std::numeric_limits<std::int32_t>::max()));
    const auto wrapped_legacy = yipe::load_legacy_save(path);
    check(wrapped_legacy.game.player.monster_defeats[34]
                  == std::numeric_limits<std::int32_t>::max()
              && wrapped_legacy.game.player.quest_progress_total == static_cast<std::int32_t>(
                  51225u - 1034u + 0x7fffffffu),
          "legacy defeat aggregation preserves signed 32-bit wrapping without overflow");

    std::vector<std::uint8_t> invalid_validation = bytes;
    write_u32(invalid_validation, 1 + 6 + 13 * sizeof(std::int32_t), 0);
    write_bytes(path, invalid_validation);
    check(!yipe::load_legacy_save(path).validation_passed,
          "legacy import reports validation failure without rejecting the save");

    const std::string long_name(300, 'L');
    write_bytes(path, legacy_save_bytes(long_name));
    check(yipe::load_legacy_save(path).game.player.name == long_name,
          "legacy import supports the MFC 16-bit CString length form");

    bytes.pop_back();
    write_bytes(path, bytes);
    bool rejected = false;
    try {
        static_cast<void>(yipe::load_legacy_save(path));
    } catch (const std::runtime_error&) {
        rejected = true;
    }
    check(rejected, "legacy import rejects truncated payloads");

    write_bytes(path, {0xff, 0xfe, 0xff});
    rejected = false;
    try {
        static_cast<void>(yipe::load_legacy_save(path));
    } catch (const std::runtime_error&) {
        rejected = true;
    }
    check(rejected, "legacy import rejects unsupported Unicode archive strings");
    std::filesystem::remove(path);
}

void test_original_auxiliary_data() {
    const std::filesystem::path evidence =
        std::filesystem::path(YIPE_TEST_EVIDENCE_DIR) / "Yipe3-2026-09-15.dat";
    const yipe::OriginalAuxiliaryData data = yipe::load_original_auxiliary_data(evidence);

    check(data.high_scores[0].name == "----" && data.high_scores[0].value == 0,
          "original auxiliary reader decodes default high score entries");
    check(data.high_scores[15].name == "Yip" && data.high_scores[16].name == "Youcef",
          "original auxiliary reader decodes captured names");
    check(data.high_scores[24].value == 1 && data.high_scores[25].value == 1,
          "original auxiliary reader decodes captured score values");
    check(data.keys_0174_to_0182
              == std::array<std::uint16_t, 8>{0x67, 0x26, 0x69, 0x25,
                                               0x27, 0x61, 0x28, 0x63},
          "original auxiliary reader preserves primary key order");
    check(data.keys_0184_to_018c
              == std::array<std::uint16_t, 5>{0x52, 0x43, 0x49, 0x53, 0x41},
          "original auxiliary reader restores in-memory combat key order");
    check(data.value_0190 == 1302 && data.value_0170 == 0 && data.value_016c == 4529,
          "original auxiliary reader decodes captured registration-related values");
    const yipe::GameSettings imported = yipe::settings_from_original(data);
    check(imported.keys == yipe::default_key_bindings(),
          "original auxiliary keys import into native settings");

    const std::filesystem::path temporary =
        std::filesystem::temp_directory_path() / "yipe3-truncated-auxiliary-test.dat";
    {
        std::ifstream input(evidence, std::ios::binary);
        std::vector<char> bytes{std::istreambuf_iterator<char>(input),
                                std::istreambuf_iterator<char>()};
        bytes.pop_back();
        std::ofstream output(temporary, std::ios::binary | std::ios::trunc);
        output.write(bytes.data(), static_cast<std::streamsize>(bytes.size()));
    }
    bool rejected = false;
    try {
        static_cast<void>(yipe::load_original_auxiliary_data(temporary));
    } catch (const std::runtime_error&) {
        rejected = true;
    }
    check(rejected, "original auxiliary reader rejects truncation");
    std::filesystem::remove(temporary);
}

void test_high_score_code() {
    yipe::Player player;
    constexpr auto empty = "0L0W0L1000W0L0W0W0W2";
    check(player.value_06a4 == 0 && yipe::high_score_code(player) == empty,
          "new characters have the original empty High Score Code and no suffix");
    player.monster_defeats.fill(1);
    check(yipe::high_score_code(player) == "0L0W0L1000W0L49W0W0W2",
          "High Score Code includes all 50 counters, not the 41 shown in Kills");
    player.monster_defeats.fill(0);
    player.level = 15;
    player.gold = 450;
    player.monster_defeats[49] = 60;
    check(yipe::high_score_code(player) == "0L14W0L551W0L59W0W0W2",
          "exact divisor inputs still encode the last remainder of the preceding group");
    ++player.level;
    ++player.gold;
    ++player.monster_defeats[49];
    check(yipe::high_score_code(player) == "1L0W1L1000W1L0W0W3W2",
          "all three quotients advance one above their divisors and contribute to the checksum");
    player.level = 31;
    player.gold = 902;
    player.monster_defeats[49] = 122;
    player.gatekeeper_state = 3;
    player.difficulty = 3;
    check(yipe::high_score_code(player) == "2L0W2L999W2L1W9W6W3",
          "code encodes gold inversion, Gatekeeper times three, quotient sum, and difficulty in order");
    for (const std::uint16_t value : {0, 4, 5, 0x8005, 0xffff}) {
        player.value_06a4 = value;
        check(yipe::high_score_code(player)
                  == std::string("2L0W2L999W2L1W9W6W3") + (value == 5 ? "W" : ""),
              "only exact raw +0x6a4 value five adds a trailing W");
    }
    player = yipe::Player{};
    for (int value : {0, 1, -1, std::numeric_limits<std::int32_t>::min()}) {
        player.level = player.gold = player.monster_defeats[0] = value;
        check(yipe::high_score_code(player) == empty,
              "nonpositive inputs and one have zero quotient/remainder without signed overflow");
    }
    player.level = player.gold = player.monster_defeats[0] = std::numeric_limits<std::int32_t>::max();
    check(yipe::high_score_code(player) == "143165576L6W4772185L604W35791394L6W0W183729155W2",
          "maximum signed inputs preserve the original loop's result without billions of iterations");
    player = yipe::Player{};
    player.monster_defeats[0] = std::numeric_limits<std::int32_t>::max();
    player.monster_defeats[49] = 1;
    check(yipe::high_score_code(player) == empty,
          "defeat summation wraps to a signed negative value before encoding");
    player.monster_defeats[0] = -1;
    player.monster_defeats[49] = 61;
    player.gatekeeper_state = 0x8000;
    player.difficulty = 0x8002;
    check(yipe::high_score_code(player) == "0L0W0L1000W0L59W-98304W0W-32766",
          "counter cancellation and signed-short Gatekeeper/difficulty match the original");
    player = yipe::Player{};
    player.monster_defeat_history_complete = false;
    player.quest_progress_total = 900;
    player.monster_defeats[49] = 61;
    check(yipe::high_score_code(player) == "0L0W0L1000W1L0W0W1W2",
          "code uses recorded counters without silently substituting old saves' unattributed aggregate");
}

void test_high_scores() {
    const yipe::HighScores defaults = yipe::default_high_scores();
    check(std::all_of(defaults.begin(), defaults.end(), [](const yipe::HighScoreEntry& entry) {
              return entry.name == "----" && entry.value == 0;
          }), "all 45 high scores start with the recovered placeholder and zero value");

    yipe::Player player;
    player.name = "Hero";
    player.record_monster_defeat(2);
    player.record_monster_defeat(41);
    player.record_monster_defeat(50);
    player.inventory_values[0] = 1;
    player.inventory_values[29] = 0x7fff;
    player.inventory_values[1] = 0x8000;
    player.inventory_values[2] = 0xffff;
    player.princesses_found = 20;
    player.gold = 789;
    player.level = 7;
    constexpr std::array<std::pair<int, std::size_t>, 9> groups{{
        {1, 0}, {2, 15}, {3, 30}, {0, 0}, {-1, 0}, {4, 0},
        {65538, 15}, {65539, 30}, {-65534, 15},
    }};
    for (const auto [difficulty, group] : groups) {
        player.difficulty = difficulty;
        auto scores = defaults;
        check(yipe::high_score_group_offset(difficulty) == group,
              "high-score difficulty uses signed-short groups and the Easy fallback");
        check(yipe::update_high_scores(scores, player, true),
              "a new player's scores and completion change the table");
        auto expected = defaults;
        expected[group] = {"Hero", 3};
        expected[group + 3] = {"Hero", 2};
        expected[group + 6] = {"Hero", 789};
        expected[group + 9] = {"Hero", 7};
        expected[group + 12] = {"Hero", 0};
        check(scores == expected,
              "scores use all 50 kills, signed positive rescue slots, cash, level, and names only");
    }
    player.difficulty = 1;
    player.monster_defeat_history_complete = false;
    player.quest_progress_total = 103;
    player.inventory_values.fill(1);
    auto scores = defaults;
    static_cast<void>(yipe::update_high_scores(scores, player));
    check(scores[0].value == 103 && scores[3].value == 30,
          "scores retain historical native-save kills and count all 30 rescue slots");

    scores = defaults;
    player = yipe::Player{};
    player.difficulty = 1;
    player.gold = 0;
    player.level = 0;
    static_cast<void>(yipe::update_high_scores(scores, player));
    for (std::size_t category = 0; category < 4; ++category) {
        check(scores[category * 3] == yipe::HighScoreEntry{player.name, 0},
              "zero scores qualify and move ahead of empty zero rows");
    }
    check(!yipe::update_high_scores(scores, player), "identical first-place scores are unchanged");

    scores = defaults;
    for (std::size_t category = 0; category < 4; ++category) {
        scores[category * 3] = {"A", 30};
        scores[category * 3 + 1] = {"B", 20};
        scores[category * 3 + 2] = {"C", 10};
    }
    player.name = "D";
    player.gold = 9;
    const auto before_rejection = scores;
    check(!yipe::update_high_scores(scores, player) && scores == before_rejection,
          "a new name below third place leaves the table unchanged");
    player.gold = 10;
    check(yipe::update_high_scores(scores, player)
              && scores[6] == yipe::HighScoreEntry{"A", 30}
              && scores[7] == yipe::HighScoreEntry{"B", 20}
              && scores[8] == yipe::HighScoreEntry{"D", 10},
          "a new third-place tie replaces the previous third-place name");
    player.gold = 20;
    check(yipe::update_high_scores(scores, player)
              && scores[7] == yipe::HighScoreEntry{"D", 20}
              && scores[8] == yipe::HighScoreEntry{"B", 20},
          "same-name improvement moves ahead of a tied preceding row");
    player.gold = 30;
    check(yipe::update_high_scores(scores, player)
              && scores[6] == yipe::HighScoreEntry{"D", 30}
              && scores[7] == yipe::HighScoreEntry{"A", 30},
          "same-name improvement can reach first place without duplicating the name");
    player.gold = 40;
    check(yipe::update_high_scores(scores, player) && scores[6].value == 40,
          "first-place same-name improvement reports a change for native persistence");
    const auto before_regression = scores;
    player.gold = 25;
    check(!yipe::update_high_scores(scores, player) && scores == before_regression,
          "a lower score never replaces or reinserts the existing name");
    player.name = "d";
    player.gold = 40;
    check(yipe::update_high_scores(scores, player)
              && scores[6] == yipe::HighScoreEntry{"d", 40}
              && scores[7] == yipe::HighScoreEntry{"D", 40}
              && scores[8] == yipe::HighScoreEntry{"A", 30},
          "case-sensitive new names move ahead of ties and evict the third row");
    player.name = "D";
    check(yipe::update_high_scores(scores, player)
              && scores[6] == yipe::HighScoreEntry{"D", 40}
              && scores[7] == yipe::HighScoreEntry{"d", 40}
              && std::count_if(scores.begin() + 6, scores.begin() + 9,
                     [](const yipe::HighScoreEntry& entry) { return entry.name == "D"; }) == 1,
          "equal same-name scores move ahead of ties without adding another record");
    check(!yipe::update_high_scores(scores, player), "an unchanged table does not request a write");
    scores[6] = {"Duplicate", 30};
    scores[7] = {"Other", 20};
    scores[8] = {"Duplicate", 10};
    player.name = "Duplicate";
    player.gold = 25;
    static_cast<void>(yipe::update_high_scores(scores, player));
    check(scores[6] == yipe::HighScoreEntry{"Duplicate", 30}
              && scores[7] == yipe::HighScoreEntry{"Duplicate", 25}
              && scores[8] == yipe::HighScoreEntry{"Other", 20},
          "imported duplicate names preserve the original last-match update rule");

    scores = defaults;
    scores[12].value = 7;
    scores[13].value = 8;
    scores[14].value = 9;
    player.name = "First";
    static_cast<void>(yipe::update_high_scores(scores, player));
    check(scores[12].name == "----", "ordinary submissions do not record game completion");
    check(yipe::update_high_scores(scores, player, true)
              && scores[12] == yipe::HighScoreEntry{"First", 7},
          "completion-only changes report a write without modifying numeric values");
    check(!yipe::update_high_scores(scores, player, true) && scores[13].name == "----",
          "the same finisher cannot occupy two completion slots");
    player.name = "first";
    static_cast<void>(yipe::update_high_scores(scores, player, true));
    player.name = "Third";
    static_cast<void>(yipe::update_high_scores(scores, player, true));
    player.name = "Fourth";
    player.gold = 999;
    static_cast<void>(yipe::update_high_scores(scores, player, true));
    check(scores[12] == yipe::HighScoreEntry{"First", 7}
              && scores[13] == yipe::HighScoreEntry{"first", 8}
              && scores[14] == yipe::HighScoreEntry{"Third", 9},
          "the first three case-sensitive finishers keep arrival order and are never evicted");
}

void test_high_score_save() {
    const std::filesystem::path path =
        std::filesystem::temp_directory_path() / "yipe3-high-score-test.y3r";
    auto scores = yipe::default_high_scores();
    for (std::size_t index = 0; index < scores.size(); ++index) {
        scores[index] = {"Name" + std::to_string(index),
                         static_cast<std::int32_t>(index) - 20};
    }
    scores[0] = {"", std::numeric_limits<std::int32_t>::min()};
    scores[1] = {std::string("A\0B", 3), std::numeric_limits<std::int32_t>::max()};
    scores[44].name.assign(65535, 'x');
    yipe::save_high_scores_atomic(path, scores);
    check(yipe::load_high_scores(path) == scores,
          "native high scores round-trip all groups, signed values, and byte-length names");
    std::vector<std::uint8_t> expected{'Y', '3', 'H', 'S', 1, 0, 0, 0, 0, 0, 0, 0, 0, 0};
    for (const auto& entry : scores) {
        append_u16(expected, static_cast<std::uint16_t>(entry.name.size()));
        expected.insert(expected.end(), entry.name.begin(), entry.name.end());
        append_i32(expected, entry.value);
    }
    write_u32(expected, 6, static_cast<std::uint32_t>(expected.size() - 14));
    write_u32(expected, 10, test_crc32(expected, 14));
    {
        std::ifstream input(path, std::ios::binary);
        const std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                               std::istreambuf_iterator<char>()};
        check(bytes == expected, "high-score version 1 has the documented framing and 45-record payload");
    }
    scores = yipe::load_original_auxiliary_data(
        std::filesystem::path(YIPE_TEST_EVIDENCE_DIR) / "Yipe3-2026-09-15.dat").high_scores;
    yipe::save_high_scores_atomic(path, scores);
    check(yipe::load_high_scores(path) == scores,
          "atomic replacement preserves every imported original auxiliary score entry");
    auto invalid = scores;
    invalid[44].name.assign(65536, 'x');
    bool failed_write = false;
    try {
        yipe::save_high_scores_atomic(path, invalid);
    } catch (const std::runtime_error&) {
        failed_write = true;
    }
    check(failed_write && yipe::load_high_scores(path) == scores
              && !std::filesystem::exists(path.string() + ".tmp"),
          "an oversized score name leaves the previous file intact without a temporary file");
    std::vector<std::uint8_t> valid;
    {
        std::ifstream input(path, std::ios::binary);
        valid.assign(std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>());
    }
    const auto rejects = [&](const std::vector<std::uint8_t>& bytes, const std::string& description) {
        write_bytes(path, bytes);
        bool rejected = false;
        try {
            static_cast<void>(yipe::load_high_scores(path));
        } catch (const std::runtime_error&) {
            rejected = true;
        }
        check(rejected, description);
    };
    auto corrupt = valid;
    corrupt[0] = 'X';
    rejects(corrupt, "high scores reject an incorrect magic");
    for (const std::uint16_t version : {0, 2, 65535}) {
        corrupt = valid;
        corrupt[4] = static_cast<std::uint8_t>(version);
        corrupt[5] = static_cast<std::uint8_t>(version >> 8u);
        rejects(corrupt, "high scores reject unsupported versions");
    }
    corrupt = valid;
    corrupt.back() ^= 1;
    rejects(corrupt, "high scores reject checksum corruption");
    corrupt = valid;
    corrupt[6] ^= 1;
    rejects(corrupt, "high scores reject an incorrect payload size");
    corrupt.resize(13);
    rejects(corrupt, "high scores reject a truncated header");
    corrupt = valid;
    corrupt.pop_back();
    rejects(corrupt, "high scores reject a truncated file");
    write_u32(corrupt, 6, static_cast<std::uint32_t>(corrupt.size() - 14));
    write_u32(corrupt, 10, test_crc32(corrupt, 14));
    rejects(corrupt, "high scores reject an incomplete record even with valid framing");
    corrupt = valid;
    corrupt.push_back(0);
    rejects(corrupt, "high scores reject appended file data");
    write_u32(corrupt, 6, static_cast<std::uint32_t>(corrupt.size() - 14));
    write_u32(corrupt, 10, test_crc32(corrupt, 14));
    rejects(corrupt, "high scores reject trailing payload data even with valid framing");
    corrupt = valid;
    corrupt[14] = 0xff;
    corrupt[15] = 0xff;
    write_u32(corrupt, 10, test_crc32(corrupt, 14));
    rejects(corrupt, "high scores reject a name length exceeding the payload");
    write_bytes(path, valid);
    bool rejected_game = false;
    try {
        static_cast<void>(yipe::load_game(path));
    } catch (const std::runtime_error&) {
        rejected_game = true;
    }
    check(rejected_game, "a high-score file cannot be mistaken for a game save");
    yipe::save_player_atomic(path, yipe::Player{});
    bool rejected_scores = false;
    try {
        static_cast<void>(yipe::load_high_scores(path));
    } catch (const std::runtime_error&) {
        rejected_scores = true;
    }
    check(rejected_scores, "a game save cannot be mistaken for high scores");
    std::filesystem::remove(path);
}

void test_settings_and_audio_catalog() {
    const yipe::GameSettings defaults;
    check(defaults.sound_enabled && defaults.keys == yipe::default_key_bindings(),
          "native settings preserve original sound and key defaults");
    check(yipe::key_binding_name(defaults.keys[0]) == "Numpad 7"
              && yipe::key_binding_name(defaults.keys[8]) == "A"
              && yipe::key_binding_name(defaults.keys[9]) == "R"
              && yipe::input_action_name(yipe::InputAction::use_item) == "Use Item",
          "settings expose recovered movement and combat action names");
    check(yipe::combat_action_for_key(defaults, 'A')
                  == yipe::InputAction::attack
              && yipe::combat_action_for_key(defaults, 'R')
                  == yipe::InputAction::run
              && yipe::combat_action_for_key(defaults, 'C')
                  == yipe::InputAction::cast
              && yipe::combat_action_for_key(defaults, 'I')
                  == yipe::InputAction::use_item
              && yipe::combat_action_for_key(defaults, 'S')
                  == yipe::InputAction::special
              && !yipe::combat_action_for_key(defaults, 0x26),
          "combat key lookup covers all five configurable actions");

    constexpr std::array movement_order{
        yipe::InputAction::north, yipe::InputAction::northeast, yipe::InputAction::northwest,
        yipe::InputAction::south, yipe::InputAction::southwest, yipe::InputAction::southeast,
        yipe::InputAction::west, yipe::InputAction::east,
    };
    yipe::GameSettings remapped;
    for (std::size_t index = 0; index < 8; ++index) {
        remapped.keys[index] = static_cast<std::uint16_t>('0' + index);
        check(yipe::movement_action_for_key(remapped, remapped.keys[index])
                  == static_cast<yipe::InputAction>(index),
              "every configured movement key resolves to its direction");
    }
    constexpr std::array<std::uint16_t, 8> fixed_keypad{0x68, 0x69, 0x67, 0x62, 0x61, 0x63, 0x64, 0x66};
    for (std::size_t index = 0; index < fixed_keypad.size(); ++index) {
        check(yipe::movement_action_for_key(remapped, fixed_keypad[index]) == movement_order[index],
              "original keypad directions survive rebinding");
    }
    for (const auto [key, action] : {
             std::pair{0x25, yipe::InputAction::west}, {0x26, yipe::InputAction::north},
             {0x27, yipe::InputAction::east}, {0x28, yipe::InputAction::south}}) {
        check(yipe::movement_action_for_key(remapped, static_cast<std::uint16_t>(key)) == action,
              "original arrow directions survive rebinding");
    }
    for (std::size_t first = 0; first < movement_order.size(); ++first) {
        for (std::size_t second = first + 1; second < movement_order.size(); ++second) {
            auto duplicate = remapped;
            duplicate.keys[static_cast<std::size_t>(movement_order[first])] = 'X';
            duplicate.keys[static_cast<std::size_t>(movement_order[second])] = 'X';
            check(yipe::movement_action_for_key(duplicate, 'X') == movement_order[first],
                  "movement duplicates follow the recovered branch order");
        }
    }
    remapped.keys[static_cast<std::size_t>(yipe::InputAction::northwest)] = 0x26;
    remapped.keys[static_cast<std::size_t>(yipe::InputAction::north)] = 0x69;
    check(yipe::movement_action_for_key(remapped, 0x26) == yipe::InputAction::north
              && yipe::movement_action_for_key(remapped, 0x69) == yipe::InputAction::north
              && !yipe::movement_action_for_key(remapped, 0),
          "fixed and configured movement keys interleave like the original handler");
    constexpr std::array combat_order{yipe::InputAction::run, yipe::InputAction::attack,
        yipe::InputAction::cast, yipe::InputAction::use_item, yipe::InputAction::special};
    for (std::size_t first = 0; first < combat_order.size(); ++first) {
        for (std::size_t second = first + 1; second < combat_order.size(); ++second) {
            auto duplicate = defaults;
            duplicate.keys[static_cast<std::size_t>(combat_order[first])] = 'X';
            duplicate.keys[static_cast<std::size_t>(combat_order[second])] = 'X';
            check(yipe::combat_action_for_key(duplicate, 'X') == combat_order[first],
                  "combat duplicates follow Run, Attack, Cast, Item, Special priority");
        }
    }
    remapped.keys[static_cast<std::size_t>(yipe::InputAction::cast)] = 0x0d;
    remapped.keys[static_cast<std::size_t>(yipe::InputAction::attack)] = 0x1b;
    check(yipe::combat_action_for_key(remapped, 0x0d) == yipe::InputAction::attack
              && yipe::combat_action_for_key(remapped, 0x1b) == yipe::InputAction::run,
          "Enter and Escape retain their original combat branch precedence");
    remapped.keys[static_cast<std::size_t>(yipe::InputAction::run)] = 0x0d;
    check(yipe::combat_action_for_key(remapped, 0x0d) == yipe::InputAction::run,
          "an existing Run binding precedes the original Enter fallback");

    yipe::GameSettings changed = defaults;
    changed.sound_enabled = false;
    changed.keys[static_cast<std::size_t>(yipe::InputAction::north)] = 0x57;
    changed.keys[static_cast<std::size_t>(yipe::InputAction::attack)] = 0x52;
    changed.keys[static_cast<std::size_t>(yipe::InputAction::cast)] = 0xba;
    changed.keys[static_cast<std::size_t>(yipe::InputAction::use_item)] = 0x6a;
    const std::filesystem::path path =
        std::filesystem::temp_directory_path() / "yipe3-settings-test.y3r";
    yipe::save_settings_atomic(path, changed);
    check(yipe::load_settings(path) == changed,
          "native settings round-trip sound and key bindings");

    std::vector<std::uint8_t> bytes;
    {
        std::ifstream input(path, std::ios::binary);
        bytes.assign(std::istreambuf_iterator<char>(input),
                     std::istreambuf_iterator<char>());
    }
    std::vector<std::uint8_t> corrupted = bytes;
    corrupted.back() ^= 1;
    write_bytes(path, corrupted);
    bool rejected = false;
    try {
        static_cast<void>(yipe::load_settings(path));
    } catch (const std::runtime_error&) {
        rejected = true;
    }
    check(rejected, "native settings reject checksum corruption");

    bytes.push_back(0);
    write_bytes(path, bytes);
    rejected = false;
    try {
        static_cast<void>(yipe::load_settings(path));
    } catch (const std::runtime_error&) {
        rejected = true;
    }
    check(rejected, "native settings reject trailing data");
    std::filesystem::remove(path);

    check(yipe::sound_effects().size() == 37,
          "sound catalog contains every original dispatcher entry");
    check(std::all_of(yipe::sound_effects().begin(), yipe::sound_effects().end(),
                      [](const yipe::SoundEffect& effect) {
                          return yipe::sound_asset_name(effect.id)
                              == effect.asset_name;
                      }),
          "sound IDs resolve to their prepared embedded assets");
    check(!yipe::sound_asset_name(139) && !yipe::sound_asset_name(999),
          "sound catalog rejects IDs absent from the original dispatcher");
    check(yipe::monster_sound_file_id(10) == 8
              && yipe::monster_sound_file_id(11) == 8
              && yipe::monster_sound_file_id(22) == 21
              && yipe::monster_sound_file_id(27) == 25
              && yipe::monster_sound_file_id(39) == 35
              && yipe::monster_sound_file_id(38) == 40
              && yipe::monster_sound_file_id(24) == 24,
          "monster sound catalog preserves shared-file aliases");
    check(yipe::spell_sound_id(0) == 201 && yipe::spell_sound_id(7) == 206
              && yipe::spell_sound_id(9) == 210
              && yipe::spell_sound_id(11) == 209,
          "spell sound IDs preserve recovered effect selection");
    check(yipe::ability_sound_id(yipe::SpecialAbility::backstab) == 130
              && yipe::ability_sound_id(yipe::SpecialAbility::frighten) == 151
              && yipe::ability_sound_id(yipe::SpecialAbility::invisibility) == 40
              && yipe::ability_sound_id(yipe::SpecialAbility::enrage) == 136,
          "ability sound IDs preserve recovered effect selection");
}

void test_starting_map() {
    const yipe::Map map = yipe::starting_map();
    check(map.id() == 0x11 && map.environment() == 0,
          "starting map preserves original map ID and environment");
    check(map.tile({0, 0}) == 3 && map.tile({5, 5}) == 2 && map.tile({6, 6}) == 1,
          "starting map uses the recovered column-major tile table");
    check(map.tile({12, 11}) == 3 && map.tile({11, 10}) == 10,
          "starting map preserves far-edge tiles");

    yipe::MapPosition position{5, 5};
    const yipe::MovementResult right = yipe::try_move(map, position, 1, 0);
    check(right.original_code == 1 && right.position_changed
              && position == yipe::MapPosition{6, 5},
          "ordinary starting-map movement updates the player position");
    const yipe::MovementResult entrance = yipe::try_move(map, position, 0, 1);
    check(entrance.original_code == 8 && !entrance.position_changed
              && position == yipe::MapPosition{6, 5},
          "tile 1 produces the original entrance action without moving");
    position = {1, 0};
    const yipe::MovementResult blocked = yipe::try_move(map, position, -1, 0);
    check(blocked.original_code == 0 && !blocked.position_changed,
          "impassable terrain blocks movement");
}

void test_static_map_catalog_and_edges() {
    check(yipe::original_map(1).has_value() && yipe::original_map(0x85).has_value(),
          "generated catalog exposes the first and last static map IDs");
    check(!yipe::original_map(0x76).has_value() && !yipe::original_map(0x78).has_value(),
          "runtime-generated map IDs are not mislabeled as static maps");

    int messages = 0;
    int overlays = 0;
    int depth_events = 0;
    int linked_first = 0;
    int linked_second = 0;
    int princesses = 0;
    int pursuing_monsters = 0;
    for (int map_id = 1; map_id <= 0x85; ++map_id) {
        const std::optional<yipe::Map> catalog_map = yipe::original_map(map_id);
        if (!catalog_map) {
            continue;
        }
        messages += catalog_map->events().message.has_position() ? 1 : 0;
        overlays += catalog_map->events().overlay.has_position() ? 1 : 0;
        depth_events += static_cast<int>(std::count_if(
            catalog_map->events().depth.begin(), catalog_map->events().depth.end(),
            [](const yipe::MapEvent& event) { return event.has_position(); }));
        linked_first += catalog_map->events().linked[0].has_position() ? 1 : 0;
        linked_second += catalog_map->events().linked[1].has_position() ? 1 : 0;
        princesses += catalog_map->events().princess.has_position() ? 1 : 0;
        pursuing_monsters +=
            catalog_map->events().pursuer.has_position() ? 1 : 0;
    }
    check(messages == 20 && overlays == 10 && depth_events == 26
              && linked_first == 8 && linked_second == 3 && princesses == 20
              && pursuing_monsters == 22,
          "generated maps preserve all statically configured movement-event positions");
    const yipe::PrincessEvent first_princess = yipe::original_map(0x2e)->events().princess;
    const yipe::PrincessEvent last_princess = yipe::original_map(0x85)->events().princess;
    check(first_princess.position == yipe::MapPosition{7, 1}
              && first_princess.portrait_variant == 1
              && first_princess.rescue_index == 13
              && last_princess.position == yipe::MapPosition{1, 9}
              && last_princess.portrait_variant == 2
              && last_princess.rescue_index == 20,
          "princess events preserve the recovered endpoint records");
    const yipe::PursuingMonsterEvent fixed_pursuer =
        yipe::original_map(0x0d)->events().pursuer;
    const yipe::PursuingMonsterEvent scaled_pursuer =
        yipe::original_map(0x10)->events().pursuer;
    check(fixed_pursuer.position == yipe::MapPosition{7, 6}
              && fixed_pursuer.type_for_level(1) == 19
              && scaled_pursuer.position == yipe::MapPosition{5, 4}
              && scaled_pursuer.type_for_level(5) == 1
              && scaled_pursuer.type_for_level(6) == 6,
          "map-authored pursuers preserve fixed and level-dependent records");

    yipe::WorldLocation location;
    location.position = {12, 5};
    check(yipe::cross_map_edge(location) && location.map_id == 0x12
              && location.region_x == 1 && location.position == yipe::MapPosition{1, 5},
          "east edge wraps into map 0x12");
    location.position = {0, 5};
    check(yipe::cross_map_edge(location) && location.map_id == 0x11
              && location.region_x == 0 && location.position == yipe::MapPosition{11, 5},
          "west edge returns to the starting map");

    location = {};
    location.position = {5, 0};
    check(yipe::cross_map_edge(location) && location.map_id == 0x0b
              && location.position == yipe::MapPosition{5, 10},
          "north edge uses the original negative-six map stride");
    location = {};
    location.position = {5, 11};
    check(yipe::cross_map_edge(location) && location.map_id == 0x17
              && location.position == yipe::MapPosition{5, 1},
          "south edge uses the original positive-six map stride");

    location = {};
    check(yipe::change_map_depth(location, 0x0e) && location.map_layer == 1
              && location.map_id == 0x39 && location.position == yipe::MapPosition{5, 5},
          "down entrance advances to the corresponding map on layer one");
    check(yipe::change_map_depth(location, 0x0f) && location.map_layer == 0
              && location.map_id == 0x11,
          "up entrance returns to the corresponding surface map");
    location.map_layer = 2;
    check(yipe::change_map_depth(location, 0x10) && location.map_layer == 0
              && location.map_id == 0x11
              && location.position == yipe::MapPosition{4, 5},
          "leaving the third layer uses the original surface-return location");
}

void test_runtime_generated_maps() {
    const auto summarize = [](const yipe::Map& map) {
        std::uint32_t hash = 2166136261u;
        int paths = 0;
        for (int x = 0; x < yipe::Map::width; ++x) {
            for (int y = 0; y < yipe::Map::height; ++y) {
                const std::uint16_t tile = map.tile({x, y});
                paths += tile == 0x27 ? 1 : 0;
                hash ^= tile;
                hash *= 16777619u;
            }
        }
        return std::pair{paths, hash};
    };
    const auto reachable_paths = [](const yipe::Map& map) {
        std::array<bool, yipe::Map::tile_count> visited{};
        std::array<yipe::MapPosition, yipe::Map::tile_count> pending{};
        std::size_t read = 0;
        std::size_t write = 1;
        pending[0] = {0, 1};
        visited[1] = true;
        while (read < write) {
            const yipe::MapPosition position = pending[read++];
            constexpr std::array<yipe::MapPosition, 4> neighbors{{
                {-1, 0}, {1, 0}, {0, -1}, {0, 1},
            }};
            for (const yipe::MapPosition offset : neighbors) {
                const yipe::MapPosition next{
                    position.x + offset.x, position.y + offset.y};
                if (!map.contains(next) || map.tile(next) != 0x27) {
                    continue;
                }
                const std::size_t index = static_cast<std::size_t>(
                    next.x * yipe::Map::height + next.y);
                if (!visited[index]) {
                    visited[index] = true;
                    pending[write++] = next;
                }
            }
        }
        return static_cast<int>(write);
    };

    yipe::MsvcRandom default_entry_random(1);
    const yipe::Map default_entry = yipe::generate_original_map(
        0x76, {1, 1}, default_entry_random);
    const auto [default_paths, default_hash] = summarize(default_entry);
    yipe::MsvcRandom alternate_entry_random(1);
    const yipe::Map alternate_entry = yipe::generate_original_map(
        0x78, {11, 10}, alternate_entry_random);
    const auto [alternate_paths, alternate_hash] = summarize(alternate_entry);
    check(default_entry.id() == 0x76 && default_entry.environment() == 7
              && default_entry.tile({0, 1}) == 0x27
              && default_entry.tile({12, 1}) == 0x27
              && default_entry.tile({11, 1}) == 0x27,
          "runtime map generator preserves its environment, exits, and encounter tile");
    check(default_paths == 47 && default_hash == 863378094u
              && default_entry_random.state() == 1158257644u,
          "seed 1 generated map preserves the recovered tile and random-call order");
    const int default_reachable = reachable_paths(default_entry);
    check(default_reachable == 44 && default_entry.tile({11, 2}) == 0x24,
          "generated-map boundary cleanup preserves the original seeded corridor cutoff");
    check(alternate_entry.id() == 0x78 && alternate_entry.tile({11, 10}) == 0x27
              && alternate_paths == 57 && alternate_hash == 1251544680u
              && alternate_entry_random.state() == 3999086192u,
          "a nondefault entry receives its recovered extra corridor and random calls");
    check(reachable_paths(alternate_entry) == alternate_paths,
          "the alternate entry connects to every generated corridor");

    yipe::MsvcRandom clamped_room_random(2566201570u);
    const auto clamped_room = yipe::generate_original_map(0x78, {1, 1}, clamped_room_random);
    const auto [clamped_paths, clamped_hash] = summarize(clamped_room);
    check(clamped_paths == 50 && clamped_hash == 1763368075u
              && clamped_room_random.state() == 1822273529u,
          "late maze clamps room minimum Y to 9, matching the original executable grid and RNG");
}

void test_other_action() {
    const auto make_map = [](std::uint16_t tile, int id = 0x11) {
        yipe::Map::Tiles tiles;
        tiles.fill(tile);
        return yipe::Map(id, 0, tiles);
    };
    constexpr yipe::MapPosition position{5, 5};
    yipe::Player player;
    check(player.bridge_treasure_available && player.healing_water_available
              && !player.cave_coins_collected && player.snow_search_count == 0,
          "Other action defaults match the original player initializer");
    yipe::MsvcRandom random(1);
    yipe::Map bridge = make_map(0x0b, 0x21);
    const int initial_gold = player.gold;
    const int initial_experience = player.experience;
    check(yipe::perform_other_action(player, bridge, position, random).message_resource_id == 1007
              && player.gold == initial_gold + 600
              && player.experience == initial_experience + 21
              && !player.bridge_treasure_available,
          "the hidden bridge treasure awards 600 gold and 20-29 experience once");
    const auto after_bridge = random.state();
    check(yipe::perform_other_action(player, bridge, position, random).message_resource_id == 1004
              && player.gold == initial_gold + 600 && random.state() == after_bridge,
          "rechecking the bridge neither grants treasure nor consumes a random roll");
    player.bridge_treasure_available = true;
    yipe::Map ordinary_bridge = make_map(0x0b);
    check(yipe::perform_other_action(player, ordinary_bridge, position, random).message_resource_id == 1004
              && player.bridge_treasure_available && random.state() == after_bridge,
          "only map 0x21 contains bridge treasure");

    yipe::Map water = make_map(0x26, 6);
    player.health = 20;
    player.maximum_health = 100;
    player.level = 5;
    player.intelligence = 13;
    random = yipe::MsvcRandom(1);
    const yipe::OtherActionResult drink = yipe::perform_other_action(player, water, position, random);
    check(drink.message_resource_id == 1008 && drink.sound_id == 143
              && player.health == 32 && !player.healing_water_available,
          "healing water rolls dryness before level-based healing");
    const auto after_drink = random.state();
    check(yipe::perform_other_action(player, water, position, random).message_resource_id == 1009
              && random.state() == after_drink && player.health == 32,
          "a dry water hole cannot heal or consume random values");
    yipe::initialize_map_actions(player, water, position);
    player.has_bonus_healing_ring = true;
    random = yipe::MsvcRandom(1);
    static_cast<void>(yipe::perform_other_action(player, water, position, random));
    check(player.health == 57, "the ring adds intelligence to Other-action healing");
    player.health = 95;
    player.healing_water_available = true;
    player.difficulty = 1;
    random = yipe::MsvcRandom(1);
    static_cast<void>(yipe::perform_other_action(player, water, position, random));
    check(player.health == 100 && player.healing_water_available,
          "healing caps at maximum HP and the dryness threshold is difficulty plus 40");
    const auto full_health_random = random.state();
    check(yipe::perform_other_action(player, water, position, random).message_resource_id == 1009
              && player.healing_water_available && random.state() == full_health_random,
          "the original full-health water message is dry without actually drying the hole");
    yipe::Map ordinary_water = make_map(0x26);
    player.health = 20;
    check(yipe::perform_other_action(player, ordinary_water, position, random).message_resource_id == 1003
              && player.health == 20 && random.state() == full_health_random,
          "water outside map 6 is refreshing but does not heal");

    yipe::Map coins = make_map(0x47, 0x45);
    const int before_coins = player.gold;
    check(yipe::perform_other_action(player, coins, position, random).message_resource_id == 146
              && player.cave_coins_collected && player.gold == before_coins + 180,
          "the cave coin pile grants exactly 180 gold");
    check(yipe::perform_other_action(player, coins, position, random).message_resource_id == 147
              && player.gold == before_coins + 180,
          "the cave coin pile stays empty on later searches");
    yipe::Map flowers = make_map(0x35, 0x12);
    const yipe::OtherActionResult flower_action =
        yipe::perform_other_action(player, flowers, position, random);
    check(flower_action.message_resource_id == 1023 && flower_action.sound_id == 152
              && flowers.tile({3, 7}) == 0x46 && flowers.tile({4, 7}) == 0x46,
          "the special flowers lower both recovered mountain tiles");

    yipe::Map snow = make_map(0x69);
    random = yipe::MsvcRandom(1);
    const int before_snow = player.gold;
    check(yipe::perform_other_action(player, snow, position, random).message_resource_id == 1048
              && player.gold == before_snow + 402 && player.snow_search_count == 1,
          "the first snow search grants 401-410 gold, not the rounded resource text amount");
    check(yipe::perform_other_action(player, snow, position, random).message_resource_id == 1049
              && player.gold == before_snow + 510 && player.snow_search_count == 2,
          "the second snow search grants 101-110 gold");
    const auto after_snow_rewards = random.state();
    check(yipe::perform_other_action(player, snow, position, random).message_resource_id == 1050
              && player.snow_search_count == 3 && random.state() == after_snow_rewards,
          "the third snow search warns without consuming a random roll");
    player.health = 3;
    const yipe::OtherActionResult frostbite =
        yipe::perform_other_action(player, snow, position, random);
    check(frostbite.message_resource_id == 1051 && frostbite.sound_id == 144
              && player.health == 1 && player.snow_search_count == 2,
          "frostbite is nonlethal and resets the snow state to the warning stage");
    static_cast<void>(yipe::perform_other_action(player, snow, position, random));
    static_cast<void>(yipe::perform_other_action(player, snow, position, random));
    check(player.gold == before_snow + 510 && player.health == 1,
          "repeated snow searches alternate warning and frostbite, never more gold");
    player.health = 50;
    player.snow_search_count = 3;
    random = yipe::MsvcRandom(1);
    static_cast<void>(yipe::perform_other_action(player, snow, position, random));
    check(player.health == 34 && player.snow_search_count == 2,
          "frostbite applies the recovered 5-19 damage before the one-HP floor");

    constexpr std::array<std::pair<std::uint16_t, int>, 24> fixed_messages{{
        {3, 1044}, {8, 1018}, {0x16, 1018}, {0x17, 1018}, {0x18, 1018},
        {0x19, 1018}, {0x1a, 1018}, {0x1b, 1036}, {0x20, 1047},
        {0x29, 1017}, {0x2a, 1017}, {0x2c, 1013}, {0x2f, 1015},
        {0x35, 1012}, {0x36, 1053}, {0x39, 1053}, {0x3c, 1016},
        {0x43, 1005}, {0x44, 1005}, {0x45, 1040}, {0x46, 1002},
        {0x47, 1055}, {0x49, 1055}, {0x65, 1035},
    }};
    for (const auto& [tile, expected] : fixed_messages) {
        yipe::Map map = make_map(tile);
        const auto before = random.state();
        const auto result = yipe::perform_other_action(player, map, position, random);
        check(result.message_resource_id == expected && !result.message.empty()
                  && result.sound_id == 0 && before == random.state(),
              "fixed Other messages preserve resource IDs and consume no random values");
    }
    yipe::Map tally = make_map(0x49, 0x57);
    check(yipe::perform_other_action(player, tally, position, random).message_resource_id == 1054,
          "map 0x57 exposes the tally-mark clue");
    yipe::Map lowered = make_map(0x46, 0x12);
    check(yipe::perform_other_action(player, lowered, position, random).message_resource_id == 1043,
          "lowered mountain tiles retain their area-specific description");

    for (std::uint32_t seed = 1; seed <= 300; ++seed) {
        constexpr std::array<std::array<int, 4>, 6> random_messages{{
            {0x1f, 71, 1046, 1006}, {0x27, 21, 1042, 1041},
            {0x28, 26, 1045, 1041}, {0x32, 61, 1037, 1014},
            {0x33, 41, 1038, 1010}, {0x53, 31, 1052, 1041},
        }};
        for (const auto& branch : random_messages) {
            yipe::Map map = make_map(static_cast<std::uint16_t>(branch[0]));
            yipe::MsvcRandom expected(seed);
            const int message = expected.next() % 100 < branch[1] ? branch[2] : branch[3];
            random = yipe::MsvcRandom(seed);
            check(yipe::perform_other_action(player, map, position, random).message_resource_id == message
                      && random.state() == expected.state(),
                  "random terrain descriptions preserve threshold and single-roll order");
        }
        for (const std::uint16_t tile : {2, 0x48, 0x56, 0x57, 0x2d, 8, 0x34}) {
            yipe::Map map = make_map(tile, 1);
            player.health = 10;
            player.gatekeeper_state = seed % 4;
            yipe::MsvcRandom expected(seed);
            const int roll = expected.next();
            int message = 1002;
            if (tile == 0x56 || tile == 0x57) {
                if (roll % 100 >= 76) {
                    message = player.gatekeeper_state < 3 ? 1032 : 1034;
                } else {
                    const int second = expected.next() % 100;
                    if (second >= 61 && tile == 0x57) {
                        message = 1033;
                    }
                }
            } else if (tile == 0x2d) {
                message = roll % 4 == 0 ? 1002 : 1024 + roll % 4;
            } else if (tile == 8) {
                message = roll % 50 < 29 ? 1018 : roll % 50 < 46 ? 1030 : 1031;
            } else if (tile == 0x34) {
                message = roll % 100 > 79 ? 1024 : 1011;
            } else if (tile == 2 && roll % 100 >= 86) {
                message = 1039;
            } else if (roll % 100 < 10) {
                message = 1028;
            }
            random = yipe::MsvcRandom(seed);
            const auto result = yipe::perform_other_action(player, map, position, random);
            check(result.message_resource_id == message && random.state() == expected.state(),
                  "Other preserves road, sand, grass, bee, and both tree random branches");
            check(player.health == (message == 1024 ? 8 : 10)
                      && result.sound_id == (message == 1024 ? 50 : 0),
                  "only the bee branch damages health and plays its sound");
        }
    }
    yipe::Map bee = make_map(0x34);
    player.health = 3;
    const auto before_bee = random.state();
    check(yipe::perform_other_action(player, bee, position, random).message_resource_id == 1011
              && player.health == 3 && random.state() == before_bee,
          "low-health players bypass both bee damage and its random roll");

    yipe::Map tunnel = make_map(0x42);
    yipe::WorldLocation location;
    const int before_hell = player.health;
    const auto tunnel_result = yipe::perform_other_action(player, tunnel, position, random);
    check(tunnel_result.message.empty() && player.pending_relocation == 3
              && yipe::apply_pending_relocation(player, location) == yipe::PendingRelocation::hell
              && location.map_id == 0x75 && location.position == yipe::MapPosition{6, 10}
              && location.region_x == -4 && location.region_y == -1
              && location.map_layer == 0 && location.alternate_map_set
              && player.current_map_id == 0x75 && player.alternate_map_set
              && player.pending_relocation == 0 && player.entered_hell_through_secret_passage
              && player.hell_gatekeeper_state == yipe::HellGatekeeperState::waiting
              && player.health == before_hell && player.death_count == 0,
          "Other defers secret Hell entry without counting a death or changing HP");
    player.hell_gatekeeper_state = yipe::HellGatekeeperState::defeated;
    static_cast<void>(yipe::perform_other_action(player, tunnel, position, random));
    static_cast<void>(yipe::apply_pending_relocation(player, location));
    check(player.hell_gatekeeper_state == yipe::HellGatekeeperState::defeated,
          "secret entry cannot revive a defeated Hell Gatekeeper");

    yipe::Map stones = *yipe::original_map(3);
    const auto original_stone_tile = stones.tile({4, 7});
    player.stamps_found = 4;
    yipe::initialize_map_actions(player, stones, {4, 7});
    check(stones.tile({4, 7}) == original_stone_tile && original_stone_tile != 0x46,
          "the stone-circle action tile remains unavailable before five stamps");
    player.stamps_found = 5;
    yipe::initialize_map_actions(player, stones, {4, 7});
    check(stones.tile({4, 7}) == 0x46, "five stamps activate the stone circle on map entry");
    static_cast<void>(yipe::perform_other_action(player, stones, {4, 7}, random));
    check(player.pending_relocation == 4
              && yipe::apply_pending_relocation(player, location) == yipe::PendingRelocation::stone_circle
              && location.map_id == 0x15 && location.position == yipe::MapPosition{6, 8}
              && location.region_x == -2 && location.region_y == -1
              && location.map_layer == 0 && !location.alternate_map_set
              && player.current_map_id == 0x15 && !player.alternate_map_set
              && player.pending_relocation == 0,
          "the stone circle reaches the recovered surface destination and synchronizes player state");
}

void test_map_entry_and_hints() {
    yipe::Player player;
    const std::array<std::pair<int, std::string_view>, 11> hints{{
        {3, "The three stones surrounding you start to glow."},
        {0x12, "You notice something strange about the flowers"},
        {0x2c, "You sense that this space seems different."},
        {0x2d, "You notice that the wall next to you could be pushed open."},
        {0x39, "You notice a crack in the wall to the north of you."},
        {0x45, "You find a small pile of coins on the floor."},
        {0x48, "Suddenly you sense that you are not yet strong or powerful enough"},
        {0x4a, "You hear the faint cry of a young female."},
        {0x57, "You notice six tally marks etched into the strangely shaped wall"},
        {0x5a, "A message on the wall says:  A group of stones holds the entrance."},
        {0x6d, "You feel very small standing in front of this giant monster."},
    }};
    for (const auto& [map_id, text] : hints) {
        check(yipe::map_movement_hint(player, map_id).starts_with(text),
              "map " + std::to_string(map_id) + " selects its recovered hint");
    }
    check(yipe::map_movement_hint(player, 0x11).empty()
              && yipe::map_movement_hint(player, -1).empty(),
          "unspecified maps have no movement hint");
    player.cave_coins_collected = true;
    player.level = 10;
    player.inventory_values[10] = 1;
    player.encountered_monster_types[35] = true;
    check(yipe::map_movement_hint(player, 0x45).empty()
              && !yipe::map_movement_hint(player, 0x48).empty()
              && !yipe::map_movement_hint(player, 0x4a).empty()
              && !yipe::map_movement_hint(player, 0x6d).empty(),
          "coins suppress their hint, but level ten, other rescues, and encounters do not");
    player.level = 11;
    player.inventory_values[11] = 1;
    player.has_defeated_wormoid = true;
    check(yipe::map_movement_hint(player, 0x48).empty()
              && yipe::map_movement_hint(player, 0x4a).empty()
              && yipe::map_movement_hint(player, 0x6d).empty(),
          "level eleven, rescue slot eleven, and a Wormoid defeat suppress their hints");

    yipe::Map passage = *yipe::original_map(0x2d);
    player.strength = 39;
    yipe::initialize_map_actions(player, passage, {11, 9});
    check(passage.tile({11, 9}) == 0x49,
          "strength 39 leaves the passage hint tile unchanged");
    player.strength = 40;
    yipe::initialize_map_actions(player, passage, {11, 9});
    check(passage.tile({11, 9}) == 0x5f
              && !yipe::map_movement_hint(player, 0x2d).empty(),
          "strength 40 changes the movement action, not the unconditional hint selector");
    yipe::MapPosition position{10, 9};
    check(yipe::try_move(passage, position, 1, 0).original_code == 0x1a,
          "the strength-gated tile dispatches the linked-wall action");
    check(passage.toggle_linked_tiles() && passage.tile({12, 9}) == 0x53,
          "the recovered linked action opens the east boundary");
    passage = *yipe::original_map(0x2d);
    player.strength = 39;
    yipe::initialize_map_actions(player, passage, {11, 9});
    check(passage.tile({11, 9}) == 0x49 && passage.tile({12, 9}) == 0x25,
          "entry patches are recomputed from the original terrain on reload");

    yipe::Map hell = *yipe::original_map(0x75);
    player.hell_gatekeeper_state = yipe::HellGatekeeperState::defeated;
    yipe::initialize_map_actions(player, hell, {6, 10});
    check(hell.tile({12, 1}) == 0x24,
          "defeating the local Hell Gatekeeper does not unlock the quest boundary");
    for (std::uint16_t state : {1, 2}) {
        player.gatekeeper_state = state;
        hell = *yipe::original_map(0x75);
        yipe::initialize_map_actions(player, hell, {6, 10});
        check(hell.tile({12, 1}) == 0x27,
              "either progressed quest Gatekeeper state opens Hell's east boundary");
    }
    player.gatekeeper_state = 0xffff;
    hell = *yipe::original_map(0x75);
    yipe::initialize_map_actions(player, hell, {6, 10});
    yipe::Map stones = *yipe::original_map(3);
    player.stamps_found = 0xffff;
    yipe::initialize_map_actions(player, stones, {4, 7});
    check(hell.tile({12, 1}) == 0x24 && stones.tile({4, 7}) != 0x46,
          "entry gates preserve the original signed-short comparisons");

    yipe::Map arrival = *yipe::original_map(0x2c);
    yipe::initialize_map_actions(player, arrival, {0, 5});
    check(arrival.tile({0, 5}) == 0x53 && arrival.tile({0, 4}) == 0x25
              && arrival.tile({0, 6}) == 0x25,
          "only the occupied wall tile becomes floor on entry");
    player.healing_water_available = false;
    yipe::Map::Tiles tiles;
    tiles.fill(0x25);
    yipe::Map map_zero(0, 0, tiles);
    yipe::initialize_map_actions(player, map_zero, {2, 3});
    check(map_zero.tile({2, 3}) == 0x53 && !player.healing_water_available,
          "map zero corrects the arrival tile without refilling water");
    bool rejected = false;
    try {
        yipe::initialize_map_actions(player, map_zero, {-1, 3});
    } catch (const std::out_of_range&) {
        rejected = true;
    }
    check(rejected, "invalid map entry coordinates are reported");
}

void test_movement_hint_defeat_state() {
    const auto fresh_encounter = [] {
        yipe::CombatEncounter encounter;
        encounter.monsters[0] = {35, "Wormoid", 175, 1, 1, 0, 1, 100, 0, false};
        encounter.monsters[1] = {2, "Giant Beetle", 175, 100, 100, 0, 1, 1, 0, false};
        return encounter;
    };
    for (int action = 0; action < 4; ++action) {
        yipe::Player player;
        player.health = 1000;
        player.maximum_health = 1000;
        player.weapon_rank = 42;
        player.strength = 100;
        player.special_ability = yipe::SpecialAbility::backstab;
        player.spell_points = 100;
        player.maximum_spell_points = 100;
        player.spells[2] = 1;
        yipe::CombatEncounter encounter = fresh_encounter();
        yipe::MsvcRandom random(1);
        if (action == 0) {
            static_cast<void>(yipe::resolve_attack_turn(player, encounter, random));
        } else if (action == 1) {
            static_cast<void>(yipe::resolve_special_ability_turn(player, encounter, random));
        } else {
            static_cast<void>(yipe::resolve_spell_turn(
                player, encounter, 2,
                action == 2 ? yipe::SpellPower::low : yipe::SpellPower::high, random));
        }
        check(encounter.monsters[0].health < 1 && encounter.monsters[1].health > 0
                  && player.has_defeated_wormoid
                  && player.monster_defeats[34] == 1 && player.quest_progress_total == 1
                  && yipe::map_movement_hint(player, 0x6d).empty(),
              "attack, Backstab, and both spell powers record a Wormoid kill before group victory");
    }
    yipe::Player player;
    player.special_ability = yipe::SpecialAbility::frighten;
    yipe::CombatEncounter encounter = fresh_encounter();
    encounter.monsters[2] = encounter.monsters[0];
    encounter.monsters[0] = encounter.monsters[1];
    yipe::MsvcRandom random(1);
    static_cast<void>(yipe::resolve_special_ability_turn(player, encounter, random));
    check(encounter.monsters[2].health == 0 && !player.has_defeated_wormoid
              && player.quest_progress_total == 0,
          "frightening away a Wormoid does not count as defeating one");
    encounter = fresh_encounter();
    encounter.monsters[0].type_id = 40;
    player.special_ability = yipe::SpecialAbility::backstab;
    player.ability_points = 5;
    static_cast<void>(yipe::resolve_special_ability_turn(player, encounter, random));
    check(encounter.monsters[0].health < 1 && !player.has_defeated_wormoid,
          "an Earth Giant kill is not substituted for the original Wormoid condition");
    encounter = fresh_encounter();
    encounter.monsters[1].attack_rating = 10000;
    player.health = 1;
    player.weapon_rank = 42;
    player.strength = 100;
    random = yipe::MsvcRandom(1);
    const auto defeat = yipe::resolve_attack_turn(player, encounter, random);
    check(defeat.outcome == yipe::CombatOutcome::defeat
              && encounter.monsters[0].health < 1 && !player.has_defeated_wormoid
              && player.quest_progress_total == 0,
          "a fatal ordinary counterattack prevents recording the pending Wormoid kill");
}

void test_monster_defeat_statistics() {
    yipe::Player counts;
    check(counts.monster_defeat_history_complete && counts.quest_progress_total == 0
              && counts.monster_defeats == std::array<std::int32_t, 50>{},
          "new players start with complete zero defeat statistics");
    for (int type = 1; type <= 50; ++type) {
        counts.record_monster_defeat(type);
        check(counts.monster_defeats[static_cast<std::size_t>(type - 1)] == 1,
              "defeat counts use type minus one for type " + std::to_string(type));
    }
    check(counts.quest_progress_total == 50 && counts.has_defeated_wormoid,
          "every recorded defeat advances the aggregate used by the King");
    for (int invalid : {0, -1, 51}) {
        bool rejected = false;
        try {
            counts.record_monster_defeat(invalid);
        } catch (const std::out_of_range&) {
            rejected = true;
        }
        check(rejected && counts.quest_progress_total == 50,
              "invalid monster types cannot mutate defeat statistics");
    }
    counts.monster_defeats[34] = std::numeric_limits<std::int32_t>::max();
    counts.quest_progress_total = std::numeric_limits<std::int32_t>::max();
    counts.record_monster_defeat(35);
    check(counts.monster_defeats[34] == std::numeric_limits<std::int32_t>::min()
              && counts.quest_progress_total == std::numeric_limits<std::int32_t>::min()
              && counts.has_defeated_wormoid,
          "defeat increments preserve the original signed 32-bit wrap");
    counts.monster_defeats[34] = -1;
    counts.record_monster_defeat(35);
    check(counts.monster_defeats[34] == 0 && !counts.has_defeated_wormoid,
          "the Wormoid hint continues to reflect the counter's zero/nonzero predicate");

    const auto make_encounter = [](int primary) {
        yipe::CombatEncounter encounter;
        encounter.monsters[0] = {primary, "Primary", 175, 1, 1, 0, 1, 1, 0, false};
        encounter.monsters[1] = {2, "Giant Beetle", 175, 1, 1, 0, 1, 1, 0, false};
        encounter.monsters[2] = {35, "Wormoid", 208, 1, 1, 0, 1, 1, 0, false};
        return encounter;
    };
    for (int boss : {40, 41}) {
        for (bool frighten_last : {false, true}) {
            yipe::Player player;
            player.special_ability = yipe::SpecialAbility::backstab;
            player.ability_points = 20;
            player.gatekeeper_state = boss == 41 ? 1 : 0;
            player.has_underworld_key = false;
            yipe::CombatEncounter encounter = make_encounter(boss);
            encounter.map_environment = 6;
            yipe::MsvcRandom random(1);
            static_cast<void>(yipe::resolve_special_ability_turn(player, encounter, random));
            check(encounter.monsters[0].health < 1 && player.quest_progress_total == 0
                      && player.monster_defeats[static_cast<std::size_t>(boss - 1)] == 0,
                  "Earth Giant and Gatekeeper counts wait for the whole group");
            static_cast<void>(yipe::resolve_special_ability_turn(player, encounter, random));
            check(player.monster_defeats[1] == 1 && player.quest_progress_total == 1,
                  "ordinary companion kills are counted immediately");
            if (frighten_last) {
                player.special_ability = yipe::SpecialAbility::frighten;
            }
            const auto victory = yipe::resolve_special_ability_turn(player, encounter, random);
            check(victory.outcome == yipe::CombatOutcome::victory
                      && player.monster_defeats[static_cast<std::size_t>(boss - 1)] == 1
                      && player.monster_defeats[34] == (frighten_last ? 0 : 1)
                      && player.quest_progress_total == (frighten_last ? 2 : 3)
                      && player.gatekeeper_state == (boss == 40 ? 1 : 2)
                      && player.has_underworld_key == (boss == 41)
                      && victory.reward.underworld_key == (boss == 41)
                      && player.stamps_found == (boss == 40 ? 1 : 0),
                  "both final-kill and Frighten victories count the primary boss once and advance quests");
        }
    }
    yipe::Player caster;
    caster.spell_points = 100;
    caster.spells[2] = 1;
    yipe::CombatEncounter group = make_encounter(2);
    group.monsters[2] = group.monsters[1];
    yipe::MsvcRandom magic_random(1);
    const auto spell = yipe::resolve_spell_turn(caster, group, 2,
                                               yipe::SpellPower::high, magic_random);
    check(spell.turn.outcome == yipe::CombatOutcome::victory
              && caster.monster_defeats[1] == 3 && caster.quest_progress_total == 3,
          "a group-damage spell records each defeated target exactly once");
    bool rejected_second_victory = false;
    try {
        static_cast<void>(yipe::resolve_attack_turn(caster, group, magic_random));
    } catch (const std::logic_error&) {
        rejected_second_victory = true;
    }
    check(rejected_second_victory && caster.monster_defeats[1] == 3,
          "an ended encounter cannot record its kills a second time");
    for (int boss : {40, 41}) {
        caster = yipe::Player{};
        caster.spell_points = 100;
        caster.spells[2] = 1;
        group = make_encounter(boss);
        magic_random = yipe::MsvcRandom(1);
        const auto boss_spell = yipe::resolve_spell_turn(
            caster, group, 2, yipe::SpellPower::high, magic_random);
        check(boss_spell.turn.outcome == yipe::CombatOutcome::victory
                  && caster.monster_defeats[static_cast<std::size_t>(boss - 1)] == 1
                  && caster.monster_defeats[1] == 1 && caster.monster_defeats[34] == 1
                  && caster.quest_progress_total == 3,
              "a group spell counts a deferred boss once alongside its ordinary companions");
    }
    yipe::Player companion_player;
    companion_player.special_ability = yipe::SpecialAbility::backstab;
    companion_player.ability_points = 20;
    group = make_encounter(2);
    group.monsters[1].type_id = 40;
    group.monsters[2].type_id = 41;
    for (int turn = 0; turn < 3; ++turn) {
        static_cast<void>(yipe::resolve_special_ability_turn(companion_player, group, magic_random));
    }
    check(companion_player.monster_defeats[39] == 0
              && companion_player.monster_defeats[40] == 0
              && companion_player.quest_progress_total == 1,
          "special types 40 and 41 count only when they are the primary boss");

    yipe::Player survivor;
    survivor.special_ability = yipe::SpecialAbility::backstab;
    yipe::CombatEncounter partial = make_encounter(2);
    yipe::MsvcRandom random(1);
    static_cast<void>(yipe::resolve_special_ability_turn(survivor, partial, random));
    const auto fled = yipe::try_flee(survivor, partial, random);
    check(fled.escaped && survivor.monster_defeats[1] == 1
              && survivor.quest_progress_total == 1,
          "fleeing keeps processed kills without counting surviving opponents");

    yipe::Player king_player;
    king_player.quest_flags.fill(true);
    king_player.stamps_found = 1;
    for (int kill = 0; kill < 50; ++kill) {
        king_player.record_monster_defeat(2);
    }
    yipe::MsvcRandom king_random(5);
    check(yipe::talk_to_king(king_player, king_random, 12).message_resource_id != 756,
          "fifty defeats do not cross the King's equipment-hint threshold");
    king_player.record_monster_defeat(2);
    king_random = yipe::MsvcRandom(5);
    check(yipe::talk_to_king(king_player, king_random, 12).message_resource_id == 756,
          "new gameplay defeats now unlock the King's hint above fifty kills");
}

void test_special_movement_actions() {
    yipe::Player player;
    player.health = 1;
    player.spell_points = 0;
    player.ability_points = 0;
    check(yipe::use_restoration_tile(player) && player.health == player.maximum_health
              && player.spell_points == player.maximum_spell_points
              && player.ability_points == player.maximum_ability_points
              && player.restoration_uses == 1,
          "restoration tile fully restores all three resource pools");
    player.restoration_uses = 7 - player.difficulty;
    player.health = 1;
    check(!yipe::use_restoration_tile(player) && player.health == 1,
          "restoration tile enforces its difficulty-derived use limit");

    player.level = 10;
    player.health = 5;
    yipe::MsvcRandom random(1);
    check(yipe::apply_fire_wall(player, random) == 10 && player.health == 1,
          "fire-wall damage uses two original random rolls and cannot kill the player");

    std::optional<yipe::Map> event_map = yipe::original_map(0x2b);
    check(event_map.has_value(), "movement-event fixture map is available");
    if (event_map) {
        yipe::MapPosition overlay_position{2, 3};
        const yipe::MovementResult overlay =
            yipe::try_move(*event_map, overlay_position, 0, -1);
        check(overlay.original_code == 0x18 && overlay.position_changed
                  && overlay_position == yipe::MapPosition{2, 2},
              "overlay event substitutes action 0x18 for its underlying blocked tile");

        yipe::MapPosition depth_position{6, 6};
        const yipe::MovementResult depth =
            yipe::try_move(*event_map, depth_position, 0, -1);
        check(depth.original_code == 0x10 && depth.position_changed
                  && depth_position == yipe::MapPosition{6, 5},
              "configured depth event overrides an ordinary movement result");

        check(event_map->tile({6, 1}) == 34 && event_map->tile({5, 8}) == 40
                  && event_map->toggle_linked_tiles()
                  && event_map->tile({6, 1}) == 40 && event_map->tile({5, 8}) == 34
                  && event_map->toggle_linked_tiles()
                  && event_map->tile({6, 1}) == 34 && event_map->tile({5, 8}) == 40,
              "linked-tile action swaps and restores both configured tiles");

        event_map->relocate_overlay_after_combat({2, 1});
        check(event_map->events().overlay.position == yipe::MapPosition{2, 2},
              "combat does not move an overlay outside environment five");

        event_map->restore_tile({6, 1}, 40);
        event_map->restore_tile({5, 8}, 34);
        check(event_map->toggle_linked_tiles() && event_map->tile({6, 1}) == 34
                  && event_map->tile({5, 8}) == 40,
              "linked-tile action recognizes and restores persisted swapped tiles");
    }

    std::optional<yipe::Map> moving_overlay = yipe::original_map(0x2c);
    check(moving_overlay.has_value(), "environment-five overlay fixture is available");
    if (moving_overlay) {
        moving_overlay->relocate_overlay_after_combat({5, 5});
        check(moving_overlay->events().overlay.position == yipe::MapPosition{6, 4},
              "combat on ordinary terrain leaves the overlay in place");
        moving_overlay->relocate_overlay_after_combat({6, 5});
        yipe::MapPosition old_overlay{6, 3};
        yipe::MapPosition new_overlay{5, 5};
        check(moving_overlay->events().overlay.position == yipe::MapPosition{6, 5}
                  && !yipe::try_move(*moving_overlay, old_overlay, 0, 1).position_changed
                  && yipe::try_move(*moving_overlay, new_overlay, 1, 0).original_code
                      == 0x18,
              "combat on a cave obstacle moves the passable overlay to that tile");
        check(yipe::original_map(0x2c)->events().overlay.position
                  == yipe::MapPosition{6, 4},
              "loading the map again restores its original overlay position");
    }

    const std::optional<yipe::Map> teleporter_map = yipe::original_map(0x54);
    check(teleporter_map && teleporter_map->linked_destination() == yipe::MapPosition{3, 4},
          "teleporter action exposes its recovered destination");

    yipe::Map::Tiles tiles{};
    tiles.fill(2);
    tiles[static_cast<std::size_t>(6 * yipe::Map::height + 5)] = 0x3e;
    yipe::Map door_map(0, 0, tiles);
    yipe::MapPosition position{5, 5};
    check(yipe::try_move(door_map, position, 1, 0).original_code == 0x12
              && position == yipe::MapPosition{6, 5},
          "door tile produces its mutable-map action");
    door_map.open_door({5, 5}, position);
    check(door_map.tile(position) == 2,
          "door action copies the previous floor tile over the entered door");

    tiles[static_cast<std::size_t>(6 * yipe::Map::height + 5)] = 0x3f;
    const yipe::Map locked_door(0, 0, tiles);
    position = {5, 5};
    check(yipe::try_move(locked_door, position, 1, 0).original_code == 0x13
              && position == yipe::MapPosition{5, 5},
          "rainbow-key door blocks a player without the key");
    yipe::MovementState movement_state;
    movement_state.has_rainbow_key = true;
    check(yipe::try_move(locked_door, position, 1, 0, movement_state).original_code == 0x12
              && position == yipe::MapPosition{6, 5},
          "rainbow key unlocks its original conditional door tile");

    tiles[static_cast<std::size_t>(6 * yipe::Map::height + 5)] = 0x58;
    const yipe::Map stream_map(0, 0, tiles);
    position = {5, 5};
    check(yipe::try_move(stream_map, position, 1, 0).original_code == 0
              && position == yipe::MapPosition{5, 5},
          "stream tile blocks a player without waterproof shoe spray");
    movement_state.has_waterproof_shoe_spray = true;
    check(yipe::try_move(stream_map, position, 1, 0, movement_state).original_code == 1
              && position == yipe::MapPosition{6, 5},
          "waterproof shoe spray opens its recovered stream tile");

    const std::optional<yipe::Map> secret_tree_map = yipe::original_map(1);
    movement_state.has_tree_climbing_shoes = true;
    position = {2, 7};
    check(secret_tree_map && secret_tree_map->tile({3, 7}) == 0x57
              && yipe::try_move(*secret_tree_map, position, 1, 0, movement_state)
                         .original_code
                  == 3
              && position == yipe::MapPosition{3, 7},
          "map 1 secret-tree tile reaches the ordinary movement-action reward path");

    yipe::Player tree_reward;
    tree_reward.gold = 12;
    tree_reward.experience = 34;
    tree_reward.restoration_uses = 2;
    check(yipe::enter_secret_tree_code(tree_reward, "HHHHHH")
                  == yipe::SecretTreeResult::incorrect_code
              && tree_reward.gold == 12 && tree_reward.experience == 34
              && tree_reward.restoration_uses == 2
              && !tree_reward.has_bonus_healing_ring,
          "secret-tree code comparison is exact and failed attempts do not mutate state");
    check(yipe::enter_secret_tree_code(tree_reward, "hhhhhh")
                  == yipe::SecretTreeResult::reward_granted
              && tree_reward.gold == 1512 && tree_reward.experience == 284
              && tree_reward.restoration_uses == 1
              && tree_reward.has_bonus_healing_ring,
          "secret-tree code grants the recovered one-time reward");
    check(yipe::enter_secret_tree_code(tree_reward, "hhhhhh")
                  == yipe::SecretTreeResult::already_solved
              && tree_reward.gold == 1512 && tree_reward.experience == 284
              && tree_reward.restoration_uses == 1,
          "the bonus-healing ring prevents repeating the secret-tree reward");
}

void test_starting_shop() {
    const auto stock = yipe::miscellaneous_shop_stock(yipe::Player{}, 0x11);
    check(stock.size() == 11, "Fred's shop exposes all original stock rows");
    check(stock[0].name == "French Fries" && stock[0].price == 75
              && stock[0].type == 1,
          "Fred's shop preserves the first consumable and price");
    check(stock[6].name == "Return to Surface Potion" && stock[6].price == 250
              && stock[6].type == 5,
          "Fred's shop preserves its last consumable");
    check(stock[7].name == "Wet Mittens" && stock[7].price == 50
              && stock[7].kind == yipe::ShopItemKind::equipment && stock[7].tier == 1,
          "Fred's shop preserves equipment type and tier");
    check(stock[10].name == "Plain Necklace" && stock[10].price == 75,
          "Fred's shop preserves its final row");

    yipe::Player player;
    player.gold = 200;
    const yipe::PurchaseResult food = yipe::purchase_shop_item(player, stock[0]);
    check(food.status == yipe::PurchaseStatus::purchased && food.inventory_slot == 1
              && player.inventory[1] == 1 && player.gold == 125,
          "Fred's shop adds a purchased consumable to the first free carrying slot");
    const yipe::PurchaseResult gloves = yipe::purchase_shop_item(player, stock[7]);
    check(gloves.status == yipe::PurchaseStatus::purchased && player.gloves_rank == 1
              && player.gold == 75,
          "Fred's shop equips an affordable improvement");
    check(yipe::purchase_shop_item(player, stock[7]).status
              == yipe::PurchaseStatus::already_equipped
              && player.gold == 75,
          "Fred's shop does not charge for an already equipped tier");
    check(yipe::purchase_shop_item(player, stock[1]).status
              == yipe::PurchaseStatus::insufficient_gold,
          "Fred's shop rejects purchases with insufficient gold");

    player.gold = 10000;
    std::fill_n(player.inventory.begin(), yipe::Player::carried_inventory_size, 1);
    check(yipe::purchase_shop_item(player, stock[0]).status
              == yipe::PurchaseStatus::inventory_full
              && player.gold == 10000,
          "Fred's shop preserves gold when all 13 carrying slots are full");
}

void test_miscellaneous_shops() {
    constexpr std::array maps{0x11, 0x1b, 8, 0x0c};
    constexpr std::array<std::array<int, 11>, 4> prices{{
        {75, 250, 1000, 1000, 2000, 2500, 250, 50, 100, 50, 75},
        {80, 275, 1000, 900, 2100, 1200, 200, 1500, 3000, 2500, 500},
        {75, 245, 800, 1900, 2750, 2000, 300, 10000, 2500, 6000, 8000},
        {70, 240, 900, 1000, 2600, 1000, 350, 500, 25, 1000, 5000},
    }};
    constexpr std::array<std::array<int, 11>, 4> types{{
        {1, 2, 6, 8, 9, 10, 5, 2, 3, 4, 7},
        {1, 2, 8, 7, 9, 4, 5, 2, 3, 4, 7},
        {1, 2, 8, 9, 10, 3, 5, 1, 2, 3, 4},
        {1, 2, 6, 7, 10, 4, 5, 1, 2, 3, 7},
    }};
    constexpr std::array<std::array<int, 4>, 4> tiers{{
        {1, 1, 1, 1}, {3, 3, 4, 2}, {7, 4, 5, 6}, {2, 1, 2, 5},
    }};
    for (std::size_t map_index = 0; map_index < maps.size(); ++map_index) {
        yipe::Player customer;
        const auto stock = yipe::miscellaneous_shop_stock(customer, maps[map_index]);
        customer.miscellaneous_shop_stage = 1;
        const auto inflated = yipe::miscellaneous_shop_stock(customer, maps[map_index]);
        for (std::size_t i = 0; i < stock.size(); ++i) {
            const auto& item = stock[i];
            check(item.price == prices[map_index][i] && item.type == types[map_index][i]
                      && item.kind == (i < 7 ? yipe::ShopItemKind::consumable : yipe::ShopItemKind::equipment)
                      && item.tier == (i < 7 ? 0 : tiers[map_index][i - 7]),
                  "miscellaneous stock matches source row " + std::to_string(i)
                      + " on map " + std::to_string(maps[map_index]));
            const bool stat_potion = i == 2 || (i == 3 && map_index != 2);
            check(inflated[i].price == prices[map_index][i] + (stat_potion ? 1500 : 0)
                      && inflated[i].name == item.name && inflated[i].type == item.type,
                  "only the seven original stat-potion prices rise by 1500");
            yipe::Player buyer;
            buyer.gold = item.price - 1;
            check(yipe::purchase_shop_item(buyer, item).status == yipe::PurchaseStatus::insufficient_gold
                      && buyer.gold == item.price - 1,
                  "each miscellaneous row rejects insufficient funds without charging");
            buyer.gold = item.price;
            const auto purchase = yipe::purchase_shop_item(buyer, item);
            check(purchase.status == yipe::PurchaseStatus::purchased && buyer.gold == 0,
                  "each miscellaneous row can be purchased with exact gold");
            if (i < 7) {
                check(purchase.inventory_slot == 1 && buyer.inventory[1] == item.type,
                      "remote consumables use the first free carried slot");
                std::fill_n(buyer.inventory.begin(), yipe::Player::carried_inventory_size, 1);
                buyer.gold = item.price;
                check(yipe::purchase_shop_item(buyer, item).status == yipe::PurchaseStatus::inventory_full
                          && buyer.gold == item.price && buyer.inventory[13] == 0,
                      "remote consumables cannot occupy reserved slots or charge on failure");
            } else {
                const int equipped = item.type == 1 ? buyer.helmet_rank
                    : item.type == 2 ? buyer.gloves_rank : item.type == 3 ? buyer.ring_rank
                    : item.type == 4 ? buyer.boots_rank : buyer.necklace_rank;
                check(equipped == tiers[map_index][i - 7], "remote accessories equip the recovered tier");
                buyer.gold = item.price;
                check(yipe::purchase_shop_item(buyer, item).status == yipe::PurchaseStatus::already_equipped
                          && buyer.gold == item.price,
                      "remote accessories cannot be bought repeatedly at the same tier");
            }
        }
    }

    for (int difficulty : {1, 2, 3}) {
        yipe::Player customer;
        customer.difficulty = difficulty;
        customer.strength = 60 - difficulty - customer.dexterity - customer.intelligence;
        customer.level = 26;
        check(!yipe::enter_miscellaneous_shop(customer) && customer.miscellaneous_shop_stage == 0,
              "level alone and equality at the stat threshold cannot unlock the shop stage");
        ++customer.strength;
        customer.level = 25;
        check(yipe::enter_miscellaneous_shop(customer) && customer.miscellaneous_shop_stage == 1,
              "the first strictly greater stat total triggers the price notice");
        customer.strength = 1;
        check(!yipe::enter_miscellaneous_shop(customer) && customer.miscellaneous_shop_stage == 1,
              "lower stats neither undo the price increase nor repeat its notice");
        customer.level = 26;
        check(!yipe::enter_miscellaneous_shop(customer) && customer.miscellaneous_shop_stage == 2,
              "level 26 unlocks the key after the price milestone");
        const auto key = yipe::miscellaneous_shop_stock(customer, 0x0c)[8];
        check(key.name == "Rainbow Key" && key.type == 8 && key.price == (difficulty + 6) * 500,
              "Outback replaces its mittens row with the difficulty-priced Rainbow Key");
        customer.inventory.fill(1);
        customer.gold = key.price - 1;
        check(yipe::purchase_shop_item(customer, key).status == yipe::PurchaseStatus::insufficient_gold
                  && !customer.has_rainbow_key && customer.miscellaneous_shop_stage == 2,
              "failed key purchases cannot change the key or shop stage");
        customer.gold = key.price * 2;
        const auto original_inventory = customer.inventory;
        check(yipe::purchase_shop_item(customer, key).status == yipe::PurchaseStatus::purchased
                  && customer.has_rainbow_key && customer.miscellaneous_shop_stage == 3
                  && customer.gold == key.price && customer.inventory == original_inventory,
              "purchased Rainbow Key uses its special flag even with full inventory");
        check(yipe::purchase_shop_item(customer, key).status == yipe::PurchaseStatus::already_equipped
                  && customer.gold == key.price,
              "a second key purchase in the same visit is rejected without charging");
        const auto rebuilt_during_visit = yipe::miscellaneous_shop_stock(customer, 0x0c)[8];
        check(rebuilt_during_visit.name == "Wet Mittens" && rebuilt_during_visit.type == 8
                  && rebuilt_during_visit.price == key.price,
              "a rebuilt stage-3 stock list preserves the original label/purchase mismatch");
        customer.miscellaneous_shop_stage = 4;
        const auto after_exit = yipe::miscellaneous_shop_stock(customer, 0x0c)[8];
        check(after_exit.name == "Wet Mittens" && after_exit.type == 2 && after_exit.price == 25,
              "after leaving with the key the Outback row returns to ordinary mittens");
        customer.miscellaneous_shop_stage = 2;
        check(yipe::miscellaneous_shop_stock(customer, 0x0c)[8].type == 2,
              "a Rainbow Key obtained elsewhere prevents the shop's special offer");
    }

    yipe::Player growing;
    growing.gold = 20000;
    yipe::MsvcRandom random(1);
    const auto town = yipe::miscellaneous_shop_stock(growing, 0x11);
    for (int i = 0; i < 20; ++i) {
        const auto bought = yipe::purchase_shop_item(growing, town[3]);
        check(bought.status == yipe::PurchaseStatus::purchased
                  && growing.use_inventory_item(bought.inventory_slot, random).consumed,
              "purchased intelligence potions build stats through the ordinary inventory API");
    }
    check(growing.intelligence == 30 && growing.gold == 0
              && yipe::enter_miscellaneous_shop(growing) && growing.miscellaneous_shop_stage == 1,
          "funded ordinary growth earns the price milestone without supplying the shop flag");
}

void test_recovered_shop_transactions() {
    const auto town_weapons =
        yipe::equipment_shop_stock(yipe::EquipmentKind::weapon, 0x11);
    check(town_weapons.front().rank == 2 && town_weapons[9].rank == 13
              && town_weapons.back().rank == 16,
          "town weapon shop preserves its nonsequential original stock");
    const auto remote_weapons =
        yipe::equipment_shop_stock(yipe::EquipmentKind::weapon, 0x08);
    check(remote_weapons.front().rank == 19 && remote_weapons.back().rank == 36,
          "remote weapon shop preserves its high-rank original stock");
    const auto cave_armor =
        yipe::equipment_shop_stock(yipe::EquipmentKind::armor, 0x1b);
    check(cave_armor.front().rank == 9 && cave_armor.back().rank == 28,
          "cave armor shop preserves its original stock");
    check(yipe::equipment_price(yipe::EquipmentKind::weapon, 38) == 55000
              && yipe::equipment_price(yipe::EquipmentKind::armor, 30) == 24250,
          "equipment price tables preserve their final recovered values");

    yipe::Player equipment_buyer;
    equipment_buyer.gold = 0;
    check(yipe::purchase_equipment(equipment_buyer, yipe::EquipmentKind::weapon, 3)
              == yipe::EquipmentTransactionStatus::must_sell_current,
          "equipment purchase requires selling the current item first");
    check(yipe::equipment_sale_value(equipment_buyer, yipe::EquipmentKind::weapon) == 25
              && yipe::equipment_sale_requires_confirmation(
                  equipment_buyer, yipe::EquipmentKind::weapon),
          "equipment sale returns half price and warns when no upgrade is affordable");
    check(yipe::sell_equipment(equipment_buyer, yipe::EquipmentKind::weapon)
              == yipe::EquipmentTransactionStatus::sold
              && equipment_buyer.weapon_rank == 1 && equipment_buyer.gold == 25,
          "equipment sale pays gold and restores the empty rank");
    equipment_buyer.gold = 2000;
    check(yipe::purchase_equipment(equipment_buyer, yipe::EquipmentKind::weapon, 3)
              == yipe::EquipmentTransactionStatus::purchased
              && equipment_buyer.weapon_rank == 3 && equipment_buyer.gold == 1890,
          "equipment purchase deducts the recovered rank price");

    yipe::Player patient;
    patient.health = 1;
    patient.maximum_health = 100;
    patient.level = 18;
    patient.gold = 200;
    yipe::MsvcRandom healing_random(1);
    const yipe::ServiceResult apple =
        yipe::purchase_healing(patient, 0, healing_random);
    check(apple.status == yipe::ServiceStatus::completed && apple.amount == 6
              && patient.health == 7 && patient.gold == 195,
          "heal shop applies the recovered apple roll and level bonus");
    const yipe::ServiceResult dinner =
        yipe::purchase_healing(patient, 3, healing_random);
    check(dinner.status == yipe::ServiceStatus::completed && patient.health == 100
              && patient.gold == 70,
          "heal shop turkey dinner fully restores health");
    check(yipe::purchase_healing(patient, 0, healing_random).status
              == yipe::ServiceStatus::already_full,
          "heal shop does not charge a full-health player");

    yipe::Player magic_customer;
    magic_customer.spell_points = 0;
    magic_customer.maximum_spell_points = 100;
    magic_customer.gold = 50;
    yipe::MsvcRandom magic_random(1);
    const yipe::ServiceResult sp_heal =
        yipe::purchase_spell_point_healing(magic_customer, false, magic_random);
    check(sp_heal.status == yipe::ServiceStatus::completed && sp_heal.amount == 17
              && magic_customer.spell_points == 17 && magic_customer.gold == 0,
          "magic shop small heal uses the recovered random and level bonuses");
    magic_customer.level = 2;
    magic_customer.gold = 100;
    check(yipe::next_spell_offer(magic_customer)->name == "Small Heal"
              && yipe::learn_next_spell(magic_customer)
                  == yipe::SpellLearningStatus::insufficient_gold,
          "spell learning preserves the original strict gold comparison");
    magic_customer.gold = 101;
    check(yipe::learn_next_spell(magic_customer) == yipe::SpellLearningStatus::learned
              && magic_customer.spells[1] == 1 && magic_customer.gold == 1
              && !yipe::next_spell_offer(magic_customer),
          "magic shop learns the first eligible unlearned spell");

    yipe::Player trainee;
    trainee.gold = 400;
    check(yipe::ability_offer_for_environment(7)->ability
              == yipe::SpecialAbility::invisibility
              && yipe::train_ability(trainee, 7)
                  == yipe::AbilityTrainingStatus::learned
              && trainee.special_ability == yipe::SpecialAbility::invisibility
              && trainee.gold == 0,
          "training center maps environment 7 to the 400-gold invisibility lesson");
    check(yipe::train_ability(trainee, 7)
              == yipe::AbilityTrainingStatus::already_trained
              && !yipe::ability_offer_for_environment(0),
          "training rejects the current ability and unsupported environments");
}

void test_recovered_casino_games() {
    yipe::CasinoWager wager{300, 0};
    check(yipe::place_casino_wager(wager, 100) == yipe::WagerStatus::placed
              && wager.gold == 200 && wager.bet == 100,
          "casino wager moves gold into the active bet");
    check(yipe::place_casino_wager(wager, 200)
              == yipe::WagerStatus::maximum_reached
              && wager.gold == 200 && wager.bet == 100,
          "casino wager enforces the original 250-gold maximum");
    yipe::clear_casino_wager(wager);
    check(wager.gold == 300 && wager.bet == 0,
          "clearing a casino wager returns it to available gold");

    yipe::SlotMachineState slots{20, 0};
    check(yipe::insert_slot_credits(slots) && slots.gold == 10 && slots.credits == 10,
          "slot machine exchanges ten gold for ten credits");
    yipe::MsvcRandom slot_random(1);
    const yipe::SlotSpinResult spin = yipe::spin_slots(slots, slot_random);
    check(spin.spun && spin.symbols == std::array<int, 3>{5, 80, 60}
              && spin.payout == 0 && slots.credits == 0,
          "seed 1 slot spin preserves the recovered reel sequence");
    check(slot_random.state() == 0x07323f41u,
          "unforced slot spin consumes the original 64 random values");

    yipe::CasinoWager race_wager{100, 0};
    static_cast<void>(yipe::place_casino_wager(race_wager, 25));
    yipe::MsvcRandom race_random(1);
    const yipe::DotRaceResult race = yipe::race_dots(race_wager, 2, race_random);
    check(race.raced && race.winner == 2
              && race.final_positions == std::array<int, 3>{460, 482, 464}
              && race.payout == 62 && race_wager.gold == 137 && race_wager.bet == 0,
          "seed 1 dot race awards the winning dot two-and-a-half times the bet");
    check(race_random.state() == 0x2b0c457fu,
          "dot race advances all three dots once per animation frame");

    check(yipe::adjust_dart_aim(16, true) == 16
              && yipe::adjust_dart_aim(73, false) == 78
              && yipe::adjust_dart_aim(78, false) == 78,
          "darts aim controls preserve the original upper clamp and lower overshoot");
    yipe::MsvcRandom dart_random(1);
    const yipe::DartThrowResult dart_1 =
        yipe::throw_dart(yipe::initial_dart_aim, 0, dart_random);
    const yipe::DartThrowResult dart_2 =
        yipe::throw_dart(yipe::initial_dart_aim, 1, dart_random);
    const yipe::DartThrowResult dart_3 =
        yipe::throw_dart(yipe::initial_dart_aim, 2, dart_random);
    check(dart_1.final_y == 28 && dart_1.score == 3
              && dart_2.final_y == 33 && dart_2.score == 8
              && dart_3.final_y == 20 && dart_3.score == 0,
          "seed 1 darts round preserves drift and scoring bands");
    check(dart_random.state() == 0x7a610fffu,
          "darts throws preserve their variable original random-call sequence");
    check(yipe::dart_round_payout({50, 50, 50}, 25) == 250
              && yipe::dart_round_payout({40, 40, 40}, 25) == 200
              && yipe::dart_round_payout({35, 35, 35}, 25) == 150
              && yipe::dart_round_payout({30, 30, 28}, 25) == 100
              && yipe::dart_round_payout({25, 24, 24}, 25) == 50
              && yipe::dart_round_payout({20, 20, 15}, 25) == 25
              && yipe::dart_round_payout({20, 20, 14}, 25) == 0,
          "darts payouts preserve every recovered score threshold");
}

void test_giant_beetle_encounter() {
    const auto placements = yipe::original_monsters_for_map(0x0b);
    check(placements.size() == 1 && placements[0].placement_index == 5
              && placements[0].type_id == 2
              && placements[0].position == yipe::MapPosition{2, 8}
              && placements[0].slot == 0 && placements[0].encounter_size == 1
              && !placements[0].respawns_after_map_reload
              && !placements[0].occupies_two_by_two_tiles,
          "map 0x0b exposes the recovered Giant Beetle placement");
    check(yipe::original_monsters_for_map(0x11).empty(),
          "maps without recovered placements remain empty");

    yipe::Player player;
    yipe::MsvcRandom random(1);
    yipe::Monster monster = yipe::start_single_monster_encounter(2, random);
    check(monster.name == "Giant Beetle" && monster.bitmap_resource_id == 175
              && monster.health == 15 && monster.attack_rating == 9
              && monster.defense_rating == 3,
          "seed 1 creates the independently traced Giant Beetle stats");

    constexpr std::array expected_player_damage{0, 1, 3, 2, 3, 0, 3, 2, 1};
    constexpr std::array expected_monster_damage{2, 0, 3, 0, 1, 3, 2, 1, 0};
    yipe::CombatTurn turn;
    for (std::size_t index = 0; index < expected_player_damage.size(); ++index) {
        turn = yipe::resolve_attack_turn(player, monster, random);
        check(turn.player_damage == expected_player_damage[index]
                  && turn.monster_damage == expected_monster_damage[index],
              "seed 1 combat turn " + std::to_string(index + 1));
    }
    check(turn.outcome == yipe::CombatOutcome::victory && turn.reward.experience == 41
              && turn.reward.gold == 43
              && turn.reward.equipment_upgrade == yipe::EquipmentUpgrade::none,
          "seed 1 victory produces the independently traced reward");
    check(player.health == 1 && player.experience == 41 && player.gold == 43,
          "seed 1 combat applies damage and rewards to the player");

    yipe::Player fleeing_player;
    fleeing_player.gold = 10;
    yipe::MsvcRandom flee_random(1);
    const yipe::Monster fleeing_monster =
        yipe::start_single_monster_encounter(2, flee_random);
    const yipe::FleeResult fled = yipe::try_flee(fleeing_player, fleeing_monster, flee_random);
    check(fled.escaped && fleeing_player.gold == 9 && fleeing_player.health == 13,
          "low-level flee succeeds and applies the original gold loss");
}

void test_multi_monster_encounter() {
    // Measured from the supplied original's 0x004042e0 damage routine. These
    // cover its low-HP decrement and the defense-roll boundaries at 28 and 38.
    struct CounterattackCase {
        int level;
        int health;
        std::uint32_t seed;
        int damage;
        std::uint32_t after;
    };
    for (const auto& fixture : std::array{
             CounterattackCase{2, 1, 1, 23, 1844513277u},
             CounterattackCase{3, 1, 1, 24, 1844513277u},
             CounterattackCase{28, 1000, 4, 11, 2492333137u},
             CounterattackCase{37, 1000, 3, 2, 354542668u},
             CounterattackCase{38, 1000, 1, 33, 1844513277u}}) {
        yipe::Player player;
        player.difficulty = 1;
        player.level = fixture.level;
        player.health = fixture.health;
        player.maximum_health = 1000;
        player.dexterity = 12;
        player.armor_rank = 1;
        player.helmet_rank = 0;
        yipe::CombatEncounter group;
        group.monsters[0].type_id = 1;
        group.monsters[0].health = group.monsters[0].maximum_health = 1000;
        group.monsters[0].rank = 25;
        group.monsters[0].attack_rating = 30;
        yipe::MsvcRandom random(fixture.seed);
        const auto turn = yipe::resolve_monster_turn(player, group, random);
        check(turn.monster_damage == fixture.damage
                  && player.health == fixture.health - fixture.damage
                  && random.state() == fixture.after,
              "original counterattack damage, HP and RNG at level "
                  + std::to_string(fixture.level));
    }

    yipe::Player missed_flee_player;
    missed_flee_player.level = 4;
    missed_flee_player.difficulty = 3;
    missed_flee_player.health = missed_flee_player.maximum_health = 10;
    missed_flee_player.dexterity = 12;
    missed_flee_player.armor_rank = 1;
    missed_flee_player.helmet_rank = 0;
    yipe::Monster missed_flee_monster;
    missed_flee_monster.type_id = 7;
    missed_flee_monster.health = missed_flee_monster.maximum_health = 1000;
    missed_flee_monster.rank = 25;
    missed_flee_monster.attack_rating = 30;
    yipe::MsvcRandom missed_flee_random(7);
    const auto missed_flee = yipe::try_flee(
        missed_flee_player, missed_flee_monster, missed_flee_random);
    check(!missed_flee.escaped && missed_flee.monster_damage == 0
              && missed_flee_player.health == 10
              && missed_flee_random.state() == 1333266977u,
          "failed flee preserves an original zero-damage miss instead of forcing one HP damage");

    yipe::MsvcRandom setup_random(1);
    const yipe::CombatEncounter encounter =
        yipe::start_monster_encounter(2, 3, setup_random);
    check(encounter.monsters[0].health == 15
              && encounter.monsters[0].attack_rating == 9
              && encounter.monsters[0].defense_rating == 3
              && encounter.monsters[1].health == 15
              && encounter.monsters[1].attack_rating == 8
              && encounter.monsters[1].defense_rating == 3
              && encounter.monsters[2].health == 14
              && encounter.monsters[2].attack_rating == 8
              && encounter.monsters[2].defense_rating == 3,
          "three-opponent setup preserves the original initialization order");

    yipe::Player reference_player;
    reference_player.health = 1000;
    reference_player.maximum_health = 1000;
    yipe::MsvcRandom reference_random = setup_random;
    std::array<int, 3> full_damage{};
    for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
        full_damage[slot] =
            yipe::resolve_monster_turn(reference_player, encounter.monsters[slot],
                                       reference_random)
                .monster_damage;
    }

    yipe::Player group_player;
    group_player.health = 1000;
    group_player.maximum_health = 1000;
    yipe::MsvcRandom group_random = setup_random;
    const yipe::CombatTurn group_turn =
        yipe::resolve_monster_turn(group_player, encounter, group_random);
    check(group_turn.monster_damage_by_slot[0] == full_damage[0]
              && group_turn.monster_damage_by_slot[1] == full_damage[1]
              && group_turn.monster_damage_by_slot[2] == full_damage[2] / 2,
          "all living opponents counterattack and slot three deals half damage");
    check(group_turn.monster_damage == full_damage[0] + full_damage[1] + full_damage[2] / 2
              && group_player.health == 1000 - group_turn.monster_damage
              && group_random.state() == reference_random.state(),
          "group counterattacks preserve aggregate damage and random-call order");

    yipe::CombatEncounter attack_encounter = encounter;
    attack_encounter.selected_target = 1;
    attack_encounter.monsters[1].health = 1;
    yipe::Player attacking_player;
    attacking_player.health = 1000;
    attacking_player.maximum_health = 1000;
    attacking_player.level = 40;
    attacking_player.strength = 100;
    attacking_player.weapon_rank = 42;
    yipe::MsvcRandom attack_random(1);
    const yipe::CombatTurn attack =
        yipe::resolve_attack_turn(attacking_player, attack_encounter, attack_random);
    check(attack.player_damage > 0 && attack_encounter.monsters[1].health < 1
              && attack.monster_damage_by_slot[1] == 0
              && attack_encounter.selected_target == 0
              && attack.outcome == yipe::CombatOutcome::ongoing,
          "a defeated selected target is skipped and replaced by a living target");
    check(attack.reward.experience == 0 && attacking_player.experience == 0
              && attack_encounter.pending_reward.experience > 0,
          "multi-opponent combat defers accumulated rewards until victory");
    const yipe::CombatReward first_pending_reward = attack_encounter.pending_reward;

    attack_encounter.monsters[0].health = 1;
    const yipe::CombatTurn second_attack =
        yipe::resolve_attack_turn(attacking_player, attack_encounter, attack_random);
    check(second_attack.reward.experience == 0 && attacking_player.experience == 0
              && attack_encounter.pending_reward.experience
                  > first_pending_reward.experience,
          "later defeated opponents continue accumulating deferred rewards");
    attack_encounter.monsters[2].health = 1;
    const yipe::CombatTurn final_attack =
        yipe::resolve_attack_turn(attacking_player, attack_encounter, attack_random);
    check(second_attack.outcome == yipe::CombatOutcome::ongoing
              && final_attack.outcome == yipe::CombatOutcome::victory
              && final_attack.reward.experience == attacking_player.experience
              && final_attack.reward.gold == attacking_player.gold
              && final_attack.reward.experience
                  == attack_encounter.pending_reward.experience,
          "group victory applies the accumulated encounter reward once");

    yipe::MsvcRandom flee_random(6);
    const yipe::CombatEncounter flee_encounter =
        yipe::start_monster_encounter(41, 3, flee_random);
    yipe::MsvcRandom flee_reference_random = flee_random;
    (void)flee_reference_random.next();
    yipe::Player flee_reference_player;
    flee_reference_player.health = 1000;
    flee_reference_player.maximum_health = 1000;
    flee_reference_player.level = 40;
    std::array<int, 3> expected_flee_damage{};
    for (std::size_t slot = 0; slot < flee_encounter.monsters.size(); ++slot) {
        int damage =
            yipe::resolve_monster_turn(flee_reference_player,
                                       flee_encounter.monsters[slot],
                                       flee_reference_random)
                .monster_damage;
        if (slot == 2) {
            damage /= 2;
        }
        if (damage > 10) {
            damage -= 3;
        }
        expected_flee_damage[slot] = std::max(damage, 1);
    }

    yipe::Player fleeing_player;
    fleeing_player.health = 1000;
    fleeing_player.maximum_health = 1000;
    fleeing_player.level = 40;
    const yipe::FleeResult flee =
        yipe::try_flee(fleeing_player, flee_encounter, flee_random);
    check(!flee.escaped
              && flee.monster_damage_by_slot == expected_flee_damage
              && flee.monster_damage == expected_flee_damage[0]
                      + expected_flee_damage[1] + expected_flee_damage[2]
              && flee_random.state() == flee_reference_random.state(),
          "a failed group flee resolves every opponent in slot order");

    yipe::CombatEncounter dead_primary_encounter = flee_encounter;
    dead_primary_encounter.monsters[0].health = 0;
    yipe::Player dead_primary_player;
    dead_primary_player.level = 40;
    yipe::MsvcRandom dead_primary_random(1);
    const yipe::FleeResult dead_primary_flee =
        yipe::try_flee(dead_primary_player, dead_primary_encounter,
                       dead_primary_random);
    check(dead_primary_flee.escaped,
          "fleeing remains available while a later opponent is alive");

    yipe::Player defeated_player;
    defeated_player.health = 1;
    defeated_player.maximum_health = 1;
    yipe::MsvcRandom defeat_random = setup_random;
    const yipe::CombatTurn defeat =
        yipe::resolve_monster_turn(defeated_player, encounter, defeat_random);
    check(defeat.outcome == yipe::CombatOutcome::defeat
              && defeated_player.health < 1,
          "group counterattacks report player defeat");

    yipe::MsvcRandom single_generated_random(1);
    const yipe::CombatEncounter single_generated =
        yipe::start_monster_encounter(17, 0, single_generated_random);
    check(single_generated.monsters[0].type_id == 17
              && single_generated.monsters[0].health == 122
              && single_generated.monsters[0].attack_rating == 18
              && single_generated.monsters[0].defense_rating == 13
              && single_generated.monsters[1].type_id == 0
              && single_generated.monsters[2].type_id == 0
              && single_generated_random.state() == 3403800452u,
          "a low-level generated encounter preserves the seeded single-opponent setup");

    yipe::MsvcRandom three_generated_random(13);
    const yipe::CombatEncounter three_generated =
        yipe::start_monster_encounter(17, 0, three_generated_random);
    check(three_generated.monsters[0].type_id == 17
              && three_generated.monsters[0].health == 126
              && three_generated.monsters[1].type_id == 17
              && three_generated.monsters[1].health == 129
              && three_generated.monsters[2].type_id == 18
              && three_generated.monsters[2].health == 129
              && three_generated_random.state() == 1879152969u,
          "a generated encounter preserves its seeded count, variant, and random order");

    bool rejected_negative_opponents = false;
    try {
        yipe::MsvcRandom random(1);
        (void)yipe::start_monster_encounter(2, -1, random);
    } catch (const std::invalid_argument&) {
        rejected_negative_opponents = true;
    }
    bool rejected_four_opponents = false;
    try {
        yipe::MsvcRandom random(1);
        (void)yipe::start_monster_encounter(2, 4, random);
    } catch (const std::invalid_argument&) {
        rejected_four_opponents = true;
    }
    check(rejected_negative_opponents && rejected_four_opponents,
          "encounters reject counts outside zero through three");
}

void test_special_abilities() {
    yipe::Monster backstab_target;
    backstab_target.type_id = 2;
    backstab_target.health = 15;
    backstab_target.maximum_health = 15;
    yipe::Player backstabber;
    backstabber.special_ability = yipe::SpecialAbility::backstab;
    yipe::MsvcRandom backstab_random(1);
    const yipe::CombatTurn backstab = yipe::resolve_special_ability_turn(
        backstabber, backstab_target, backstab_random);
    check(backstab.player_damage == 6 && backstab_target.health == 9
              && backstabber.ability_points == 2,
          "Backstab uses its recovered damage floor and costs three AP");
    check(backstab.monster_damage == 0 && backstabber.health == 13
              && backstab_random.state() == 0xc823f683u,
          "Backstab consumes two rolls and prevents a counterattack");

    backstabber.ability_points = 2;
    const int health_before_failed_backstab = backstab_target.health;
    const std::uint32_t random_before_failed_backstab = backstab_random.state();
    static_cast<void>(yipe::resolve_special_ability_turn(
        backstabber, backstab_target, backstab_random));
    check(backstab_target.health == health_before_failed_backstab
              && backstab_random.state() == random_before_failed_backstab,
          "an ability with fewer than three AP does nothing");

    yipe::Monster powerful_target = backstab_target;
    powerful_target.health = 100;
    yipe::Player powerful_backstabber;
    powerful_backstabber.special_ability = yipe::SpecialAbility::backstab;
    yipe::MsvcRandom powerful_backstab_random(1);
    const yipe::CombatTurn powerful_backstab = yipe::resolve_special_ability_turn(
        powerful_backstabber, powerful_target, powerful_backstab_random);
    check(powerful_backstab.player_damage == 15 && powerful_target.health == 85,
          "Backstab preserves the high-health minimum damage");

    yipe::MsvcRandom group_setup_random(1);
    const yipe::CombatEncounter group =
        yipe::start_monster_encounter(2, 3, group_setup_random);
    yipe::CombatEncounter frightened_group = group;
    frightened_group.pending_reward.experience = 4;
    yipe::Player frightener;
    frightener.special_ability = yipe::SpecialAbility::frighten;
    yipe::MsvcRandom frighten_random(1);
    static_cast<void>(yipe::resolve_special_ability_turn(
        frightener, frightened_group, frighten_random));
    check(frightened_group.monsters[2].type_id == 0
              && frightened_group.monsters[2].health == 0
              && frightened_group.pending_reward.experience
                  == 4 + group.monsters[2].attack_rating + 2,
          "Frighten removes slot three and adds its recovered XP amount");
    check(frightener.ability_points == 2 && frighten_random.state() == 1,
          "Frighten costs three AP without consuming a random value");

    yipe::CombatEncounter two_opponents = group;
    two_opponents.monsters[2] = {};
    yipe::Player failed_frightener;
    failed_frightener.special_ability = yipe::SpecialAbility::frighten;
    yipe::MsvcRandom failed_frighten_random(1);
    static_cast<void>(yipe::resolve_special_ability_turn(
        failed_frightener, two_opponents, failed_frighten_random));
    check(failed_frightener.ability_points == 5,
          "Frighten is not charged when no third opponent exists");

    yipe::CombatEncounter invisible_group = group;
    yipe::Player invisible_player;
    invisible_player.health = 1000;
    invisible_player.maximum_health = 1000;
    invisible_player.special_ability = yipe::SpecialAbility::invisibility;
    yipe::MsvcRandom invisibility_random(1);
    static_cast<void>(yipe::resolve_special_ability_turn(
        invisible_player, invisible_group, invisibility_random));
    check(invisible_player.special_ability_duration == 32
              && invisible_player.ability_points == 2,
          "Invisibility duration scales for the second and third opponents");
    const yipe::CombatTurn invisible_counterattacks = yipe::resolve_monster_turn(
        invisible_player, invisible_group, invisibility_random);
    check(invisible_counterattacks.monster_damage == 0
              && invisible_player.health == 1000
              && invisible_player.special_ability_duration == 29,
          "Invisibility blocks each monster attack and spends one duration per attacker");

    yipe::CombatEncounter invisible_attack_group = group;
    yipe::Player invisible_attacker;
    invisible_attacker.health = 1000;
    invisible_attacker.maximum_health = 1000;
    invisible_attacker.special_ability = yipe::SpecialAbility::invisibility;
    invisible_attacker.special_ability_duration = 7;
    yipe::MsvcRandom invisible_attack_random(1);
    const yipe::CombatTurn invisible_attack = yipe::resolve_attack_turn(
        invisible_attacker, invisible_attack_group, invisible_attack_random);
    check(invisible_attack.player_damage == 1
              && invisible_attack.monster_damage == 0
              && invisible_attacker.special_ability_duration == 4
              && invisible_attack_random.state() == 2453175039u,
          "an invisible attack applies its situational bonus before the equipment roll");

    yipe::CombatEncounter lucky_attack_group = group;
    yipe::Player lucky_attacker;
    lucky_attacker.health = 1000;
    lucky_attacker.maximum_health = 1000;
    lucky_attacker.has_lucky_rabbit_foot = true;
    yipe::MsvcRandom lucky_attack_random(1);
    const yipe::CombatTurn lucky_attack = yipe::resolve_attack_turn(
        lucky_attacker, lucky_attack_group, lucky_attack_random);
    check(lucky_attack.player_damage == 1 && lucky_attack.monster_damage == 3
              && lucky_attack_random.state() == 4200262242u,
          "the Lucky Rabbit Foot applies its situational attack roll in original order");

    yipe::CombatEncounter enraged_group = group;
    yipe::Player enraged_player;
    enraged_player.special_ability = yipe::SpecialAbility::enrage;
    yipe::MsvcRandom enrage_random(1);
    static_cast<void>(yipe::resolve_special_ability_turn(
        enraged_player, enraged_group, enrage_random));
    check(enraged_player.special_ability_duration == 14
              && enraged_player.dexterity == 19
              && enraged_player.strength == 24
              && enraged_player.intelligence == 11
              && enraged_player.ability_points == 2,
          "Enrage preserves its seeded duration and temporary stat boosts");

    yipe::MsvcRandom gatekeeper_setup_random(1);
    const yipe::Monster gatekeeper =
        yipe::start_single_monster_encounter(41, gatekeeper_setup_random);
    yipe::Player reference_player;
    reference_player.health = 1000;
    reference_player.maximum_health = 1000;
    reference_player.level = 40;
    yipe::Player expiring_enrage = reference_player;
    expiring_enrage.special_ability = yipe::SpecialAbility::enrage;
    expiring_enrage.special_ability_duration = 1;
    expiring_enrage.enrage_base_dexterity = expiring_enrage.dexterity;
    expiring_enrage.enrage_base_intelligence = expiring_enrage.intelligence;
    expiring_enrage.enrage_base_strength = expiring_enrage.strength;
    yipe::MsvcRandom reference_attack_random(7);
    yipe::MsvcRandom enrage_attack_random(7);
    const yipe::CombatTurn reference_attack = yipe::resolve_monster_turn(
        reference_player, gatekeeper, reference_attack_random);
    const yipe::CombatTurn reduced_attack = yipe::resolve_monster_turn(
        expiring_enrage, gatekeeper, enrage_attack_random);
    check(reference_attack.monster_damage > 2
              && reduced_attack.monster_damage == reference_attack.monster_damage - 1,
          "Enrage reduces qualifying monster damage by one");
    check(expiring_enrage.special_ability_duration == 0
              && expiring_enrage.enrage_base_dexterity == 0
              && expiring_enrage.enrage_base_intelligence == 0
              && expiring_enrage.enrage_base_strength == 0
              && expiring_enrage.dexterity == reference_player.dexterity
              && expiring_enrage.intelligence == reference_player.intelligence
              && expiring_enrage.strength == reference_player.strength,
          "expired Enrage restores the original player stats");
}

void test_monster_placement_catalog() {
    std::size_t placement_count = 0;
    std::array<bool, yipe::original_monster_placement_count> placement_indices{};
    int respawning_count = 0;
    int two_by_two_count = 0;
    for (int map_id = 0; map_id <= 0x85; ++map_id) {
        for (const yipe::MonsterPlacement& placement :
             yipe::original_monsters_for_map(map_id)) {
            ++placement_count;
            if (placement.placement_index < placement_indices.size()) {
                placement_indices[placement.placement_index] = true;
            }
            respawning_count += placement.respawns_after_map_reload ? 1 : 0;
            two_by_two_count += placement.occupies_two_by_two_tiles ? 1 : 0;
        }
    }
    check(placement_count == 186 && respawning_count == 23
              && two_by_two_count == 16,
          "generated catalog exposes every recovered monster placement behavior");
    check(std::all_of(placement_indices.begin(), placement_indices.end(),
                      [](bool present) { return present; }),
          "every recovered placement has a stable catalog index");

    check(yipe::original_monsters_for_map(8, 3, 1)[1].encounter_size == 3,
          "difficulty controls dynamic encounter size");
    check(yipe::original_monsters_for_map(0x39, 2, 6)[1].type_id == 1
              && yipe::original_monsters_for_map(0x39, 2, 7)[1].type_id == 5,
          "map 0x39 changes its monster type at level 7");
    check(yipe::original_monsters_for_map(0x75, 2, 2)[0].type_id == 3
              && yipe::original_monsters_for_map(0x75, 2, 17)[0].type_id == 18
              && yipe::original_monsters_for_map(0x75, 2, 21)[0].type_id == 20,
          "map 0x75 preserves its level-dependent monster normalization");
    check(yipe::original_monsters_for_map(0x76, 2, 1)[0].type_id == 32
              && yipe::original_monsters_for_map(0x77, 2, 1)[0].type_id == 33
              && yipe::original_monsters_for_map(0x78, 2, 1)[0].type_id == 34,
          "generated maps select their distinct monster types");
    check(yipe::original_monsters_for_map(0x79, 3, 1)[0].type_id == 38
              && yipe::original_monsters_for_map(0x7f, 3, 1)[0].type_id == 39,
          "late special encounters derive monster types from difficulty");
    check(yipe::original_monsters_for_map(0x4c, 2, 1)[0].companion_type_1 == 8
              && yipe::original_monsters_for_map(0x4c, 2, 1)[0].companion_type_2 == 8
              && yipe::original_monsters_for_map(0x5f, 2, 1)[0].companion_type_2 == 16
              && yipe::original_monsters_for_map(0x5f, 3, 1)[0].companion_type_2 == 20,
          "extended placements preserve fixed and difficulty-derived companions");
    check(yipe::original_monsters_for_map(0x6e, 2, 1)[0].companion_type_2 == 0
              && yipe::original_monsters_for_map(0x6e, 3, 1)[0].companion_type_2 == 12,
          "map 0x6e enables its third companion only on difficulty three");
    const yipe::MonsterPlacement large =
        yipe::original_monsters_for_map(0x4c, 2, 1)[0];
    check(yipe::monster_placement_contains(large, {8, 5})
              && yipe::monster_placement_contains(large, {9, 5})
              && yipe::monster_placement_contains(large, {8, 6})
              && yipe::monster_placement_contains(large, {9, 6})
              && !yipe::monster_placement_contains(large, {10, 6}),
          "extended placements occupy the recovered two-by-two footprint");
}

void test_roaming_monsters() {
    yipe::Player player;
    player.level = 10;
    player.health = 10;
    player.maximum_health = 10;
    player.random_encounter_threshold = 40;
    yipe::WorldLocation location;
    location.map_id = 20;

    yipe::MsvcRandom trigger_random(1);
    check(yipe::should_spawn_roaming_monster(
              player, location, 2, trigger_random),
          "roaming encounter threshold accepts a roll above the adjusted chance");
    yipe::MsvcRandom terrain_random(1);
    check(!yipe::should_spawn_roaming_monster(
               player, location, 8, terrain_random),
          "roaming encounter terrain modifier raises the encounter threshold");
    player.maximum_health = 80;
    yipe::MsvcRandom critical_health_random(1);
    check(!yipe::should_spawn_roaming_monster(
               player, location, 2, critical_health_random),
          "critical health raises the roaming encounter threshold");
    player.maximum_health = 10;
    player.random_encounter_threshold = 125;
    yipe::MsvcRandom repellent_random(1);
    check(!yipe::should_spawn_roaming_monster(
               player, location, 2, repellent_random),
          "monster repellent prevents roaming encounter generation");
    check(yipe::roaming_monster_allowed(0, 4, false)
              && !yipe::roaming_monster_allowed(0, 5, false)
              && yipe::roaming_monster_allowed(1, 5, false)
              && !yipe::roaming_monster_allowed(1, 8, false)
              && yipe::roaming_monster_allowed(2, 8, false)
              && yipe::roaming_monster_allowed(7, 34, false)
              && !yipe::roaming_monster_allowed(7, 35, false)
              && !yipe::roaming_monster_allowed(1, 1, true),
          "roaming environment and alternate-map filters match both recovered tables");

    yipe::Map::Tiles tiles{};
    tiles.fill(2);
    const yipe::Map map(20, 1, tiles);
    yipe::MapVisibility visibility;
    yipe::initialize_map_visibility(visibility, map, {5, 5});
    check(visibility.contains({0, 0}) && visibility.contains({12, 11}),
          "surface maps begin fully revealed");
    player.random_encounter_threshold = 40;
    yipe::MsvcRandom spawn_random(1);
    const std::optional<yipe::RoamingMonster> spawned =
        yipe::spawn_roaming_monster(
            map, player, {5, 5}, {}, visibility, {}, spawn_random);
    check(spawned
              && spawned->type_id == 7
              && spawned->position == yipe::MapPosition{11, 7},
          "roaming monster generation preserves type and position random order");

    constexpr std::array<int, 5> locked_types{10, 15, 22, 27, 32};
    constexpr std::array<int, 5> expanded_types{9, 14, 23, 29, 31};
    for (int environment = 2; environment <= 6; ++environment) {
        const yipe::Map environment_map(20, environment, tiles);
        yipe::Player locked_player = player;
        yipe::MsvcRandom locked_random(1);
        const std::optional<yipe::RoamingMonster> locked =
            yipe::spawn_roaming_monster(
                environment_map, locked_player, {5, 5}, {}, visibility, {},
                locked_random);
        yipe::Player expanded_player = player;
        expanded_player.encountered_monster_types[
            static_cast<std::size_t>(environment + 34)] = true;
        yipe::MsvcRandom expanded_random(1);
        const std::optional<yipe::RoamingMonster> expanded =
            yipe::spawn_roaming_monster(
                environment_map, expanded_player, {5, 5}, {}, visibility, {},
                expanded_random);
        const std::size_t index = static_cast<std::size_t>(environment - 2);
        check(locked && expanded
                  && locked->type_id == locked_types[index]
                  && expanded->type_id == expanded_types[index],
              "encounter history expands each late roaming-monster pool");
    }

    yipe::MsvcRandom blocked_spawn_random(1);
    const std::optional<yipe::RoamingMonster> blocked_spawn =
        yipe::spawn_roaming_monster(
            map, player, {5, 5}, {}, visibility, {}, blocked_spawn_random,
            yipe::MapPosition{11, 7});
    check(blocked_spawn
              && blocked_spawn->position != yipe::MapPosition{11, 7},
          "roaming monster generation avoids the map-authored pursuer");

    yipe::MapEvents chest_events;
    chest_events.chests[0] = {11, 7};
    chest_events.chests[1] = {2, 2};
    yipe::Map chest_map(20, 1, tiles, chest_events);
    yipe::MsvcRandom chest_spawn_random(1);
    const auto chest_spawn = yipe::spawn_roaming_monster(
        chest_map, player, {5, 5}, {}, visibility, {}, chest_spawn_random);
    check(chest_spawn && chest_spawn->position != yipe::MapPosition{11, 7}
              && chest_spawn->position != yipe::MapPosition{2, 2},
          "roaming monsters cannot spawn on unopened chests");
    yipe::RoamingMonster chest_pursuit{7, {1, 1}};
    yipe::MsvcRandom chest_move_random(1);
    static_cast<void>(yipe::move_roaming_monster(
        chest_pursuit, chest_map, {4, 4}, {}, chest_move_random));
    check(chest_pursuit.position == yipe::MapPosition{1, 2},
          "an unopened chest diverts roaming pursuit");
    chest_map.remove_chest(1);
    chest_pursuit.position = {1, 1};
    yipe::MsvcRandom opened_chest_random(1);
    static_cast<void>(yipe::move_roaming_monster(
        chest_pursuit, chest_map, {4, 4}, {}, opened_chest_random));
    check(chest_pursuit.position == yipe::MapPosition{2, 2},
          "an opened chest releases its cell for roaming pursuit");

    const yipe::Map excluded_map(8, 1, tiles);
    yipe::MsvcRandom excluded_random(1);
    const std::uint32_t excluded_state = excluded_random.state();
    check(!yipe::spawn_roaming_monster(
               excluded_map, player, {5, 5}, {}, visibility, {},
               excluded_random)
              && excluded_random.state() == excluded_state,
          "maps excluded by the original generator do not consume spawn rolls");

    yipe::Map::Tiles dungeon_tiles{};
    dungeon_tiles.fill(0x22);
    dungeon_tiles[static_cast<std::size_t>(5 * yipe::Map::height + 5)] = 2;
    const yipe::Map dungeon(0x39, 1, dungeon_tiles);
    yipe::MapVisibility dungeon_visibility;
    yipe::initialize_map_visibility(dungeon_visibility, dungeon, {5, 5});
    check(dungeon_visibility.contains({4, 4})
              && dungeon_visibility.contains({6, 6})
              && !dungeon_visibility.contains({3, 3}),
          "underground visibility reveals the current room through opaque borders");

    yipe::RoamingMonster pursuing{7, {1, 1}};
    yipe::MsvcRandom movement_random(1);
    check(yipe::move_roaming_monster(
              pursuing, map, {4, 4}, {}, movement_random)
                  == yipe::RoamingMonsterMove::moved
              && pursuing.position == yipe::MapPosition{2, 2},
          "roaming monsters pursue the player diagonally");

    yipe::RoamingMonster adjacent{7, {1, 1}};
    yipe::MsvcRandom collision_random(1);
    check(yipe::move_roaming_monster(
              adjacent, map, {2, 2}, {}, collision_random)
                  == yipe::RoamingMonsterMove::encountered_player
              && adjacent.position == yipe::MapPosition{1, 1},
          "roaming pursuit reports combat without moving onto the player");

    yipe::MonsterPlacement blocker;
    blocker.position = {2, 2};
    std::vector<yipe::MonsterPlacement> blockers{blocker};
    yipe::RoamingMonster diverted{7, {1, 1}};
    yipe::MsvcRandom diverted_random(1);
    check(yipe::move_roaming_monster(
              diverted, map, {4, 4}, blockers, diverted_random)
                  == yipe::RoamingMonsterMove::moved
              && diverted.position == yipe::MapPosition{1, 2},
          "blocked roaming pursuit falls back to a random adjacent step");

    player.random_encounter_threshold = 125;
    std::optional<yipe::RoamingMonster> existing =
        yipe::RoamingMonster{7, {1, 1}};
    yipe::MsvcRandom update_random(1);
    yipe::MsvcRandom update_reference(1);
    static_cast<void>(update_reference.next());
    static_cast<void>(update_reference.next());
    check(yipe::update_roaming_monster(
              existing, map, player, location, 2, {}, visibility, {},
              update_random)
                  == yipe::RoamingMonsterUpdate::moved
              && existing->position == yipe::MapPosition{2, 2}
              && update_random.state() == update_reference.state(),
          "existing roaming monsters consume the encounter roll before pursuit");

    const std::optional<yipe::Map> authored_map = yipe::original_map(0x10);
    check(authored_map.has_value(),
          "level-dependent pursuer map remains in the static catalog");
    const std::optional<yipe::PursuingMonster> low_level_pursuer =
        yipe::pursuing_monster_for_map(*authored_map, 5);
    const std::optional<yipe::PursuingMonster> high_level_pursuer =
        yipe::pursuing_monster_for_map(*authored_map, 6);
    check(low_level_pursuer && low_level_pursuer->type_id == 1
              && low_level_pursuer->position == yipe::MapPosition{5, 4}
              && low_level_pursuer->origin == yipe::MapPosition{5, 4}
              && high_level_pursuer && high_level_pursuer->type_id == 6,
          "map loads create the recovered level-dependent pursuer");

    yipe::PursuingMonster authored{19, {5, 4}, {5, 4}};
    yipe::MsvcRandom authored_random(1);
    yipe::MsvcRandom authored_reference(1);
    static_cast<void>(authored_reference.next());
    static_cast<void>(authored_reference.next());
    check(yipe::move_pursuing_monster(
              authored, map, {9, 8}, {}, std::nullopt, visibility,
              authored_random)
                  == yipe::RoamingMonsterMove::moved
              && authored.position == yipe::MapPosition{5, 5}
              && authored_random.state() == authored_reference.state(),
          "visible map-authored pursuers preserve random-step order");

    yipe::PursuingMonster authored_collision{19, {1, 1}, {1, 1}};
    yipe::MsvcRandom authored_collision_random(100);
    check(yipe::move_pursuing_monster(
              authored_collision, map, {2, 2}, {}, std::nullopt, visibility,
              authored_collision_random)
                  == yipe::RoamingMonsterMove::encountered_player
              && authored_collision.position == yipe::MapPosition{1, 1},
          "map-authored pursuit starts combat without entering the player tile");

    yipe::MapVisibility hidden_visibility;
    yipe::PursuingMonster hidden{19, {1, 1}, {1, 1}};
    yipe::MsvcRandom hidden_random(1);
    const std::uint32_t hidden_state = hidden_random.state();
    check(yipe::move_pursuing_monster(
              hidden, map, {2, 2}, {}, std::nullopt, hidden_visibility,
              hidden_random)
                  == yipe::RoamingMonsterMove::stayed
              && hidden.position == yipe::MapPosition{1, 1}
              && hidden_random.state() == hidden_state,
          "unrevealed map-authored pursuers stay put without consuming randomness");

    yipe::PursuingMonster blocked_pursuer{19, {5, 4}, {5, 4}};
    yipe::MsvcRandom blocked_pursuer_random(100);
    check(yipe::move_pursuing_monster(
              blocked_pursuer, map, {9, 8}, {},
              yipe::RoamingMonster{7, {6, 5}}, visibility,
              blocked_pursuer_random)
                  != yipe::RoamingMonsterMove::encountered_player
              && blocked_pursuer.position != yipe::MapPosition{6, 5},
          "map-authored pursuers avoid the transient roaming monster");
}

void test_monster_definition_catalog() {
    const auto bunny = yipe::original_monster_definition(1);
    const auto beetle = yipe::original_monster_definition(2);
    const auto gatekeeper = yipe::original_monster_definition(41);
    check(bunny && bunny->name == "Small Bunny" && bunny->bitmap_resource_id == 174
              && bunny->health_base == 10 && bunny->rank_and_health_range == 1
              && bunny->damage_minimum == 1 && bunny->damage_maximum == 3,
          "monster catalog starts with the Small Bunny record");
    check(beetle && beetle->name == "Giant Beetle" && beetle->string_resource_id == 231
              && beetle->health_base == 14 && beetle->attack_base == 8
              && beetle->defense_base == 3 && beetle->special_rating == 0
              && beetle->damage_minimum == 1 && beetle->damage_maximum == 3,
          "monster catalog preserves the Giant Beetle record");
    check(gatekeeper && gatekeeper->name == "Gatekeeper"
              && gatekeeper->bitmap_resource_id == 214
              && gatekeeper->health_base == 315 && gatekeeper->special_rating == 50
              && gatekeeper->damage_minimum == 15 && gatekeeper->damage_maximum == 22,
          "monster catalog ends with the Gatekeeper record");
    check(!yipe::original_monster_definition(0)
              && !yipe::original_monster_definition(42),
          "monster catalog rejects out-of-range type IDs");

    yipe::MsvcRandom random(1);
    const yipe::Monster monster = yipe::start_single_monster_encounter(41, random);
    check(monster.name == "Gatekeeper" && monster.health == 332 && monster.rank == 25
              && monster.attack_rating == 26 && monster.defense_rating == 25
              && monster.special_rating == 50,
          "generic setup applies the recovered table and random sequence");

    yipe::Player high_rank_player;
    high_rank_player.level = 10;
    high_rank_player.difficulty = 3;
    const yipe::MonsterPlacement high_rank_placement =
        yipe::original_monsters_for_map(0x4c, high_rank_player.difficulty,
                                        high_rank_player.level)[0];
    yipe::MsvcRandom high_rank_random(1);
    const yipe::CombatEncounter high_rank_encounter = yipe::start_monster_encounter(
        high_rank_placement, high_rank_player, high_rank_random, 1);
    check(high_rank_encounter.monsters[0].type_id == 35
              && high_rank_encounter.monsters[0].health == 111
              && high_rank_encounter.monsters[1].type_id == 8
              && high_rank_encounter.monsters[2].type_id == 8
              && high_rank_encounter.map_environment == 1
              && high_rank_random.state() == 2179684817u,
          "high-rank placement setup applies companion overrides and level scaling");
    check(high_rank_player.encountered_monster_types[35]
              && high_rank_player.encountered_monster_types[8],
          "combat setup records every encountered monster type");

    yipe::MsvcRandom empowered_random(1);
    const yipe::CombatEncounter empowered = yipe::start_monster_encounter(
        41, 1, empowered_random, yipe::EncounterSetup{10, 3, 0, 0, 1});
    check(empowered.monsters[0].type_id == 41
              && empowered.monsters[0].health == 1404
              && empowered.monsters[0].gatekeeper_empowered
              && empowered.monsters[1].type_id == 40
              && empowered.monsters[1].health == 1183
              && empowered.monsters[2].type_id == 37
              && empowered.monsters[2].health == 255
              && empowered_random.state() == 3750785579u,
          "empowered Gatekeeper setup scales health and creates difficulty companions");

    yipe::Player reference_defender = high_rank_player;
    reference_defender.health = 10000;
    reference_defender.maximum_health = 10000;
    yipe::Player empowered_defender = reference_defender;
    yipe::Monster ordinary_gatekeeper = empowered.monsters[0];
    ordinary_gatekeeper.gatekeeper_empowered = false;
    yipe::MsvcRandom ordinary_attack_random(7);
    yipe::MsvcRandom empowered_attack_random(7);
    const yipe::CombatTurn ordinary_attack = yipe::resolve_monster_turn(
        reference_defender, ordinary_gatekeeper, ordinary_attack_random);
    const yipe::CombatTurn empowered_attack = yipe::resolve_monster_turn(
        empowered_defender, empowered.monsters[0], empowered_attack_random);
    check(ordinary_attack.monster_damage == 55
              && empowered_attack.monster_damage == 47
              && ordinary_attack_random.state() == 3788699098u
              && empowered_attack_random.state() == 2936718116u,
          "empowered Gatekeeper monster damage preserves its bonus rolls");

    yipe::Player ordinary_attacker;
    ordinary_attacker.health = 10000;
    ordinary_attacker.maximum_health = 10000;
    ordinary_attacker.level = 40;
    ordinary_attacker.strength = 100;
    ordinary_attacker.weapon_rank = 42;
    ordinary_attacker.difficulty = 3;
    yipe::Player special_attacker = ordinary_attacker;
    ordinary_gatekeeper.health = 10000;
    ordinary_gatekeeper.maximum_health = 10000;
    yipe::Monster special_gatekeeper = ordinary_gatekeeper;
    special_gatekeeper.gatekeeper_empowered = true;
    yipe::MsvcRandom ordinary_player_attack_random(7);
    yipe::MsvcRandom special_player_attack_random(7);
    const yipe::CombatTurn ordinary_player_attack = yipe::resolve_attack_turn(
        ordinary_attacker, ordinary_gatekeeper, ordinary_player_attack_random);
    const yipe::CombatTurn special_player_attack = yipe::resolve_attack_turn(
        special_attacker, special_gatekeeper, special_player_attack_random);
    check(ordinary_player_attack.player_damage == 30
              && special_player_attack.player_damage == 26
              && ordinary_player_attack.monster_damage == 42
              && special_player_attack.monster_damage == 33
              && ordinary_player_attack_random.state() == 3078176496u
              && special_player_attack_random.state() == 729675781u,
          "empowered Gatekeeper defense preserves its attack and counterattack rolls");
}

void test_weapon_damage_catalog() {
    for (int weapon_rank = 1; weapon_rank <= 42; ++weapon_rank) {
        const auto damage = yipe::original_weapon_damage_range(weapon_rank);
        check(damage.first > 0 && damage.first <= damage.second,
              "weapon rank " + std::to_string(weapon_rank)
                  + " has a valid recovered damage range");
    }

    check(yipe::original_weapon_damage_range(1) == std::pair{1, 2}
              && yipe::original_weapon_damage_range(2) == std::pair{1, 3},
          "weapon catalog preserves its first two ranges");
    check(yipe::original_weapon_damage_range(21) == std::pair{7, 12}
              && yipe::original_weapon_damage_range(42) == std::pair{18, 29},
          "weapon catalog preserves middle and final ranges");
    check(yipe::original_weapon_damage_range(0) == std::pair{0, 1}
              && yipe::original_weapon_damage_range(43) == std::pair{0, 1},
          "weapon catalog preserves the original invalid-rank fallback");
}

void test_equipment_combat_ratings() {
    yipe::Player player;
    check(yipe::player_attack_equipment_rating(player) == 2
              && yipe::player_defense_equipment_rating(player) == 3,
          "default equipment produces the recovered attack and defense ratings");

    player.weapon_rank = 10;
    player.armor_rank = 11;
    player.boots_rank = 6;
    player.gloves_rank = 4;
    player.helmet_rank = 7;
    player.ring_rank = 5;
    player.necklace_rank = 6;
    check(yipe::player_attack_equipment_rating(player) == 17,
          "helmet, gloves, and necklace apply every recovered attack bonus");
    check(yipe::player_defense_equipment_rating(player) == 41,
          "all equipment ranks and the special boots/ring tiers contribute to defense");

    player.special_ability = yipe::SpecialAbility::invisibility;
    player.special_ability_duration = 20;
    check(yipe::player_attack_equipment_rating(player) == 17,
          "Invisibility remains separate from the deterministic equipment rating");

    player = yipe::Player{};
    player.gloves_rank = 0xffff;
    player.helmet_rank = 0x8000;
    player.ring_rank = 0x7fff;
    check(yipe::player_attack_equipment_rating(player) == 2
              && yipe::player_defense_equipment_rating(player) == 0,
          "equipment totals sign-extend accessory shorts as in the original helpers");
    player.gloves_rank = 6;
    check(yipe::player_attack_equipment_rating(player) == 3,
          "combat preserves its greater-than-three glove rule beyond the named tiers");
}

void test_king_quest_dialog() {
    yipe::Player player;
    player.name = "Ari";
    yipe::MsvcRandom random(1);
    const yipe::KingConversation introduction =
        yipe::talk_to_king(player, random, 13);
    check(introduction.greeting_resource_id == 721
              && introduction.message_resource_id == 731
              && introduction.footer_resource_id == 766
              && introduction.greeting == "Welcome, Ari"
              && introduction.message.starts_with("Thank you for coming")
              && player.quest_flags[0],
          "the first King conversation forces the original introduction");

    const yipe::KingConversation princess_request =
        yipe::talk_to_king(player, random, 13);
    check(princess_request.greeting_resource_id == 727
              && princess_request.message_resource_id == 732
              && princess_request.footer_resource_id == 767
              && player.quest_flags[1],
          "the second King conversation introduces the missing princesses");

    yipe::Player stamp_reward;
    stamp_reward.quest_flags[0] = true;
    stamp_reward.quest_flags[1] = true;
    stamp_reward.stamps_found = 1;
    yipe::MsvcRandom stamp_random(1);
    const yipe::KingConversation first_stamp =
        yipe::talk_to_king(stamp_reward, stamp_random, 8);
    check(first_stamp.message_resource_id == 733
              && first_stamp.footer_resource_id == 768
              && stamp_reward.gold == 500 && stamp_reward.quest_flags[2],
          "the King pays the one-time first-stamp reward");

    yipe::Player spray_hint;
    spray_hint.quest_flags.fill(true);
    spray_hint.quest_flags[7] = false;
    spray_hint.stamps_found = 4;
    yipe::MsvcRandom spray_random(1);
    const yipe::KingConversation missing_spray =
        yipe::talk_to_king(spray_hint, spray_random, 12);
    check(missing_spray.message_resource_id == 747 && !spray_hint.quest_flags[7],
          "four-stamp dialog waits for the waterproof shoe spray");
    spray_hint.has_waterproof_shoe_spray = true;
    const yipe::KingConversation found_spray =
        yipe::talk_to_king(spray_hint, spray_random, 12);
    check(found_spray.message_resource_id == 746 && spray_hint.quest_flags[7],
          "the King recognizes the waterproof shoe spray once");

    yipe::Player princess_reward;
    princess_reward.quest_flags.fill(true);
    princess_reward.quest_flags[8] = false;
    princess_reward.princesses_found = 10;
    yipe::MsvcRandom princess_random(1);
    const yipe::KingConversation lucky_foot =
        yipe::talk_to_king(princess_reward, princess_random, 12);
    check(lucky_foot.message_resource_id == 739
              && lucky_foot.footer_resource_id == 774
              && princess_reward.has_lucky_rabbit_foot
              && princess_reward.quest_flags[8],
          "ten rescued princesses award the Lucky Rabbit Foot once");

    yipe::Player gatekeeper_reward;
    gatekeeper_reward.quest_flags.fill(true);
    gatekeeper_reward.quest_flags[10] = false;
    gatekeeper_reward.stamps_found = 6;
    gatekeeper_reward.princesses_found = 19;
    gatekeeper_reward.gatekeeper_state = 2;
    gatekeeper_reward.weapon_rank = 37;
    yipe::MsvcRandom gatekeeper_random(1);
    const yipe::KingConversation defeated_gatekeeper =
        yipe::talk_to_king(gatekeeper_reward, gatekeeper_random, 12);
    check(defeated_gatekeeper.message_resource_id == 750
              && gatekeeper_reward.quest_score == 1500
              && gatekeeper_reward.weapon_rank == 38
              && gatekeeper_reward.quest_flags[10],
          "the first Gatekeeper victory grants the recovered score and weapon reward");

    gatekeeper_reward.quest_flags[11] = false;
    gatekeeper_reward.gatekeeper_state = 3;
    const yipe::KingConversation completion =
        yipe::talk_to_king(gatekeeper_reward, gatekeeper_random, 20);
    check(completion.message_resource_id == 751
              && completion.footer_resource_id == 786
              && gatekeeper_reward.weapon_rank == 38
              && gatekeeper_reward.armor_rank == 30
              && gatekeeper_reward.quest_flags[11],
          "the final King conversation grants the fixed end-game equipment");
}

void test_chest_rewards() {
    int count = 0;
    for (int id = 1; id <= 128; ++id) {
        for (const auto position : yipe::original_chests_for_map(id)) {
            if (position.x >= 0) ++count;
        }
    }
    check(count == 145, "chest catalog retains all four slots across 75 original map IDs");
    check(yipe::original_chests_for_map(0x63)[1] == yipe::MapPosition{9, 1},
          "the independently identified spray chest is slot 1 at (9,1) on map 0x63");
    for (std::size_t slot = 0; slot < 4; ++slot) {
        yipe::Player player;
        auto map = *yipe::original_map(0x63);
        yipe::MsvcRandom random(1);
        const auto position = map.events().chests[slot];
        const auto reward = yipe::open_chest(player, map, position, slot, random);
        constexpr std::array xp{28, 29, 30, 31};
        constexpr std::array gold{55, 72, 103, 115};
        check(reward.opened && reward.experience == xp[slot] && reward.gold == gold[slot]
                  && player.experience == xp[slot] && player.gold == gold[slot],
              "seed-1 chest XP/gold matches the four original rolls for slot " + std::to_string(slot));
        yipe::MsvcRandom reference(1);
        for (int i = 0; i < (slot == 1 ? 4 : 6); ++i) static_cast<void>(reference.next());
        check(random.state() == reference.state(), "spray skips random loot; ordinary chest uses two loot rolls");
        check(slot == 1 ? reward.loot.waterproof_shoe_spray && player.has_waterproof_shoe_spray
                       : slot == 0 ? reward.loot.item_id == 1 && player.inventory[0] == 1
                                   : reward.loot.equipment_upgrade == yipe::EquipmentUpgrade::gloves
                                         && player.gloves_rank == 1,
              "each original chest kind selects its recovered reward branch");
        const auto before_repeat = random.state();
        check(!yipe::open_chest(player, map, position, slot, random).opened
                  && random.state() == before_repeat && player.gold == gold[slot],
              "opening the same chest twice cannot repeat rewards or consume randomness");
        auto reentered = *yipe::original_map(0x63);
        yipe::initialize_map_actions(player, reentered, {9, 2});
        check(reentered.events().chests[slot] == yipe::MapPosition{-1, -1},
              "map re-entry removes the opened chest by its original slot");
    }

    yipe::Player spray;
    auto spray_map = *yipe::original_map(0x63);
    yipe::MapPosition position{9, 2};
    yipe::MsvcRandom random(1);
    check(yipe::try_move(spray_map, position, 0, -1).position_changed
              && yipe::open_chest(spray, spray_map, position, 1, random).loot.waterproof_shoe_spray,
          "ordinary movement reaches and awards the real spray chest without supplying its flag");
    auto stream = yipe::starting_map();
    for (int x = 1; x < yipe::Map::width; ++x) {
        for (int y = 0; y < yipe::Map::height; ++y) {
            if (stream.tile({x, y}) != 0x58 && stream.tile({x, y}) != 0x59) continue;
            yipe::MapPosition from{x - 1, y};
            check(!yipe::try_move(stream, from, 1, 0).position_changed
                      && yipe::try_move(stream, from, 1, 0,
                                       {spray.has_waterproof_shoe_spray}).position_changed,
                  "acquired spray unlocks a real stream tile");
        }
    }

    yipe::Player full;
    full.inventory.fill(2);
    auto full_map = *yipe::original_map(0x63);
    yipe::MsvcRandom full_random(1);
    const auto full_reward = yipe::open_chest(full, full_map, {1, 3}, 0, full_random);
    yipe::MsvcRandom full_reference(1);
    for (int i = 0; i < 5; ++i) static_cast<void>(full_reference.next());
    check(full_reward.opened && full_reward.loot.inventory_full && full.gold == 55
              && full_random.state() == full_reference.state(),
          "full inventory retains chest gold/XP, consumes the chest, and skips the item roll");
    full.inventory[13] = 0;
    yipe::MsvcRandom reserved_random(1);
    check(yipe::roll_treasure(full, 999, 1, reserved_random).inventory_full,
          "reserved inventory slots cannot accept chest consumables");

    struct ConsumableCase {
        int kind;
        std::uint32_t seed;
        std::uint16_t item;
        std::uint16_t item_at_limits;
    };
    constexpr std::array<ConsumableCase, 26> consumables{{
        {1, 3, 1, 2}, {1, 1, 1, 2}, {1, 6, 1, 2}, {1, 13, 1, 2},
        {1, 4, 2, 2}, {1, 2, 2, 2}, {1, 7, 2, 2},
        {2, 3, 2, 2}, {2, 1, 2, 2}, {2, 6, 2, 2}, {2, 13, 5, 5},
        {2, 4, 5, 5}, {2, 2, 4, 4}, {2, 7, 4, 4},
        {3, 8, 6, 2}, {3, 5, 7, 2}, {3, 2, 8, 2},
        {3, 10, 6, 2}, {3, 7, 7, 2}, {3, 1, 8, 2},
        {4, 8, 6, 2}, {4, 5, 7, 2}, {4, 2, 8, 2},
        {4, 10, 9, 9}, {4, 7, 10, 10}, {4, 1, 3, 3},
    }};
    for (const auto& test : consumables) {
        for (bool at_limits : {false, true}) {
            yipe::Player recipient;
            recipient.level = at_limits ? 11 : 10;
            recipient.strength = recipient.dexterity = recipient.intelligence = at_limits ? 50 : 49;
            recipient.inventory[0] = 10;
            yipe::MsvcRandom item_random(test.seed);
            yipe::MsvcRandom item_reference(test.seed);
            static_cast<void>(item_reference.next());
            static_cast<void>(item_reference.next());
            const auto loot = yipe::roll_treasure(recipient, 999, test.kind, item_random);
            const auto item = at_limits ? test.item_at_limits : test.item;
            check(loot.item_id == item && recipient.inventory[0] == 10
                      && recipient.inventory[1] == item && item_random.state() == item_reference.state(),
                  "chest consumable selection and level/stat boundary for kind "
                      + std::to_string(test.kind) + " seed " + std::to_string(test.seed));
        }
    }

    yipe::Player capped;
    capped.helmet_rank = 6; capped.weapon_rank = 29; capped.armor_rank = 26;
    capped.ring_rank = 5; capped.necklace_rank = 6; capped.gloves_rank = 5; capped.boots_rank = 5;
    yipe::MsvcRandom capped_random(1);
    const auto capped_loot = yipe::roll_treasure(capped, -1, 4, capped_random);
    check(capped_loot.equipment_upgrade == yipe::EquipmentUpgrade::none && capped_loot.item_id != 0,
          "a capped equipment roll falls through to chest consumables");

    yipe::Player forced;
    forced.level = 15;
    yipe::MsvcRandom forced_random(1);
    const auto forced_loot = yipe::roll_treasure(forced, 99, 1, forced_random);
    yipe::MsvcRandom forced_reference(1);
    static_cast<void>(forced_reference.next());
    check(forced_loot.equipment_upgrade == yipe::EquipmentUpgrade::weapon
              && forced.weapon_rank == 3 && forced.equipment_upgrade_count == 1
              && forced.quest_score == 10 && forced_random.state() == forced_reference.state(),
          "forced weapon upgrade retains score and skips selection/item rolls");
    forced.current_map_id = 0x63;
    forced.equipment_upgrade_count = 0;
    const auto before_spray = forced_random.state();
    check(yipe::roll_treasure(forced, 78, 2, forced_random).waterproof_shoe_spray
              && forced.equipment_upgrade_count == 1 && forced.weapon_rank == 3
              && forced_random.state() == before_spray,
          "spray preserves the original forced-upgrade bookkeeping without granting equipment");

    forced.current_map_id = 1;
    forced.equipment_upgrade_count = 0;
    forced.loot_roll_penalty = 100;
    yipe::MsvcRandom penalized_random(1);
    const auto penalized = yipe::roll_treasure(forced, 99, 1, penalized_random);
    check(penalized.equipment_upgrade == yipe::EquipmentUpgrade::none && penalized.item_id == 0
              && forced.equipment_upgrade_count == 1 && penalized_random.state() == forced_reference.state(),
          "a failed forced threshold retains its bookkeeping and skips consumable randomness");

    yipe::MsvcRandom generated_random(1);
    auto generated = yipe::generate_original_map(0x78, {1, 1}, generated_random);
    check(generated.events().chests[2] == yipe::MapPosition{3, 5}
              && generated.events().chests[3] == yipe::MapPosition{10, 10},
          "runtime-generated map retains both original chest slots");
    generated.restore_tile({3, 5}, 0x24);
    yipe::initialize_map_actions(spray, generated, {1, 1});
    check(generated.events().chests[2] == yipe::MapPosition{-1, -1},
          "map initialization suppresses a chest on generated fire terrain, as in 0x0041ce30");

    const auto path = std::filesystem::temp_directory_path() / "yipe-chest-state-test.y3r";
    yipe::GameState state;
    state.player = spray;
    state.player.opened_chests[127] = 15;
    yipe::save_game_atomic(path, state);
    check(yipe::load_game(path).player.opened_chests == state.player.opened_chests,
          "native chest flags retain early and last-map slots");
    state.player.opened_chests[0] = 16;
    bool rejected = false;
    try { yipe::save_game_atomic(path, state); }
    catch (const std::runtime_error&) { rejected = true; }
    check(rejected, "native chest flags reject bits outside the four original slots");
    remove_save_extension(path, 4, 18);
    std::ifstream input(path, std::ios::binary);
    std::vector<std::uint8_t> invalid_flags{std::istreambuf_iterator<char>(input),
                                           std::istreambuf_iterator<char>()};
    input.close();
    invalid_flags.back() = 16;
    write_u32(invalid_flags, 10, test_crc32(invalid_flags, 14));
    write_bytes(path, invalid_flags);
    rejected = false;
    try { static_cast<void>(yipe::load_game(path)); }
    catch (const std::runtime_error&) { rejected = true; }
    check(rejected, "version 18 rejects invalid chest bits even with a valid checksum");
    state.player.opened_chests[0] = 0;
    yipe::save_game_atomic(path, state);
    remove_save_extension(path, 5, 18);
    rejected = false;
    try { static_cast<void>(yipe::load_game(path)); }
    catch (const std::runtime_error&) { rejected = true; }
    check(rejected, "version 18 requires all 128 chest-state bytes");
    std::filesystem::remove(path);
}

void test_quest_progression_events() {
    yipe::Player gold_player;
    yipe::MsvcRandom gold_random(1);
    const yipe::PrincessReward gold = yipe::rescue_princess(
        gold_player, 13, yipe::PrincessGift::gold, gold_random);
    check(gold.rescued && gold.message_resource_id == 1100 && gold.amount == 347
              && gold.message == "The Princess gives you 347 gold pieces!"
              && gold_player.gold == 347 && gold_player.princesses_found == 1
              && gold_player.inventory_values[13] == 1
              && gold_random.state() == 0xc823f683u,
          "gold rescue reward preserves its two seeded rolls and one-time flag");
    const std::uint32_t after_gold = gold_random.state();
    check(!yipe::rescue_princess(gold_player, 13, yipe::PrincessGift::gold,
                                 gold_random).rescued
              && gold_player.gold == 347 && gold_random.state() == after_gold,
          "an already rescued princess cannot grant another reward");

    yipe::Player experience_player;
    yipe::MsvcRandom experience_random(1);
    const yipe::PrincessReward experience = yipe::rescue_princess(
        experience_player, 15, yipe::PrincessGift::experience, experience_random);
    check(experience.message_resource_id == 1101 && experience.amount == 145
              && experience_player.experience == 145
              && experience_random.state() == 0x29e2c0u,
          "experience rescue reward preserves its one seeded roll");

    yipe::Player kissed_player;
    kissed_player.health = 1;
    kissed_player.spell_points = 2;
    kissed_player.ability_points = 3;
    kissed_player.maximum_spell_points = 9;
    yipe::MsvcRandom kiss_random(1);
    const yipe::PrincessReward kiss = yipe::rescue_princess(
        kissed_player, 2, yipe::PrincessGift::kiss, kiss_random);
    check(kiss.message_resource_id == 1102
              && kissed_player.health == kissed_player.maximum_health
              && kissed_player.spell_points == 9
              && kissed_player.ability_points == kissed_player.maximum_ability_points
              && kiss_random.state() == 1,
          "kiss reward restores all three pools without consuming randomness");

    yipe::Player clue_player;
    yipe::MsvcRandom clue_random(5);
    const yipe::PrincessReward clue = yipe::rescue_princess(
        clue_player, 8, yipe::PrincessGift::mystery, clue_random);
    check(clue.message_resource_id == 1106 && clue_player.princess_mystery_uses == 2
              && !clue_player.has_rainbow_key,
          "mystery roll four gives the clue and advances its counter twice");

    yipe::Player ring_player;
    yipe::MsvcRandom ring_random(2);
    const yipe::PrincessReward ring = yipe::rescue_princess(
        ring_player, 11, yipe::PrincessGift::mystery, ring_random);
    check(ring.message_resource_id == 1107 && ring_player.ring_rank == 1
              && ring_player.princess_mystery_uses == 1,
          "mystery roll five grants a ring below the recovered rank cap");

    yipe::Player key_player;
    key_player.princess_mystery_uses = 100;
    yipe::MsvcRandom key_random(1);
    const yipe::PrincessReward key = yipe::rescue_princess(
        key_player, 16, yipe::PrincessGift::mystery, key_random);
    check(key.message_resource_id == 1108 && key_player.has_rainbow_key
              && key_player.princess_mystery_uses == 101,
          "a high mystery threshold selects the recovered rainbow-key case");

    const auto win_battle = [](yipe::Player& player, int type_id, int environment) {
        player.health = 10000;
        player.maximum_health = 10000;
        player.level = 40;
        player.strength = 100;
        player.weapon_rank = 42;
        yipe::MsvcRandom random(1);
        yipe::CombatEncounter encounter = yipe::start_monster_encounter(
            type_id, 1, random,
            yipe::EncounterSetup{player.level, player.difficulty, 0, 0,
                                 0, environment});
        encounter.monsters[0].health = 1;
        yipe::CombatTurn turn;
        do {
            turn = yipe::resolve_attack_turn(player, encounter, random);
        } while (turn.outcome == yipe::CombatOutcome::ongoing);
        return turn.outcome;
    };

    yipe::Player stamp_player;
    check(win_battle(stamp_player, 35, 1) == yipe::CombatOutcome::victory
              && stamp_player.stamps_found == 1,
          "the boss matching environment plus 34 grants one stamp");
    yipe::Player wrong_environment;
    static_cast<void>(win_battle(wrong_environment, 35, 2));
    check(wrong_environment.stamps_found == 0,
          "a boss outside its matching environment does not grant a stamp");

    yipe::Player first_gatekeeper;
    static_cast<void>(win_battle(first_gatekeeper, 40, 6));
    check(first_gatekeeper.stamps_found == 1 && first_gatekeeper.gatekeeper_state == 1,
          "the environment-six boss grants the final stamp and first Gatekeeper state");

    yipe::Player final_gatekeeper;
    final_gatekeeper.gatekeeper_state = 1;
    static_cast<void>(win_battle(final_gatekeeper, 41, 0));
    check(final_gatekeeper.has_underworld_key && final_gatekeeper.gatekeeper_state == 2
              && final_gatekeeper.stamps_found == 0,
          "the first Gatekeeper victory awards the key before advancing state without a stamp");

    for (bool already_keyed : {false, true}) {
        yipe::Player early;
        early.level = 15;
        early.special_ability = yipe::SpecialAbility::backstab;
        early.has_underworld_key = already_keyed;
        yipe::CombatEncounter encounter;
        encounter.monsters[0] = {41, "Gatekeeper", 214, 1, 1, 0, 1, 1, 0, false};
        yipe::MsvcRandom random(1);
        const auto reward = yipe::resolve_special_ability_turn(early, encounter, random);
        check(reward.outcome == yipe::CombatOutcome::victory && early.has_underworld_key
                  && early.gatekeeper_state == 0 && reward.reward.underworld_key == !already_keyed
                  && early.weapon_rank == (already_keyed ? 3 : 2)
                  && early.equipment_upgrade_count == (already_keyed ? 1 : 0),
              "early Gatekeeper win grants its key once, skips first-win loot, and leaves later quest state locked");
    }

    yipe::Player finale;
    finale.gatekeeper_state = 2;
    std::fill(finale.inventory_values.begin() + 1,
              finale.inventory_values.begin() + 21, 1);
    check(yipe::complete_gatekeeper_finale(finale) && finale.gatekeeper_state == 3,
          "returning with all 20 rescue slots completes the Gatekeeper finale");
    check(!yipe::complete_gatekeeper_finale(finale),
          "the finale transition can only occur once");

    finale.gatekeeper_state = 2;
    finale.inventory_values[20] = 0xffff;
    finale.inventory_values[29] = 0x8000;
    check(finale.rescued_princess_count() == 19
              && !yipe::complete_gatekeeper_finale(finale) && finale.gatekeeper_state == 2,
          "negative signed rescue slots cannot falsely complete the finale");
    finale.inventory_values[20] = 0x7fff;
    check(finale.rescued_princess_count() == 20
              && yipe::complete_gatekeeper_finale(finale) && finale.gatekeeper_state == 3,
          "a negative reserved slot cannot block a valid 20-princess finale");
    finale.gatekeeper_state = 2;
    finale.inventory_values[29] = 1;
    check(!yipe::complete_gatekeeper_finale(finale) && finale.gatekeeper_state == 2,
          "the finale requires exactly 20 positive rescue slots, not at least 20");
    finale.inventory_values[29] = 0;
    finale.gatekeeper_state = 1;
    check(!yipe::complete_gatekeeper_finale(finale) && finale.gatekeeper_state == 1,
          "the complete rescue count does not bypass Gatekeeper progression");
}

void test_death_and_hell_recovery() {
    yipe::Player player;
    player.health = 0;
    player.entered_hell_through_secret_passage = true;
    yipe::WorldLocation location{{2, 8}, 0, 1, 2, false, 0x5b};
    yipe::MsvcRandom random(1);
    const yipe::CombatDefeatResult first =
        yipe::resolve_combat_defeat(player, location, 2, random);
    check(first.message_resource_id == 701
              && first.message.starts_with("Your character has run out")
              && player.health == 1 && player.death_count == 1
              && !player.entered_hell_through_secret_passage
              && player.hell_gatekeeper_state
                  == yipe::HellGatekeeperState::waiting,
          "a first defeat selects its advice and initializes Hell recovery state");
    check(location.position == yipe::MapPosition{6, 10}
              && location.region_x == -4 && location.region_y == -1
              && location.map_layer == 0 && location.alternate_map_set
              && location.map_id == 0x75 && player.current_map_id == 0x75
              && player.alternate_map_set,
          "combat defeat relocates the player to the recovered Hell entrance");

    const yipe::CombatDefeatResult second =
        yipe::resolve_combat_defeat(player, location, 8, random);
    check(second.message_resource_id == 702 && player.death_count == 2
              && player.hell_gatekeeper_state
                  == yipe::HellGatekeeperState::waiting,
          "a second defeat selects its fixed advice without resetting the Gatekeeper");

    yipe::Player experienced;
    experienced.level = 21;
    yipe::WorldLocation experienced_location;
    yipe::MsvcRandom experienced_random(1);
    const yipe::CombatDefeatResult experienced_first =
        yipe::resolve_combat_defeat(
            experienced, experienced_location, 20, experienced_random);
    check(experienced_first.message_resource_id == 714
              && experienced.death_count == 1,
          "a high-level first defeat selects the recovered encouragement message");

    yipe::Player gatekeeper_victim;
    gatekeeper_victim.hell_gatekeeper_state =
        yipe::HellGatekeeperState::waiting;
    yipe::WorldLocation gatekeeper_location;
    yipe::MsvcRandom gatekeeper_random(1);
    const yipe::CombatDefeatResult gatekeeper_defeat =
        yipe::resolve_combat_defeat(
            gatekeeper_victim, gatekeeper_location, 41, gatekeeper_random);
    check(gatekeeper_defeat.message_resource_id == 715
              && gatekeeper_victim.death_count == 1
              && gatekeeper_victim.hell_gatekeeper_state
                  == yipe::HellGatekeeperState::waiting,
          "a Gatekeeper defeat keeps the player trapped with its special message");

    yipe::Player easy;
    easy.difficulty = 1;
    easy.gold = 900;
    check(yipe::gatekeeper_recovery_fee(easy) == 100,
          "easy Gatekeeper fee charges one ninth of available gold");
    easy.gold = 2000;
    check(yipe::gatekeeper_recovery_fee(easy) == 150,
          "easy Gatekeeper fee applies its fixed cap");
    yipe::Player normal;
    normal.difficulty = 2;
    normal.level = 10;
    normal.gold = 700;
    check(yipe::gatekeeper_recovery_fee(normal) == 100,
          "normal Gatekeeper fee charges one seventh of available gold");
    normal.gold = 7000;
    check(yipe::gatekeeper_recovery_fee(normal) == 270,
          "normal Gatekeeper fee uses the recovered level cap");
    yipe::Player hard;
    hard.difficulty = 3;
    hard.level = 25;
    hard.gold = 1000;
    check(yipe::gatekeeper_recovery_fee(hard) == 200,
          "hard Gatekeeper fee charges one fifth of available gold");
    hard.gold = 10000;
    check(yipe::gatekeeper_recovery_fee(hard) == 400,
          "hard Gatekeeper fee uses the recovered level cap");
    easy.gold = 50;
    check(yipe::gatekeeper_recovery_fee(easy) == 10,
          "Gatekeeper recovery enforces its ten-gold minimum");
    easy.gold = 7;
    check(yipe::gatekeeper_recovery_fee(easy) == 7,
          "Gatekeeper recovery caps its fee to the player's available gold");

    normal.health = 1;
    normal.maximum_health = 83;
    const int paid = yipe::pay_gatekeeper_recovery(normal);
    check(paid == 270 && normal.gold == 6730 && normal.health == 83
              && normal.hell_gatekeeper_state
                  == yipe::HellGatekeeperState::paid,
          "paying the Gatekeeper deducts the fee and fully heals the player");
}

void test_recovered_spells() {
    const auto& spells = yipe::spell_definitions();
    check(spells.size() == 12 && spells[0].name == "Return to Surface"
              && spells[11].name == "Rabbit Replacement"
              && spells[0].usage == yipe::SpellUsage::non_combat
              && spells[1].usage == yipe::SpellUsage::either
              && spells[2].usage == yipe::SpellUsage::combat,
          "spell catalog preserves all names and usage restrictions");

    const yipe::Player wizard(yipe::CharacterType::wizard);
    const yipe::Player warrior(yipe::CharacterType::warrior);
    const yipe::Player rogue(yipe::CharacterType::rogue);
    yipe::Player random_specialty;
    random_specialty.specialty = 2;
    check(yipe::spell_point_cost(wizard, 1, yipe::SpellPower::low) == 6
              && yipe::spell_point_cost(warrior, 1, yipe::SpellPower::low) == 8
              && yipe::spell_point_cost(rogue, 1, yipe::SpellPower::low) == 8
              && yipe::spell_point_cost(random_specialty, 1, yipe::SpellPower::low) == 10
              && yipe::spell_point_cost(warrior, 1, yipe::SpellPower::high) == 16
              && yipe::spell_point_cost(warrior, 5, yipe::SpellPower::high) == 33,
          "spell costs follow the original specialty table and selective doubling");

    yipe::Player surface_caster;
    surface_caster.spell_points = 10;
    surface_caster.maximum_spell_points = 10;
    yipe::WorldLocation cave{{7, 4}, 0, 0, 1, false, 0x39};
    yipe::MsvcRandom surface_random(1);
    const yipe::ExplorationSpellResult surface = yipe::cast_exploration_spell(
        surface_caster, cave, 0, yipe::SpellPower::low, surface_random);
    check(surface.status == yipe::SpellCastStatus::cast && surface.location_changed
              && cave.position == yipe::MapPosition{7, 4} && cave.map_layer == 1
              && cave.map_id == 0x39 && surface_caster.spell_points == 5
              && surface_caster.pending_relocation == 1,
          "Return to Surface queues the recovered deferred relocation");
    surface_caster.surface_return_map_id = 0x11;
    check(yipe::apply_pending_relocation(surface_caster, cave)
                  == yipe::PendingRelocation::surface
              && cave.map_layer == 0 && cave.map_id == 0x11
              && surface_caster.pending_relocation == 0,
          "the deferred surface relocation restores its saved map");
    const yipe::ExplorationSpellResult already_on_surface =
        yipe::cast_exploration_spell(surface_caster, cave, 0,
                                     yipe::SpellPower::low, surface_random);
    check(already_on_surface.status == yipe::SpellCastStatus::unavailable
              && surface_caster.spell_points == 5,
          "Return to Surface is unavailable without charge on the surface");

    yipe::Player healer;
    healer.spells[1] = 1;
    healer.health = 1;
    healer.maximum_health = 100;
    healer.spell_points = 100;
    healer.maximum_spell_points = 100;
    yipe::WorldLocation outdoors;
    yipe::MsvcRandom heal_random(1);
    const yipe::ExplorationSpellResult small_heal = yipe::cast_exploration_spell(
        healer, outdoors, 1, yipe::SpellPower::low, heal_random);
    check(small_heal.status == yipe::SpellCastStatus::cast
              && small_heal.healing == 14 && healer.health == 15
              && healer.spell_points == 92,
          "Small Heal uses the recovered seeded formula and spell-point cost");

    yipe::Player repellent_caster;
    repellent_caster.spells[4] = 1;
    repellent_caster.level = 10;
    repellent_caster.intelligence = 10;
    repellent_caster.spell_points = 100;
    repellent_caster.maximum_spell_points = 100;
    yipe::MsvcRandom repellent_random(1);
    const yipe::ExplorationSpellResult repellent = yipe::cast_exploration_spell(
        repellent_caster, outdoors, 4, yipe::SpellPower::high,
        repellent_random);
    check(repellent.status == yipe::SpellCastStatus::cast
              && repellent.repellent_duration == 46
              && repellent_caster.random_encounter_threshold == 125
              && repellent_caster.saved_random_encounter_threshold == 85
              && repellent_caster.monster_repellent_steps == 46
              && repellent_caster.spell_points == 76,
          "Stinky Smell applies the recovered capped duration and status fields");

    yipe::Player full_healer;
    full_healer.spells[5] = 1;
    full_healer.health = 1;
    full_healer.maximum_health = 100;
    full_healer.spell_points = 100;
    full_healer.maximum_spell_points = 100;
    yipe::MsvcRandom full_heal_random(1);
    const yipe::ExplorationSpellResult full_heal = yipe::cast_exploration_spell(
        full_healer, outdoors, 5, yipe::SpellPower::high, full_heal_random);
    check(full_heal.healing == 99 && full_healer.health == 100
              && full_healer.spell_points == 67,
          "Full Heal restores all health without a high-power surcharge");

    yipe::Player town_caster;
    town_caster.spells[8] = 1;
    town_caster.spell_points = 100;
    town_caster.maximum_spell_points = 100;
    yipe::WorldLocation distant_surface{{12, 8}, 4, -3, 0, true, 0x75};
    yipe::MsvcRandom town_random(1);
    const yipe::ExplorationSpellResult town = yipe::cast_exploration_spell(
        town_caster, distant_surface, 8, yipe::SpellPower::low, town_random);
    check(town.location_changed && distant_surface.position == yipe::MapPosition{12, 8}
              && distant_surface.map_id == 0x75 && town_caster.spell_points == 85
              && town_caster.pending_relocation == 2,
          "Return to Yipe queues the recovered deferred relocation");
    check(yipe::apply_pending_relocation(town_caster, distant_surface)
                  == yipe::PendingRelocation::yipe
              && distant_surface.position == yipe::MapPosition{4, 5}
              && distant_surface.region_x == 0 && distant_surface.region_y == 0
              && distant_surface.map_id == 0x11
              && town_caster.pending_relocation == 0,
          "the deferred Yipe relocation restores the town position");

    yipe::Player super_healer;
    super_healer.spells[9] = 1;
    super_healer.health = 1;
    super_healer.maximum_health = 100;
    super_healer.spell_points = 100;
    super_healer.maximum_spell_points = 100;
    super_healer.ability_points = 2;
    super_healer.maximum_ability_points = 5;
    yipe::MsvcRandom super_heal_random(1);
    const yipe::ExplorationSpellResult super_heal = yipe::cast_exploration_spell(
        super_healer, outdoors, 9, yipe::SpellPower::low, super_heal_random);
    check(super_heal.healing == 26 && super_healer.health == 27
              && super_healer.ability_points == 3
              && super_healer.spell_points == 45,
          "Super Heal restores seeded health and one AP at low power");

    const auto single_target = [] {
        yipe::CombatEncounter encounter;
        encounter.monsters[0] =
            {2, "Giant Beetle", 175, 100, 100, 0, 1, 100, 0, false};
        return encounter;
    };
    const auto cast_attack = [&single_target](std::size_t spell, int expected_damage) {
        yipe::Player caster;
        caster.health = 1000;
        caster.maximum_health = 1000;
        caster.spell_points = 200;
        caster.maximum_spell_points = 200;
        caster.spells[spell] = 1;
        yipe::CombatEncounter encounter = single_target();
        yipe::MsvcRandom random(1);
        const yipe::CombatSpellResult result = yipe::resolve_spell_turn(
            caster, encounter, spell, yipe::SpellPower::low, random);
        check(result.status == yipe::SpellCastStatus::cast
                  && result.damage_by_slot[0] == expected_damage
                  && encounter.monsters[0].health == 100 - expected_damage
                  && result.turn.monster_damage_by_slot[0] > 0,
              "combat spell " + std::to_string(spell)
                  + " applies its seeded damage before the counterattack");
        return result;
    };
    static_cast<void>(cast_attack(2, 10));
    static_cast<void>(cast_attack(3, 30));
    static_cast<void>(cast_attack(6, 53));

    yipe::Player group_attacker;
    group_attacker.health = 1000;
    group_attacker.maximum_health = 1000;
    group_attacker.spell_points = 100;
    group_attacker.maximum_spell_points = 100;
    group_attacker.spells[2] = 1;
    yipe::CombatEncounter attack_group;
    for (yipe::Monster& monster : attack_group.monsters) {
        monster = {2, "Giant Beetle", 175, 100, 100, 0, 1, 100, 0, false};
    }
    yipe::MsvcRandom group_attack_random(1);
    const yipe::CombatSpellResult group_attack = yipe::resolve_spell_turn(
        group_attacker, attack_group, 2, yipe::SpellPower::high,
        group_attack_random);
    check(group_attack.damage_by_slot == std::array<int, 3>{12, 11, 12}
              && attack_group.monsters[0].health == 88
              && attack_group.monsters[1].health == 89
              && attack_group.monsters[2].health == 88
              && group_attacker.spell_points == 76,
          "high-power attack spells target all opponents in recovered slot order");

    yipe::Player restricted_caster;
    restricted_caster.spell_points = 100;
    restricted_caster.maximum_spell_points = 100;
    restricted_caster.spells[2] = 1;
    yipe::MsvcRandom restricted_random(1);
    const yipe::ExplorationSpellResult wrong_exploration_context =
        yipe::cast_exploration_spell(restricted_caster, outdoors, 2,
                                     yipe::SpellPower::low, restricted_random);
    yipe::CombatEncounter restricted_target = single_target();
    const yipe::CombatSpellResult wrong_combat_context = yipe::resolve_spell_turn(
        restricted_caster, restricted_target, 0, yipe::SpellPower::low,
        restricted_random);
    restricted_caster.spells[1] = 1;
    restricted_caster.spell_points = 7;
    const yipe::ExplorationSpellResult too_expensive = yipe::cast_exploration_spell(
        restricted_caster, outdoors, 1, yipe::SpellPower::low, restricted_random);
    check(wrong_exploration_context.status == yipe::SpellCastStatus::wrong_context
              && wrong_combat_context.status == yipe::SpellCastStatus::wrong_context
              && too_expensive.status
                  == yipe::SpellCastStatus::insufficient_spell_points
              && restricted_caster.spell_points == 7,
          "spell context and SP failures leave player resources unchanged");

    yipe::Player suction_caster;
    suction_caster.health = 500;
    suction_caster.maximum_health = 1000;
    suction_caster.spell_points = 100;
    suction_caster.maximum_spell_points = 100;
    suction_caster.spells[7] = 1;
    yipe::CombatEncounter suction_target = single_target();
    yipe::MsvcRandom suction_random(1);
    const yipe::CombatSpellResult suction = yipe::resolve_spell_turn(
        suction_caster, suction_target, 7, yipe::SpellPower::low,
        suction_random);
    check(suction.damage_by_slot[0] == 33 && suction.healing == 33
              && suction_target.monsters[0].health == 67,
          "Health Suction heals by the recovered seeded damage amount");

    yipe::Player shuffle_caster;
    shuffle_caster.health = 1000;
    shuffle_caster.maximum_health = 1000;
    shuffle_caster.spell_points = 100;
    shuffle_caster.maximum_spell_points = 100;
    shuffle_caster.spells[10] = 1;
    yipe::CombatEncounter lone_monster = single_target();
    yipe::MsvcRandom unavailable_random(1);
    const yipe::CombatSpellResult unavailable_shuffle = yipe::resolve_spell_turn(
        shuffle_caster, lone_monster, 10, yipe::SpellPower::low,
        unavailable_random);
    check(unavailable_shuffle.status == yipe::SpellCastStatus::unavailable
              && shuffle_caster.spell_points == 100,
          "HP Shuffle requires multiple living monsters and does not charge on failure");
    yipe::CombatEncounter group;
    group.monsters[0] = {2, "One", 175, 30, 30, 0, 1, 0, 0, false};
    group.monsters[1] = {2, "Two", 175, 40, 40, 0, 1, 0, 0, false};
    group.monsters[2] = {2, "Three", 175, 50, 50, 0, 1, 0, 0, false};
    yipe::MsvcRandom shuffle_random(1);
    const yipe::CombatSpellResult shuffle = yipe::resolve_spell_turn(
        shuffle_caster, group, 10, yipe::SpellPower::low, shuffle_random);
    check(shuffle.status == yipe::SpellCastStatus::cast
              && group.monsters[0].health == 46 && group.monsters[1].health == 47
              && group.monsters[2].health == 27
              && shuffle_caster.spell_points == 88,
          "HP Shuffle circulates seeded health portions across three monsters");

    yipe::Player rabbit_caster;
    rabbit_caster.health = 1000;
    rabbit_caster.maximum_health = 1000;
    rabbit_caster.spell_points = 100;
    rabbit_caster.maximum_spell_points = 100;
    rabbit_caster.spells[11] = 1;
    yipe::CombatEncounter rabbit_group;
    rabbit_group.monsters[0] = {2, "One", 175, 30, 30, 0, 1, 8, 9, false};
    rabbit_group.monsters[1] = {3, "Two", 176, 40, 50, 7, 4, 11, 12, true};
    yipe::MsvcRandom rabbit_random(1);
    const yipe::CombatSpellResult rabbit = yipe::resolve_spell_turn(
        rabbit_caster, rabbit_group, 11, yipe::SpellPower::low, rabbit_random);
    check(rabbit.status == yipe::SpellCastStatus::cast
              && rabbit_group.monsters[1].type_id == 1
              && rabbit_group.monsters[1].bitmap_resource_id == 174
              && rabbit_group.monsters[1].health == 40
              && rabbit_group.monsters[1].maximum_health == 50
              && rabbit_group.monsters[1].rank == 4
              && rabbit_group.monsters[1].special_rating == 5
              && rabbit_group.monsters[1].attack_rating == 5
              && rabbit_group.monsters[1].defense_rating == 5
              && rabbit_caster.spell_points == 67,
          "Rabbit Replacement transforms the last opponent while preserving health and rank");
}

void test_first_playable_flow() {
    const std::optional<yipe::Map> map = yipe::original_map(0x0b);
    check(map.has_value(), "first encounter map is available");
    if (!map) {
        return;
    }

    yipe::MapPosition position{5, 10};
    constexpr std::array<yipe::MapPosition, 8> route{{
        {0, -1}, {0, -1}, {0, -1}, {0, -1}, {0, -1},
        {-1, 0}, {-1, 0}, {-1, 0},
    }};
    for (const yipe::MapPosition delta : route) {
        const yipe::MovementResult movement =
            yipe::try_move(*map, position, delta.x, delta.y);
        check(movement.position_changed, "first encounter route stays on passable tiles");
    }
    constexpr std::array<yipe::MapPosition, 3> final_steps{{
        {0, 1}, {0, 1}, {0, 1},
    }};
    for (const yipe::MapPosition delta : final_steps) {
        static_cast<void>(yipe::try_move(*map, position, delta.x, delta.y));
    }
    check(position == yipe::MapPosition{2, 8},
          "the recovered map route reaches the Giant Beetle placement");

    yipe::GameState game;
    game.location = {{2, 8}, 0, 1, 0, false, 0x0b};
    yipe::MsvcRandom random(1);
    const yipe::MonsterPlacement placement =
        yipe::original_monsters_for_map(0x0b)[0];
    yipe::CombatEncounter encounter = yipe::start_monster_encounter(
        placement, game.player, random, map->environment());
    const yipe::CombatTurn opening =
        yipe::resolve_attack_turn(game.player, encounter, random);
    check(opening.monster_damage == 2 && game.player.health == 11,
          "playable flow takes combat damage before item use");
    const yipe::ItemUseResult item = game.player.use_inventory_item(0, random, false, true);
    check(item.consumed && game.player.inventory[0] == 0,
          "playable flow consumes the starting healing item in combat");
    static_cast<void>(yipe::resolve_monster_turn(game.player, encounter, random));

    yipe::CombatTurn turn;
    for (int attempt = 0; attempt < 30 && turn.outcome == yipe::CombatOutcome::ongoing;
         ++attempt) {
        turn = yipe::resolve_attack_turn(game.player, encounter, random);
    }
    check(turn.outcome == yipe::CombatOutcome::victory,
          "playable flow can finish the encounter after using an item");
    game.defeated_monster_placements.push_back(placement.placement_index);
    game.random_state = random.state();

    const std::filesystem::path path =
        std::filesystem::temp_directory_path() / "yipe3-rebuild-flow-test.y3r";
    yipe::save_game_atomic(path, game);
    const yipe::GameState resumed = yipe::load_game(path);
    check(resumed.location.position == yipe::MapPosition{2, 8}
              && resumed.defeated_monster_placements
                  == std::vector<std::uint16_t>{placement.placement_index}
              && resumed.player.inventory[0] == 0
              && resumed.player.experience > 0 && resumed.random_state == random.state(),
          "playable flow resumes its location, encounter, inventory, reward, and random state");
    std::filesystem::remove(path);
}

void test_original_runtime_saves() {
    const auto directory = std::filesystem::path(YIPE_TEST_EVIDENCE_DIR) / "r08";
    const auto native = std::filesystem::temp_directory_path() / "yipe-r08-import.y3r";
    for (const auto* filename : {"R08Combat-start.yp3", "R08Combat-south.yp3",
                                 "R08Combat-reopened.yp3", "R08Combat-restart.yp3"}) {
        const auto imported = yipe::load_legacy_save(directory / filename);
        const auto& game = imported.game;
        const auto& player = game.player;
        const int y = std::string_view(filename) == "R08Combat-start.yp3" ? 5 : 6;
        check(imported.validation_passed && imported.stored_validation == 5
                  && player.name == "R08Combat" && player.dexterity == 13
                  && player.intelligence == 10 && player.strength == 16
                  && player.health == 13 && player.maximum_health == 13
                  && player.spell_points == 10 && player.maximum_spell_points == 10
                  && player.ability_points == 5 && player.maximum_ability_points == 5
                  && player.level == 1 && player.gold == 0 && player.experience == 0
                  && player.weapon_rank == 2 && player.armor_rank == 2
                  && player.helmet_rank == 1 && player.difficulty == 2
                  && player.inventory[28] == 0 && !player.silly_mode
                  && game.location.map_id == 0x11 && game.location.map_layer == 0
                  && game.location.region_x == 0 && game.location.region_y == 0
                  && game.location.position == yipe::MapPosition{5, y},
              std::string("original-produced archive matches recorded state: ") + filename);
        yipe::save_game_atomic(native, game);
        const auto restored = yipe::load_game(native);
        check(restored.player.name == player.name
                  && restored.player.health == player.health
                  && restored.player.strength == player.strength
                  && restored.player.spell_points == player.spell_points
                  && restored.player.ability_points == player.ability_points
                  && restored.player.inventory == player.inventory
                  && restored.player.inventory_values == player.inventory_values
                  && restored.player.spells == player.spells
                  && restored.player.monster_defeats == player.monster_defeats
                  && restored.player.encountered_monster_types == player.encountered_monster_types
                  && restored.location.position == game.location.position
                  && restored.location.map_id == game.location.map_id,
              std::string("real archive survives native save/reload: ") + filename);
    }
    std::filesystem::remove(native);
}

void test_exploration_timer() {
    yipe::Player player;
    player.health = player.maximum_health = 90;
    yipe::MsvcRandom random(1);
    for (int id = 0; id <= 0x85; ++id) {
        const auto hazard = yipe::timed_hazard_for_map(id);
        if (!hazard) continue;
        auto map = *yipe::original_map(id);
        yipe::MapVisibility visibility;
        visibility.map_id = id;
        visibility.visible[static_cast<std::size_t>(
            hazard->first.x * yipe::Map::height + hazard->first.y)] = true;
        std::optional<yipe::RoamingMonster> roaming = yipe::RoamingMonster{1, hazard->second};
        check(!yipe::advance_timed_hazard(map, visibility, player, {0, 0}, roaming, random).changed,
              "both hazard cells must be visible before the timer changes terrain");
        visibility.visible.fill(true);
        map.restore_tile(hazard->first, hazard->floor);
        map.restore_tile(hazard->second, hazard->floor);
        auto update = yipe::advance_timed_hazard(
            map, visibility, player, hazard->first, roaming, random);
        check(update.changed && map.tile(hazard->first) == hazard->hazard
                  && map.tile(hazard->second) == hazard->hazard,
              "timer arms both authored hazard cells");
        check((hazard->hazard == 0x24) == !roaming.has_value(),
              "arming fire removes the roaming monster but arming spikes/cracks does not");
        player.health = 1;
        roaming = yipe::RoamingMonster{1, hazard->second};
        update = yipe::advance_timed_hazard(
            map, visibility, player, hazard->second, roaming, random);
        check(!roaming && player.health == 1
                  && update.fell == (hazard->hazard == 0x2a)
                  && update.sound_id == (hazard->hazard == 0x24 ? 0
                                         : hazard->hazard == 0x2a ? 135 : 137),
              "hazard activation affects the second cell and never kills the player");
        if (hazard->hazard != 0x24) {
            check(map.tile(hazard->first) == hazard->hazard + 1,
                  "spikes and cracks retain an active phase");
            static_cast<void>(yipe::advance_timed_hazard(
                map, visibility, player, {0, 0}, roaming, random));
        }
        check(map.tile(hazard->first) == hazard->floor
                  && map.tile(hazard->second) == hazard->floor,
              "the hazard cycle restores both original floors");
    }

    yipe::Map::Tiles rails;
    rails.fill(0x0d);
    yipe::MapEvents events;
    events.overlay = {{4, 5}, 1};
    yipe::Map platform(0x4a, 5, rails, events);
    yipe::MapVisibility visibility;
    yipe::MapPosition rider{4, 5};
    check(!platform.advance_overlay(visibility, rider, random),
          "hidden platforms do not move");
    visibility.visible.fill(true);
    check(!platform.advance_overlay(visibility, rider, random, yipe::MapPosition{4, 4})
              && platform.events().overlay.value == 3,
          "roaming monster blocks the platform and reverses its direction");
    check(platform.advance_overlay(visibility, rider, random)
              && rider == yipe::MapPosition{4, 6},
          "a visible platform carries its rider in the reversed direction");
    platform.restore_tile({4, 7}, 0x53);
    check(!platform.advance_overlay(visibility, rider, random)
              && platform.events().overlay.value == 1,
          "platforms cannot leave their authored 0x0c/0x0d rails");
    events.overlay.value = 5;
    rails.fill(0x53);
    rails[4 * yipe::Map::height + 5] = 0x0d;
    yipe::Map random_platform(0x4a, 5, rails, events);
    yipe::MsvcRandom expected_random(random.state());
    static_cast<void>(expected_random.next());
    static_cast<void>(expected_random.next());
    static_cast<void>(random_platform.advance_overlay(visibility, rider, random));
    check(random.state() == expected_random.state(),
          "random platform retries do not consume extra random numbers");

    auto passage = *yipe::original_map(5);
    yipe::ExplorationTimer timer;
    player.spell_points = player.ability_points = 0;
    player.maximum_spell_points = player.maximum_ability_points = 10;
    for (int tick = 1; tick <= 76; ++tick) {
        check(timer.advance(player, passage, {5, 5}) == tick,
              "exploration timer follows its 76-tick phase");
        if (tick == 38) check(player.spell_points == 2, "first SP restoration tick");
        if (tick == 47) check(passage.tile({7, 8}) == 2, "timed mountain passage opens");
        if (tick == 57) check(player.ability_points == 1, "AP restoration tick");
        if (tick == 61) check(passage.tile({7, 8}) == 0x11, "timed passage closes");
    }
    check(player.spell_points == 4 && timer.advance(player, passage, {5, 5}) == 1,
          "the final SP tick resets the phase without a duplicate zero tick");
    player.monster_repellent_steps = 2;
    player.random_encounter_threshold = 125;
    player.saved_random_encounter_threshold = 79;
    check(!yipe::advance_repellent_timer(player) && yipe::advance_repellent_timer(player)
              && player.random_encounter_threshold == 79,
          "repellent duration counts timer ticks, restoring the saved encounter threshold");
    player.monster_repellent_steps = 1;
    player.saved_random_encounter_threshold = 125;
    player.difficulty = 2;
    check(yipe::advance_repellent_timer(player) && player.random_encounter_threshold == 83,
          "repellent expiration reproduces the original threshold fallback");

    player.difficulty = 2;
    player.level = 7;
    player.health = 90;
    yipe::MsvcRandom fire_random(1);
    check(yipe::apply_fire_wall(player, fire_random) == 2 + 41 % 2 + 18467 % 7
              && player.health == 86,
          "fire damage consumes the difficulty roll before the level roll");
}

void test_timed_princess_routes() {
    yipe::GameState game;
    game.player.has_rainbow_key = true;
    game.player.health = game.player.maximum_health = 100;
    yipe::MsvcRandom random(1);
    yipe::Map map = *yipe::original_map(0x44);
    game.location = {{9, 10}, -1, -2, 1, false, 0x44};
    check(yipe::try_move(map, game.location.position, 0, 1).original_code == 2
              && yipe::cross_map_edge(game.location) && game.location.map_id == 0x4a
              && game.location.position == yipe::MapPosition{9, 1},
          "princess 11 route enters 0x4a from the ordinary south edge of 0x44");
    map = *yipe::original_map(0x4a);
    yipe::initialize_map_visibility(game.map_visibility, map, game.location.position);
    const auto step = [&](yipe::MapPosition next) {
        const auto previous = game.location.position;
        check(std::abs(next.x - previous.x) <= 1 && std::abs(next.y - previous.y) <= 1,
              "route proof uses only adjacent movement");
        const auto result = yipe::try_move(
            map, game.location.position, next.x - previous.x, next.y - previous.y,
            {false, game.player.has_rainbow_key, false, false});
        check(result.position_changed && game.location.position == next,
              "timed princess route uses legal movement");
        if (result.original_code == 0x12) map.open_door(previous, next);
        yipe::reveal_map_from(game.map_visibility, map, game.location.position);
    };
    for (const auto next : {yipe::MapPosition{8, 2}, {7, 3}, {6, 4}, {5, 5},
                            {5, 6}, {5, 7}, {5, 8}, {4, 9}}) step(next);
    yipe::ExplorationTimer timer;
    const auto wait_for_platform = [&](yipe::MapPosition target) {
        for (int ticks = 0; ticks < 10000 && game.location.position != target; ++ticks) {
            if (timer.advance(game.player, map, game.location.position) % 3 == 0) {
                static_cast<void>(map.advance_overlay(
                    game.map_visibility, game.location.position, random));
                yipe::reveal_map_from(game.map_visibility, map, game.location.position);
            }
        }
        check(game.location.position == target, "seeded platform reaches the required landing");
    };
    wait_for_platform({3, 3});
    const auto native = std::filesystem::temp_directory_path() / "yipe-platform-rider.y3r";
    game.random_state = random.state();
    yipe::save_game_atomic(native, game);
    game = yipe::load_game(native);
    map = *yipe::original_map(0x4a);
    map.restore_overlay_for_saved_position(game.location.position);
    yipe::initialize_map_visibility(game.map_visibility, map, game.location.position);
    check(map.events().overlay.position == game.location.position,
          "reopening aboard a platform restores support without permanent terrain changes");
    step({2, 3});
    step({1, 3});
    step({1, 2});
    step({1, 1});
    step({2, 1});
    check(game.location.position == map.events().princess.position
              && map.events().princess.rescue_index == 11,
          "moving platform plus rainbow-key door reaches princess 11");
    static_cast<void>(yipe::rescue_princess(game.player, 11, yipe::PrincessGift::kiss, random));
    for (const auto next : {yipe::MapPosition{1, 1}, {1, 2}, {1, 3}, {2, 3}, {3, 3}}) step(next);
    wait_for_platform({4, 5});
    step({5, 5});
    check(game.player.inventory_values[11] > 0 && game.map_tile_changes.empty(),
          "princess 11 can be rescued and left without opening permanent terrain");

    map = *yipe::original_map(0x7f);
    game.location = {{8, 10}, 0, -2, 0, true, 0x7f};
    check(yipe::try_move(map, game.location.position, 0, 1).original_code == 2
              && yipe::cross_map_edge(game.location) && game.location.map_id == 0x85
              && game.location.position == yipe::MapPosition{8, 1},
          "princess 20 route enters 0x85 from 0x7f's ordinary south edge");
    map = *yipe::original_map(0x85);
    yipe::initialize_map_visibility(game.map_visibility, map, game.location.position);
    for (const auto next : {yipe::MapPosition{7, 2}, {6, 3}, {6, 4}, {6, 5},
                            {6, 6}, {6, 7}, {6, 8}}) step(next);
    check(!yipe::try_move(map, game.location.position, -1, 0).position_changed,
          "the fire wall blocks the princess chamber until a timed opening");
    yipe::ExplorationTimer fire_timer;
    for (int tick = 0; tick < 4; ++tick) {
        if (fire_timer.advance(game.player, map, game.location.position) % 4 == 0) {
            static_cast<void>(yipe::advance_timed_hazard(
                map, game.map_visibility, game.player, game.location.position,
                game.roaming_monster, random));
        }
    }
    for (const auto next : {yipe::MapPosition{5, 8}, {4, 8}, {3, 8}, {2, 8}, {1, 9}}) step(next);
    check(game.location.position == map.events().princess.position
              && map.events().princess.rescue_index == 20,
          "the timed fire-wall opening reaches princess 20");
    static_cast<void>(yipe::rescue_princess(game.player, 20, yipe::PrincessGift::kiss, random));
    static_cast<void>(yipe::advance_timed_hazard(
        map, game.map_visibility, game.player, game.location.position,
        game.roaming_monster, random));
    check(map.tile({5, 8}) == 0x24 && map.tile({8, 5}) == 0x24,
          "both fire walls close again rather than becoming permanent exits");
    for (const auto next : {yipe::MapPosition{2, 8}, {3, 8}, {4, 8}}) step(next);
    static_cast<void>(yipe::advance_timed_hazard(
        map, game.map_visibility, game.player, game.location.position,
        game.roaming_monster, random));
    step({5, 8});
    step({6, 8});
    yipe::save_game_atomic(native, game);
    const auto restored = yipe::load_game(native);
    check(restored.player.inventory_values[11] > 0 && restored.player.inventory_values[20] > 0
              && restored.map_tile_changes.empty()
              && yipe::original_map(0x85)->tile({5, 8}) == 0x24,
          "both rescues survive saving while timed terrain resets on map reload");
    std::filesystem::remove(native);
}

void test_linked_princess_route() {
    std::optional<yipe::Map> entrance_map = yipe::original_map(0x2d);
    std::optional<yipe::Map> princess_map = yipe::original_map(0x2e);
    check(entrance_map.has_value() && princess_map.has_value(),
          "linked-princess route maps are available");
    if (!entrance_map || !princess_map) {
        return;
    }

    yipe::Player player;
    player.gold = 30000; // Fund the purchases; this does not prove normal-play income.
    yipe::MsvcRandom random(1);
    for (int strength = 17; strength <= 40; ++strength) {
        static_cast<void>(yipe::enter_miscellaneous_shop(player));
        const auto purchase = yipe::purchase_shop_item(
            player, yipe::miscellaneous_shop_stock(player, 0x11)[2]);
        if (purchase.status != yipe::PurchaseStatus::purchased) {
            check(false, "funded strength growth can purchase the next potion");
            return;
        }
        check(player.use_inventory_item(purchase.inventory_slot, random, false).consumed
                  && player.strength == strength,
              "ordinary shop purchases and item use grow strength through 30 to 40");
        if (strength == 39) {
            yipe::initialize_map_actions(player, *entrance_map, {10, 9});
            check(entrance_map->tile({11, 9}) == 0x49 && entrance_map->tile({12, 9}) == 0x25,
                  "earned strength 39 still leaves the outer linked mechanism disabled");
        }
    }
    check(player.gold == 0 && player.miscellaneous_shop_stage == 1,
          "24 strength potions cost 30000 including the earned shop price increase");
    yipe::WorldLocation location{{10, 9}, 0, 2, 1, false, 0x2d};
    yipe::initialize_map_actions(player, *entrance_map, location.position);
    const yipe::MovementResult switch_step =
        yipe::try_move(*entrance_map, location.position, 1, 0);
    check(switch_step.original_code == 0x1a && entrance_map->toggle_linked_tiles()
              && entrance_map->tile({12, 9}) == 0x53,
          "strength-40 switch opens map 0x2d's east boundary");
    const yipe::MovementResult crossing =
        yipe::try_move(*entrance_map, location.position, 1, 0);
    const bool crossed = crossing.original_code == 2
        && yipe::cross_map_edge(location);
    check(crossed && location.map_id == 0x2e
              && location.position == yipe::MapPosition{1, 9},
          "map 0x2d's opened east exit enters map 0x2e");
    if (location.map_id != 0x2e || location.position != yipe::MapPosition{1, 9}) {
        return;
    }
    yipe::initialize_map_actions(player, *princess_map, location.position);

    constexpr std::array<yipe::MapPosition, 10> to_switch{{
        {2, 8}, {3, 7}, {4, 6}, {5, 5}, {6, 5},
        {7, 6}, {8, 7}, {9, 8}, {10, 9}, {11, 10},
    }};
    for (const yipe::MapPosition next : to_switch) {
        const yipe::MovementResult movement = yipe::try_move(
            *princess_map, location.position, next.x - location.position.x,
            next.y - location.position.y);
        if (!movement.position_changed || location.position != next) {
            check(false, "the linked-princess route reaches its switch");
            return;
        }
    }
    check(princess_map->tile({8, 3}) == 0x25
              && princess_map->toggle_linked_tiles()
              && princess_map->tile({8, 3}) == 0x53,
          "the switch opens the wall to the princess chamber");

    yipe::MovementState equipped;
    equipped.has_rainbow_key = true;
    constexpr std::array<yipe::MapPosition, 11> to_princess{{
        {10, 9}, {9, 8}, {8, 7}, {9, 6}, {10, 5}, {11, 4},
        {10, 3}, {9, 2}, {8, 3}, {7, 2}, {7, 1},
    }};
    bool opened_door = false;
    for (const yipe::MapPosition next : to_princess) {
        const yipe::MapPosition previous = location.position;
        const yipe::MovementResult movement = yipe::try_move(
            *princess_map, location.position, next.x - previous.x,
            next.y - previous.y, equipped);
        if (!movement.position_changed || location.position != next) {
            check(false, "the linked-princess route reaches the princess tile");
            return;
        }
        if (movement.original_code == 0x12) {
            princess_map->open_door(previous, location.position);
            opened_door = true;
        }
    }
    check(opened_door && location.position == princess_map->events().princess.position
              && princess_map->events().princess.rescue_index == 13,
          "linked wall and rainbow key make princess 13 reachable by ordinary movement");
    check(yipe::rescue_princess(player, princess_map->events().princess.rescue_index,
                              yipe::PrincessGift::kiss, random).rescued
              && player.inventory_values[13] > 0,
          "the linked route rescues princess 13 after earning its strength prerequisite");
}

} // namespace

int main() {
    test_random();
    test_player_defaults();
    test_random_character_creation();
    test_princess_statistics();
    test_health_change();
    test_level_progression();
    test_healing_item();
    test_stat_and_maximum_items();
    test_stat_caps();
    test_relocation_item();
    test_item_names();
    test_native_save();
    test_game_save();
    test_legacy_save_import();
    test_original_runtime_saves();
    test_original_auxiliary_data();
    test_high_scores();
    test_high_score_code();
    test_high_score_save();
    test_settings_and_audio_catalog();
    test_starting_map();
    test_static_map_catalog_and_edges();
    test_runtime_generated_maps();
    test_special_movement_actions();
    test_other_action();
    test_map_entry_and_hints();
    test_movement_hint_defeat_state();
    test_monster_defeat_statistics();
    test_starting_shop();
    test_miscellaneous_shops();
    test_recovered_shop_transactions();
    test_recovered_casino_games();
    test_giant_beetle_encounter();
    test_multi_monster_encounter();
    test_special_abilities();
    test_monster_placement_catalog();
    test_roaming_monsters();
    test_monster_definition_catalog();
    test_weapon_damage_catalog();
    test_equipment_combat_ratings();
    test_king_quest_dialog();
    test_quest_progression_events();
    test_chest_rewards();
    test_death_and_hell_recovery();
    test_recovered_spells();
    test_first_playable_flow();
    test_linked_princess_route();
    test_exploration_timer();
    test_timed_princess_routes();

    if (failures != 0) {
        std::cerr << failures << " test(s) failed\n";
        return 1;
    }
    std::cout << "All core tests passed\n";
}
