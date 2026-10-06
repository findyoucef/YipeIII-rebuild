// Independent oracle: link this driver to the existing src/player.cpp and src/random.cpp.
// Every row includes the complete Player state after the operation, plus its result and RNG.
// Reproduction and row format are documented in README.md.
#include "yipe/player.hpp"
#include <array>
#include <cstdint>
#include <iostream>
#include <limits>
#include <locale>
#include <stdexcept>

namespace {
constexpr std::array<std::uint32_t, 8> seeds{
    0u, 1u, 2u, 0x7fffffffu, 0x80000000u, 0xffffffffu, 0xdeadbeefu, 2566201570u,
};

void state(const yipe::Player& p);

void growth(const yipe::Player& p, yipe::MsvcRandom& random, const yipe::LevelUpResult& result) {
    std::cout << '|' << result.leveled_up << ',' << result.health_increase << ','
              << result.spell_point_increase << ',' << static_cast<int>(result.stat_increase)
              << ',' << random.state();
    state(p);
}

void item(const yipe::Player& p, yipe::MsvcRandom& random, const yipe::ItemUseResult& result) {
    std::cout << '|' << result.consumed << ',' << result.amount << ',' << result.sound_id
              << ',' << result.message << ',' << random.state();
    state(p);
}
} // namespace

int main() {
    std::cout.imbue(std::locale::classic());
    for (int type = 1; type <= 3; ++type) {
        std::cout << "defaults|" << type;
        state(yipe::Player(static_cast<yipe::CharacterType>(type)));
    }
    for (const auto seed : seeds) {
        yipe::Player p(yipe::CharacterType::wizard);
        p.name = "Reroll";
        p.difficulty = 3;
        p.combat_style = 1;
        p.level = 7;
        p.experience = -123;
        p.gold = 321;
        p.stat_cap_mode = 1;
        p.inventory[28] = 11;
        p.inventory_values[29] = 0xffff;
        p.silly_mode = true;
        p.hell_gatekeeper_state = yipe::HellGatekeeperState::paid;
        p.surface_return_region_x = -7;
        p.surface_return_region_y = -8;
        yipe::MsvcRandom random(seed);
        for (int roll = 0; roll < 64; ++roll) {
            p.reroll_random_character(random);
            std::cout << "rerolls|" << seed << ',' << roll << '|' << random.state();
            state(p);
        }
    }
    for (int health : {-10, 0, 1, 13, 20}) {
        for (int delta : {-20, -1, 0, 1, 20}) {
            yipe::Player p;
            p.health = health;
            const bool alive = p.adjust_health(delta);
            std::cout << "health|" << health << ',' << delta << '|' << alive;
            state(p);
        }
    }
    for (int level = -2; level <= 102; ++level) {
        const auto required = yipe::experience_required_for_next_level(level);
        std::cout << "thresholds|" << level << '|' << required.value_or(-1) << '\n';
    }
    for (const auto seed : seeds) {
        for (int level = 1; level <= 100; ++level) {
            for (int difficulty = 1; difficulty <= 3; ++difficulty) {
                for (int boundary = 0; boundary < 3; ++boundary) {
                    yipe::Player p;
                    p.level = level;
                    p.difficulty = difficulty;
                    p.combat_style = level % 4;
                    p.stat_cap_mode = level % 3;
                    p.strength = 24;
                    p.dexterity = 23;
                    p.intelligence = difficulty % 2 == 0 ? 20 : 21;
                    p.health = -3;
                    p.maximum_health = 80;
                    p.spell_points = 2;
                    p.maximum_spell_points = 99;
                    p.ability_points = 1;
                    p.maximum_ability_points = 9;
                    p.restoration_uses = boundary - 1;
                    const int required = yipe::experience_required_for_next_level(level).value_or(600000);
                    p.experience = boundary == 2 ? 2000000000 : required + boundary - 1;
                    yipe::MsvcRandom random(seed);
                    const auto result = yipe::try_level_up(p, random);
                    std::cout << "growth|" << seed << ',' << level << ',' << difficulty << ',' << boundary;
                    growth(p, random, result);
                }
            }
        }
    }
    for (int mode : {-1, 0, 1, 2, 3}) {
        for (int style : {0, 1, 3}) {
            for (int offset : {-1, 0, 1}) {
                yipe::Player p;
                p.stat_cap_mode = mode;
                p.strength = p.dexterity = p.intelligence = p.maximum_stat() + offset;
                p.combat_style = style;
                p.level = 4;
                p.experience = 1501;
                p.restoration_uses = 2;
                yipe::MsvcRandom random(1);
                const auto result = yipe::try_level_up(p, random);
                std::cout << "caps|" << mode << ',' << style << ',' << offset;
                growth(p, random, result);
            }
        }
    }
    for (int mode : {-1, 0, 1, 2, 3}) {
        for (int id : {6, 7, 8}) {
            for (int offset : {-1, 0, 1}) {
                yipe::Player p;
                p.stat_cap_mode = mode;
                p.strength = p.dexterity = p.intelligence = p.maximum_stat() + offset;
                p.inventory[1] = static_cast<std::uint16_t>(id);
                yipe::MsvcRandom random(1);
                const auto result = p.use_inventory_item(1, random, false);
                std::cout << "stat_items|" << mode << ',' << id << ',' << offset;
                item(p, random, result);
            }
        }
    }
    constexpr std::array health{1, 13, 20, -20, 0, 12};
    constexpr std::array spell_points{0, 10, 15, -10, 1, 9};
    constexpr std::array intelligence{1, 10, 20, 1000, 100, 10};
    constexpr std::array map_ids{57, 57, 17, 155, 57, 40};
    constexpr std::array<std::uint16_t, 6> thresholds{85, 99, 100, 125, 65535, 85};
    for (const auto seed : seeds) {
        for (int id : {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 65535}) {
            for (int slot : {0, 12, 13, 28, 29}) {
                for (int variant = 0; variant < 6; ++variant) {
                    yipe::Player p;
                    p.inventory[slot] = static_cast<std::uint16_t>(id);
                    p.inventory_values[slot] = 0xffff;
                    p.health = health[variant];
                    p.spell_points = spell_points[variant];
                    p.stat_cap_mode = variant % 3;
                    p.strength = p.dexterity = p.maximum_stat() + variant % 3 - 1;
                    p.intelligence = id == 8 ? p.strength : intelligence[variant];
                    p.has_bonus_healing_ring = variant % 2 == 1;
                    p.level = variant == 3 ? 100 : variant + 1;
                    p.random_encounter_threshold = thresholds[variant];
                    p.saved_random_encounter_threshold = 73;
                    p.monster_repellent_steps = variant == 3 ? 65530 : 9;
                    p.surface_return_map_id = variant == 1 ? 1 : -7;
                    p.pending_relocation = 4;
                    yipe::MsvcRandom random(seed);
                    const auto result = p.use_inventory_item(slot, random, (seed & 1) == 0,
                        variant == 4, map_ids[variant], variant == 3);
                    std::cout << "items|" << seed << ',' << id << ',' << slot << ',' << variant;
                    item(p, random, result);
                }
            }
        }
    }
    for (int used : {0, 1}) {
        for (int map_id : {39, 40}) {
            for (bool alternate : {false, true}) {
                for (bool combat : {false, true}) {
                    yipe::Player p;
                    p.inventory[0] = 5;
                    p.surface_return_map_id = static_cast<std::int16_t>(used);
                    yipe::MsvcRandom random(1);
                    const auto result = p.use_inventory_item(0, random, true, combat, map_id, alternate);
                    std::cout << "relocation|" << used << ',' << map_id << ',' << alternate << ',' << combat;
                    item(p, random, result);
                }
            }
        }
    }
    {
        yipe::Player p;
        yipe::MsvcRandom random(1);
        for (int use = 0; use < 10; ++use) {
            p.inventory[0] = 4;
            const auto result = p.use_inventory_item(0, random);
            std::cout << "repellent|" << use;
            item(p, random, result);
        }
    }
    for (int slot = 0; slot < 30; ++slot) {
        for (int value : {0, 1, 2, 0x7fff, 0x8000, 0xffff}) {
            yipe::Player p;
            p.princesses_found = 20;
            p.inventory.fill(1);
            p.quest_flags.fill(true);
            p.inventory_values[slot] = static_cast<std::uint16_t>(value);
            std::cout << "rescues|" << slot << ',' << value << '|' << p.rescued_princess_count();
            state(p);
        }
    }
    {
        yipe::Player p;
        p.inventory_values.fill(1);
        std::cout << "rescues|all|" << p.rescued_princess_count();
        state(p);
    }
    {
        yipe::Player p;
        for (int type = 1; type <= 50; ++type) {
            p.record_monster_defeat(type);
            std::cout << "defeats|" << type;
            state(p);
        }
        for (int value : {std::numeric_limits<int>::max(), -1}) {
            p.monster_defeats[34] = value;
            p.quest_progress_total = value;
            p.record_monster_defeat(35);
            std::cout << "defeats|wrap," << value;
            state(p);
        }
        for (int type : {-1, 0, 51, std::numeric_limits<int>::min(), std::numeric_limits<int>::max()}) {
            bool rejected = false;
            try { p.record_monster_defeat(type); }
            catch (const std::out_of_range&) { rejected = true; }
            std::cout << "defeats|invalid," << type << '|' << rejected;
            state(p);
        }
        for (int slot : {-1, 30, std::numeric_limits<int>::max()}) {
            yipe::MsvcRandom random(1);
            bool rejected = false;
            try { p.use_inventory_item(static_cast<std::size_t>(slot), random); }
            catch (const std::out_of_range&) { rejected = true; }
            std::cout << "invalid_slots|" << slot << '|' << rejected << ',' << random.state();
            state(p);
        }
    }
}

namespace {
void state(const yipe::Player& p) {
    std::cout << '|' << p.name;
    std::cout << ',' << +p.dexterity;
    std::cout << ',' << +p.intelligence;
    std::cout << ',' << +p.strength;
    std::cout << ',' << +p.health;
    std::cout << ',' << +p.maximum_health;
    std::cout << ',' << +p.spell_points;
    std::cout << ',' << +p.maximum_spell_points;
    std::cout << ',' << +p.ability_points;
    std::cout << ',' << +p.maximum_ability_points;
    std::cout << ',' << +p.level;
    std::cout << ',' << +p.experience;
    std::cout << ',' << +p.gold;
    std::cout << ',' << +p.weapon_rank;
    std::cout << ',' << +p.armor_rank;
    std::cout << ',' << +p.boots_rank;
    std::cout << ',' << +p.gloves_rank;
    std::cout << ',' << +p.helmet_rank;
    std::cout << ',' << +p.ring_rank;
    std::cout << ',' << +p.necklace_rank;
    std::cout << ',' << +p.difficulty;
    std::cout << ',' << +p.combat_style;
    std::cout << ',' << +p.specialty;
    std::cout << ',' << +p.silly_mode;
    std::cout << ',' << +p.equipment_upgrade_count;
    std::cout << ',' << +p.loot_roll_penalty;
    std::cout << ',' << +p.restoration_uses;
    std::cout << ',' << +p.death_count;
    std::cout << ',' << +p.quest_score;
    std::cout << ',' << +p.quest_progress_total;
    std::cout << ',' << +p.gatekeeper_state;
    std::cout << ',' << +p.stamps_found;
    std::cout << ',' << +p.princesses_found;
    std::cout << ',' << +p.princess_mystery_uses;
    std::cout << ',' << +p.miscellaneous_shop_stage;
    std::cout << ',' << +p.value_06a4;
    std::cout << ',' << +p.has_lucky_rabbit_foot;
    std::cout << ',' << +p.has_waterproof_shoe_spray;
    std::cout << ',' << +p.has_rainbow_key;
    std::cout << ',' << +p.has_underworld_key;
    std::cout << ',' << +p.has_tree_climbing_shoes;
    std::cout << ',' << static_cast<int>(p.special_ability);
    std::cout << ',' << +p.special_ability_duration;
    std::cout << ',' << +p.enrage_base_dexterity;
    std::cout << ',' << +p.enrage_base_intelligence;
    std::cout << ',' << +p.enrage_base_strength;
    std::cout << ',' << +p.monster_defeat_history_complete;
    std::cout << ',' << +p.alternate_map_set;
    std::cout << ',' << +p.has_bonus_healing_ring;
    std::cout << ',' << +p.current_map_id;
    std::cout << ',' << +p.stat_cap_mode;
    std::cout << ',' << +p.surface_return_map_id;
    std::cout << ',' << +p.surface_return_region_x;
    std::cout << ',' << +p.surface_return_region_y;
    std::cout << ',' << +p.random_encounter_threshold;
    std::cout << ',' << +p.saved_random_encounter_threshold;
    std::cout << ',' << +p.monster_repellent_steps;
    std::cout << ',' << +p.pending_relocation;
    std::cout << ',' << +p.entered_hell_through_secret_passage;
    std::cout << ',' << static_cast<int>(p.hell_gatekeeper_state);
    std::cout << ',' << +p.bridge_treasure_available;
    std::cout << ',' << +p.healing_water_available;
    std::cout << ',' << +p.cave_coins_collected;
    std::cout << ',' << +p.snow_search_count;
    std::cout << ',' << +p.has_defeated_wormoid;
    for (const auto value : p.inventory) std::cout << ',' << +value;
    for (const auto value : p.inventory_values) std::cout << ',' << +value;
    for (const auto value : p.quest_flags) std::cout << ',' << +value;
    for (const auto value : p.opened_chests) std::cout << ',' << +value;
    for (const auto value : p.spells) std::cout << ',' << +value;
    for (const auto value : p.encountered_monster_types) std::cout << ',' << +value;
    for (const auto value : p.monster_defeats) std::cout << ',' << +value;
    std::cout << ',' << p.maximum_stat() << '\n';
}
} // namespace
