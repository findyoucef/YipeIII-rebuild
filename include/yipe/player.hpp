#pragma once

#include "yipe/random.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>

namespace yipe {

enum class CharacterType : std::uint8_t {
    warrior = 1,
    rogue = 2,
    wizard = 3,
};

enum class SpecialAbility : std::uint8_t {
    none = 0,
    backstab = 1,
    frighten = 2,
    invisibility = 3,
    enrage = 4,
};

enum class HellGatekeeperState : std::int16_t {
    absent = 0,
    waiting = 1,
    paid = -1,
    defeated = -2,
};

enum class StatIncrease : std::uint8_t {
    none,
    strength,
    intelligence,
    dexterity,
};

struct LevelUpResult {
    bool leveled_up = false;
    int health_increase = 0;
    int spell_point_increase = 0;
    StatIncrease stat_increase = StatIncrease::none;
};

struct ItemUseResult {
    bool consumed = false;
    int amount = 0;
    int sound_id = 0;
    std::string message;
};

class Player {
public:
    static constexpr std::size_t inventory_size = 30;
    static constexpr std::size_t carried_inventory_size = 13;
    static constexpr std::size_t spell_count = 15;
    static constexpr std::size_t quest_flag_count = 30;
    static constexpr std::size_t monster_history_size = 42;
    static constexpr std::size_t monster_defeat_table_size = 50;

    Player();
    explicit Player(CharacterType type);

    // Original random specialty at 0x004461f0 / stat roll 0x00446380.
    void reroll_random_character(MsvcRandom& random);

    // Returns true while the player remains alive. Original address: 0x00444a00.
    bool adjust_health(int delta);

    // Reconstructs the consumable-item switch at 0x00445750.
    ItemUseResult use_inventory_item(std::size_t slot, MsvcRandom& random,
                                     bool play_sound = true, bool in_combat = false,
                                     int current_map_id = 0x11,
                                     bool alternate_map_set = false);

    [[nodiscard]] int maximum_stat() const;

    // Counts positive signed rescue slots, not the separate quest counter (0x00446c10).
    [[nodiscard]] int rescued_princess_count() const;

    // Original +0x584 counters use monster type minus one (0x00445000).
    void record_monster_defeat(int type_id);

    std::string name;
    int dexterity = 0;
    int intelligence = 0;
    int strength = 0;
    int health = 0;
    int maximum_health = 0;
    int spell_points = 0;
    int maximum_spell_points = 0;
    int ability_points = 0;
    int maximum_ability_points = 0;
    int level = 1;
    int experience = 0;
    int gold = 0;
    int weapon_rank = 2;
    int armor_rank = 2;
    std::uint16_t boots_rank = 0;
    std::uint16_t gloves_rank = 0;
    std::uint16_t helmet_rank = 1;
    std::uint16_t ring_rank = 0;
    std::uint16_t necklace_rank = 0;
    int difficulty = 2;
    // +0x67c: level-up stat bias, separate from the chosen specialty.
    int combat_style = 0;
    // +0x6cc: 0 Quickness, 1 Magic, 2 Random, 3 Combat.
    int specialty = 3;
    bool silly_mode = false;
    int equipment_upgrade_count = 0;
    int loot_roll_penalty = 2;
    int restoration_uses = 0;
    int death_count = 0;
    int quest_score = 0;
    // Includes the unattributed aggregate retained by older native saves.
    int quest_progress_total = 0;
    std::uint16_t gatekeeper_state = 0;
    std::uint16_t stamps_found = 0;
    std::uint16_t princesses_found = 0;
    std::uint16_t princess_mystery_uses = 0;
    // +0x686: 0 initial, 1 higher prices, 2 key offered, 3 bought, 4 shop exited.
    std::uint16_t miscellaneous_shop_stage = 0;
    // Original +0x6a4: only value 5 adds a High Score Code suffix; other meaning unknown.
    std::uint16_t value_06a4 = 0;
    bool has_lucky_rabbit_foot = false;
    bool has_waterproof_shoe_spray = false;
    bool has_rainbow_key = false;
    bool has_underworld_key = false;
    bool has_tree_climbing_shoes = false;
    SpecialAbility special_ability = SpecialAbility::none;
    int special_ability_duration = 0;
    int enrage_base_dexterity = 0;
    int enrage_base_intelligence = 0;
    int enrage_base_strength = 0;
    std::array<std::uint16_t, inventory_size> inventory{};
    std::array<std::uint16_t, inventory_size> inventory_values{};
    std::array<bool, quest_flag_count> quest_flags{};
    // Original +0x152 table: four one-time chest flags for each map 1..128.
    std::array<std::uint8_t, 128> opened_chests{};
    std::array<std::uint16_t, spell_count> spells{};
    std::array<bool, monster_history_size> encountered_monster_types{};
    std::array<std::int32_t, monster_defeat_table_size> monster_defeats{};
    bool monster_defeat_history_complete = true;

    bool alternate_map_set = false;
    bool has_bonus_healing_ring = false;
    int current_map_id = 0x11;
    // D8: new characters use the supplied-content limits; native saves retain theirs.
    int stat_cap_mode = 2;
    std::int16_t surface_return_map_id = 0;
    std::int16_t surface_return_region_x = 0;
    std::int16_t surface_return_region_y = 0;
    std::uint16_t random_encounter_threshold = 85;
    std::uint16_t saved_random_encounter_threshold = 85;
    std::uint16_t monster_repellent_steps = 0; // Historical name; counts exploration timer ticks.
    std::uint16_t pending_relocation = 0;
    bool entered_hell_through_secret_passage = false;
    HellGatekeeperState hell_gatekeeper_state = HellGatekeeperState::absent;
    bool bridge_treasure_available = true;
    bool healing_water_available = true;
    bool cave_coins_collected = false;
    std::uint16_t snow_search_count = 0;
    // Also retains version 14's predicate when the per-type counts are missing.
    bool has_defeated_wormoid = false;

private:
    void initialize(CharacterType type);
};

// Reconstructs the threshold check at 0x00445230 and the single-level growth
// routine at 0x00445450. A completed encounter grants at most one level.
[[nodiscard]] std::optional<int> experience_required_for_next_level(int level);
[[nodiscard]] LevelUpResult try_level_up(Player& player, MsvcRandom& random);

} // namespace yipe
