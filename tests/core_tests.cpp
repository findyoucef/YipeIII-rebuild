#include "yipe/auxiliary_data.hpp"
#include "yipe/casino.hpp"
#include "yipe/encounter.hpp"
#include "yipe/item_names.hpp"
#include "yipe/legacy_save.hpp"
#include "yipe/map.hpp"
#include "yipe/player.hpp"
#include "yipe/random.hpp"
#include "yipe/save.hpp"
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
    check(player.name == "Yipie", "default player name");
    check(player.strength == 16 && player.dexterity == 13 && player.intelligence == 10,
          "warrior default stats");
    check(player.health == 13 && player.maximum_health == 13, "warrior default health");
    check(player.spell_points == 0 && player.maximum_spell_points == 0,
          "initial spell points are reset");
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
    check(rogue.ability_points == 6 && rogue.maximum_ability_points == 6,
          "rogue starts with six ability points");

    const yipe::Player wizard(yipe::CharacterType::wizard);
    check(wizard.strength == 11 && wizard.dexterity == 12 && wizard.intelligence == 16,
          "wizard default stats");
    check(wizard.health == 10 && wizard.maximum_health == 10,
          "wizard default health");
    check(wizard.ability_points == 5 && wizard.maximum_ability_points == 5,
          "wizard starts with five ability points");
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
              && warrior.spell_points == 6 && warrior.maximum_spell_points == 6
              && warrior.restoration_uses == 1
              && warrior_level.stat_increase == yipe::StatIncrease::none,
          "ordinary level growth applies gains and restores one restoration use");

    yipe::Player milestone(yipe::CharacterType::wizard);
    milestone.level = 4;
    milestone.experience = 1501;
    yipe::MsvcRandom milestone_random(1);
    const yipe::LevelUpResult milestone_level =
        yipe::try_level_up(milestone, milestone_random);
    check(milestone.level == 5
              && milestone_level.stat_increase == yipe::StatIncrease::intelligence
              && milestone.intelligence == 17,
          "milestone levels force the archetype's preferred stat increase");

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

    player.strength = 30;
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
    original.unknown_0680 = 77;

    yipe::save_player_atomic(path, original);
    const yipe::Player loaded = yipe::load_player(path);
    check(loaded.name == original.name && loaded.intelligence == original.intelligence,
          "native save restores identity and stats");
    check(loaded.health == 7 && loaded.maximum_spell_points == 19,
          "native save restores health and spell points");
    check(loaded.inventory == original.inventory, "native save restores inventory");
    check(loaded.has_bonus_healing_ring && loaded.unknown_0680 == 77,
          "native save preserves reconstructed unknown fields");
    const yipe::GameState upgraded_from_v1 = yipe::load_game(path);
    check(upgraded_from_v1.player.name == original.name
              && upgraded_from_v1.location.map_id == 0x11
              && !upgraded_from_v1.giant_beetle_defeated,
          "version 1 player save loads with default world state");

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
    original.location = {{2, 8}, 0, 1, 0, false, 0x0b};
    original.giant_beetle_defeated = true;
    original.random_state = 0x12345678u;
    original.map_tile_changes = {{0x2b, {6, 1}, 40}, {0x2b, {5, 8}, 34}};

    yipe::save_game_atomic(path, original);
    const yipe::GameState loaded = yipe::load_game(path);
    check(loaded.player.name == original.player.name && loaded.player.experience == 41
              && loaded.player.gold == 43 && loaded.player.weapon_rank == 3
              && loaded.player.gloves_rank == 1,
          "version 6 save restores progression and equipment");
    check(loaded.player.difficulty == 3 && loaded.player.combat_style == 1
              && loaded.player.equipment_upgrade_count == 2
              && loaded.player.loot_roll_penalty == 7,
          "version 6 save restores combat settings and counters");
    check(loaded.location.position == yipe::MapPosition{2, 8}
              && loaded.location.region_y == 1 && loaded.location.map_id == 0x0b,
          "version 6 save restores world location");
    check(loaded.giant_beetle_defeated && loaded.random_state == 0x12345678u,
          "version 6 save restores encounter and random state");
    check(loaded.player.ability_points == 2
              && loaded.player.maximum_ability_points == 5
              && loaded.player.special_ability == yipe::SpecialAbility::enrage
              && loaded.player.special_ability_duration == 8
              && loaded.player.enrage_base_dexterity == 13
              && loaded.player.enrage_base_intelligence == 10
              && loaded.player.enrage_base_strength == 16,
          "version 6 save restores ability and active status state");
    check(loaded.player.restoration_uses == 3
              && loaded.player.has_waterproof_shoe_spray
              && loaded.player.has_rainbow_key
              && loaded.player.has_underworld_key
              && loaded.player.has_tree_climbing_shoes,
          "version 6 save restores special movement state");
    check(loaded.map_tile_changes == original.map_tile_changes,
          "version 6 save restores persistent map tile changes");
    check(loaded.player.spells == original.player.spells,
          "version 6 save restores learned spells");

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
              && loaded_version_2.player.special_ability == yipe::SpecialAbility::none,
          "version 2 saves receive default ability state");
    std::filesystem::remove(path);
}

std::vector<std::uint8_t> legacy_save_bytes(const std::string& name) {
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
    append_i32(bytes, 44);
    append_i32(bytes, 55);

    for (const bool value : std::array{false, true, true, false, false, true, true}) {
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
        case 3: value = 1; break;
        case 6: value = 125; break;
        case 11: value = 1; break;
        case 15: value = 3; break;
        case 16: value = 85; break;
        case 17: value = 22; break;
        case 26: value = 2; break;
        case 31: value = 1; break;
        default: break;
        }
        append_u16(bytes, value);
        append_i32(bytes, static_cast<std::int32_t>(1000 + index));
        append_i32(bytes, index == 4 ? 1 : 0);
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
              && player.has_tree_climbing_shoes,
          "legacy import maps recovered difficulty, movement, and combat state");
    check(player.inventory[0] == 5 && player.inventory[29] == 9
              && player.spells == imported.values_010c
              && player.has_bonus_healing_ring && player.unknown_0680 == 22,
          "legacy import maps inventory, spells, and recovered extended fields");
    check(imported.flags_0152[7] && imported.flags_0352[8]
              && imported.values_010c[14] == 15
              && imported.flag_values[4].flag_0148
              && imported.inventory_entries[29].value_00d0 == 39
              && imported.inventory_entries[29].flag_012a
              && imported.extended_entries[4].value_0584 == 1004
              && imported.extended_entries[4].flag_0552
              && imported.trailing_healing_flag,
          "legacy import retains unresolved serialized fields");

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
    }
    check(messages == 20 && overlays == 10 && depth_events == 26
              && linked_first == 8 && linked_second == 3,
          "generated maps preserve all statically configured movement-event positions");

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

        event_map->restore_tile({6, 1}, 40);
        event_map->restore_tile({5, 8}, 34);
        check(event_map->toggle_linked_tiles() && event_map->tile({6, 1}) == 34
                  && event_map->tile({5, 8}) == 40,
              "linked-tile action recognizes and restores persisted swapped tiles");
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
    const auto& stock = yipe::freds_miscellaneous_stock();
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
    check(placements.size() == 1 && placements[0].type_id == 2
              && placements[0].position == yipe::MapPosition{2, 8}
              && placements[0].slot == 0 && placements[0].encounter_size == 1
              && !placements[0].flag_36c && !placements[0].flag_371,
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
    int flag_36c_count = 0;
    int flag_371_count = 0;
    for (int map_id = 0; map_id <= 0x85; ++map_id) {
        for (const yipe::MonsterPlacement& placement :
             yipe::original_monsters_for_map(map_id)) {
            ++placement_count;
            flag_36c_count += placement.flag_36c ? 1 : 0;
            flag_371_count += placement.flag_371 ? 1 : 0;
        }
    }
    check(placement_count == 186 && flag_36c_count == 23 && flag_371_count == 16,
          "generated catalog exposes every recovered monster placement and flag");

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
        high_rank_placement, high_rank_player, high_rank_random);
    check(high_rank_encounter.monsters[0].type_id == 35
              && high_rank_encounter.monsters[0].health == 111
              && high_rank_encounter.monsters[1].type_id == 8
              && high_rank_encounter.monsters[2].type_id == 8
              && high_rank_random.state() == 2179684817u,
          "high-rank placement setup applies companion overrides and level scaling");

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
    check(yipe::spell_point_cost(wizard, 1, yipe::SpellPower::low) == 6
              && yipe::spell_point_cost(warrior, 1, yipe::SpellPower::low) == 10
              && yipe::spell_point_cost(rogue, 1, yipe::SpellPower::low) == 8
              && yipe::spell_point_cost(warrior, 1, yipe::SpellPower::high) == 20
              && yipe::spell_point_cost(warrior, 5, yipe::SpellPower::high) == 38,
          "spell costs preserve class differences and selective high-power doubling");

    yipe::Player surface_caster;
    surface_caster.spell_points = 10;
    surface_caster.maximum_spell_points = 10;
    yipe::WorldLocation cave{{7, 4}, 0, 0, 1, false, 0x39};
    yipe::MsvcRandom surface_random(1);
    const yipe::ExplorationSpellResult surface = yipe::cast_exploration_spell(
        surface_caster, cave, 0, yipe::SpellPower::low, surface_random);
    check(surface.status == yipe::SpellCastStatus::cast && surface.location_changed
              && cave.position == yipe::MapPosition{7, 4} && cave.map_layer == 0
              && cave.map_id == 0x11 && surface_caster.spell_points == 5
              && surface_caster.unknown_0692 == 1,
          "Return to Surface preserves position and restores the matching surface map");
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
              && healer.spell_points == 90,
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
              && repellent_caster.unknown_066a == 125
              && repellent_caster.unknown_067e == 85
              && repellent_caster.unknown_0680 == 46
              && repellent_caster.spell_points == 72,
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
              && full_healer.spell_points == 62,
          "Full Heal restores all health without a high-power surcharge");

    yipe::Player town_caster;
    town_caster.spells[8] = 1;
    town_caster.spell_points = 100;
    town_caster.maximum_spell_points = 100;
    yipe::WorldLocation distant_surface{{12, 8}, 4, -3, 0, true, 0x75};
    yipe::MsvcRandom town_random(1);
    const yipe::ExplorationSpellResult town = yipe::cast_exploration_spell(
        town_caster, distant_surface, 8, yipe::SpellPower::low, town_random);
    check(town.location_changed && distant_surface.position == yipe::MapPosition{4, 5}
              && distant_surface.region_x == 0 && distant_surface.region_y == 0
              && distant_surface.map_id == 0x11 && town_caster.spell_points == 85
              && town_caster.unknown_0692 == 2,
          "Return to Yipe relocates a surface caster to the recovered town position");

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
              && super_healer.spell_points == 40,
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
              && group_attacker.spell_points == 72,
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
    restricted_caster.spell_points = 9;
    const yipe::ExplorationSpellResult too_expensive = yipe::cast_exploration_spell(
        restricted_caster, outdoors, 1, yipe::SpellPower::low, restricted_random);
    check(wrong_exploration_context.status == yipe::SpellCastStatus::wrong_context
              && wrong_combat_context.status == yipe::SpellCastStatus::wrong_context
              && too_expensive.status
                  == yipe::SpellCastStatus::insufficient_spell_points
              && restricted_caster.spell_points == 9,
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
              && shuffle_caster.spell_points == 86,
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
              && rabbit_caster.spell_points == 64,
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
    yipe::Monster monster = yipe::start_single_monster_encounter(2, random);
    const yipe::CombatTurn opening =
        yipe::resolve_attack_turn(game.player, monster, random);
    check(opening.monster_damage == 2 && game.player.health == 11,
          "playable flow takes combat damage before item use");
    const yipe::ItemUseResult item = game.player.use_inventory_item(0, random, false, true);
    check(item.consumed && game.player.inventory[0] == 0,
          "playable flow consumes the starting healing item in combat");
    static_cast<void>(yipe::resolve_monster_turn(game.player, monster, random));

    yipe::CombatTurn turn;
    for (int attempt = 0; attempt < 30 && turn.outcome == yipe::CombatOutcome::ongoing;
         ++attempt) {
        turn = yipe::resolve_attack_turn(game.player, monster, random);
    }
    check(turn.outcome == yipe::CombatOutcome::victory,
          "playable flow can finish the encounter after using an item");
    game.giant_beetle_defeated = true;
    game.random_state = random.state();

    const std::filesystem::path path =
        std::filesystem::temp_directory_path() / "yipe3-rebuild-flow-test.y3r";
    yipe::save_game_atomic(path, game);
    const yipe::GameState resumed = yipe::load_game(path);
    check(resumed.location.position == yipe::MapPosition{2, 8}
              && resumed.giant_beetle_defeated && resumed.player.inventory[0] == 0
              && resumed.player.experience > 0 && resumed.random_state == random.state(),
          "playable flow resumes its location, encounter, inventory, reward, and random state");
    std::filesystem::remove(path);
}

} // namespace

int main() {
    test_random();
    test_player_defaults();
    test_health_change();
    test_level_progression();
    test_healing_item();
    test_stat_and_maximum_items();
    test_item_names();
    test_native_save();
    test_game_save();
    test_legacy_save_import();
    test_original_auxiliary_data();
    test_starting_map();
    test_static_map_catalog_and_edges();
    test_runtime_generated_maps();
    test_special_movement_actions();
    test_starting_shop();
    test_recovered_shop_transactions();
    test_recovered_casino_games();
    test_giant_beetle_encounter();
    test_multi_monster_encounter();
    test_special_abilities();
    test_monster_placement_catalog();
    test_monster_definition_catalog();
    test_weapon_damage_catalog();
    test_equipment_combat_ratings();
    test_recovered_spells();
    test_first_playable_flow();

    if (failures != 0) {
        std::cerr << failures << " test(s) failed\n";
        return 1;
    }
    std::cout << "All core tests passed\n";
}
