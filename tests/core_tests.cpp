#include "yipe/auxiliary_data.hpp"
#include "yipe/item_names.hpp"
#include "yipe/map.hpp"
#include "yipe/player.hpp"
#include "yipe/random.hpp"
#include "yipe/save.hpp"
#include "yipe/shop.hpp"

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
    test_original_auxiliary_data();
    test_starting_map();
    test_starting_shop();

    if (failures != 0) {
        std::cerr << failures << " test(s) failed\n";
        return 1;
    }
    std::cout << "All core tests passed\n";
}
