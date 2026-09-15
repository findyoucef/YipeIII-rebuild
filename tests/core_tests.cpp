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
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>

namespace {

int failures = 0;

void check(bool condition, const std::string& description) {
    if (!condition) {
        ++failures;
        std::cerr << "FAIL: " << description << '\n';
    }
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
    check(player.inventory[0] == 1, "new player owns item 1");

    const yipe::Player rogue(yipe::CharacterType::rogue);
    check(rogue.strength == 11 && rogue.dexterity == 15 && rogue.intelligence == 12,
          "rogue default stats");
    check(rogue.health == 13 && rogue.maximum_health == 13,
          "rogue default health");

    const yipe::Player wizard(yipe::CharacterType::wizard);
    check(wizard.strength == 11 && wizard.dexterity == 12 && wizard.intelligence == 16,
          "wizard default stats");
    check(wizard.health == 10 && wizard.maximum_health == 10,
          "wizard default health");
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
    original.location = {{2, 8}, 0, 1, 0, false, 0x0b};
    original.giant_beetle_defeated = true;
    original.random_state = 0x12345678u;

    yipe::save_game_atomic(path, original);
    const yipe::GameState loaded = yipe::load_game(path);
    check(loaded.player.name == original.player.name && loaded.player.experience == 41
              && loaded.player.gold == 43 && loaded.player.weapon_rank == 3
              && loaded.player.gloves_rank == 1,
          "version 2 save restores progression and equipment");
    check(loaded.player.difficulty == 3 && loaded.player.combat_style == 1
              && loaded.player.equipment_upgrade_count == 2
              && loaded.player.loot_roll_penalty == 7,
          "version 2 save restores combat settings and counters");
    check(loaded.location.position == yipe::MapPosition{2, 8}
              && loaded.location.region_y == 1 && loaded.location.map_id == 0x0b,
          "version 2 save restores world location");
    check(loaded.giant_beetle_defeated && loaded.random_state == 0x12345678u,
          "version 2 save restores encounter and random state");
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
              && placements[0].position == yipe::MapPosition{2, 8},
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
    test_first_playable_flow();

    if (failures != 0) {
        std::cerr << failures << " test(s) failed\n";
        return 1;
    }
    std::cout << "All core tests passed\n";
}
