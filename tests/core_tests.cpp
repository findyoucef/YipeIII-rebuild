#include "yipe/auxiliary_data.hpp"
#include "yipe/encounter.hpp"
#include "yipe/item_names.hpp"
#include "yipe/map.hpp"
#include "yipe/player.hpp"
#include "yipe/random.hpp"
#include "yipe/save.hpp"
#include "yipe/shop.hpp"

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

void convert_game_save_to_version_2(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                    std::istreambuf_iterator<char>()};
    constexpr std::size_t header_size = 14;
    constexpr std::size_t version_3_extension_size = 7 * sizeof(std::int32_t);
    bytes.resize(bytes.size() - version_3_extension_size);
    bytes[4] = 2;
    bytes[5] = 0;
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
    enhanced.unknown_0065_healing_bonus = true;
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
    original.unknown_0065_healing_bonus = true;
    original.unknown_0680 = 77;

    yipe::save_player_atomic(path, original);
    const yipe::Player loaded = yipe::load_player(path);
    check(loaded.name == original.name && loaded.intelligence == original.intelligence,
          "native save restores identity and stats");
    check(loaded.health == 7 && loaded.maximum_spell_points == 19,
          "native save restores health and spell points");
    check(loaded.inventory == original.inventory, "native save restores inventory");
    check(loaded.unknown_0065_healing_bonus && loaded.unknown_0680 == 77,
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
    original.location = {{2, 8}, 0, 1, 0, false, 0x0b};
    original.giant_beetle_defeated = true;
    original.random_state = 0x12345678u;

    yipe::save_game_atomic(path, original);
    const yipe::GameState loaded = yipe::load_game(path);
    check(loaded.player.name == original.player.name && loaded.player.experience == 41
              && loaded.player.gold == 43 && loaded.player.weapon_rank == 3
              && loaded.player.gloves_rank == 1,
          "version 3 save restores progression and equipment");
    check(loaded.player.difficulty == 3 && loaded.player.combat_style == 1
              && loaded.player.equipment_upgrade_count == 2
              && loaded.player.loot_roll_penalty == 7,
          "version 3 save restores combat settings and counters");
    check(loaded.location.position == yipe::MapPosition{2, 8}
              && loaded.location.region_y == 1 && loaded.location.map_id == 0x0b,
          "version 3 save restores world location");
    check(loaded.giant_beetle_defeated && loaded.random_state == 0x12345678u,
          "version 3 save restores encounter and random state");
    check(loaded.player.ability_points == 2
              && loaded.player.maximum_ability_points == 5
              && loaded.player.special_ability == yipe::SpecialAbility::enrage
              && loaded.player.special_ability_duration == 8
              && loaded.player.enrage_base_dexterity == 13
              && loaded.player.enrage_base_intelligence == 10
              && loaded.player.enrage_base_strength == 16,
          "version 3 save restores ability and active status state");

    convert_game_save_to_version_2(path);
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
    test_healing_item();
    test_stat_and_maximum_items();
    test_item_names();
    test_native_save();
    test_game_save();
    test_original_auxiliary_data();
    test_starting_map();
    test_static_map_catalog_and_edges();
    test_starting_shop();
    test_giant_beetle_encounter();
    test_multi_monster_encounter();
    test_special_abilities();
    test_monster_placement_catalog();
    test_monster_definition_catalog();
    test_weapon_damage_catalog();
    test_first_playable_flow();

    if (failures != 0) {
        std::cerr << failures << " test(s) failed\n";
        return 1;
    }
    std::cout << "All core tests passed\n";
}
