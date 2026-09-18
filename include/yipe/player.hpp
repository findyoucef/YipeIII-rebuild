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

    Player();
    explicit Player(CharacterType type);

    // Returns true while the player remains alive. Original address: 0x00444a00.
    bool adjust_health(int delta);

    // Reconstructs the consumable-item switch at 0x00445750.
    ItemUseResult use_inventory_item(std::size_t slot, MsvcRandom& random,
                                     bool play_sound = true, bool in_combat = false,
                                     int current_map_id = 0x11,
                                     bool alternate_map_set = false);

    [[nodiscard]] int maximum_stat() const;

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
    int combat_style = 2;
    int equipment_upgrade_count = 0;
    int loot_roll_penalty = 2;
    int restoration_uses = 0;
    int death_count = 0;
    int quest_score = 0;
    int quest_progress_total = 0;
    std::uint16_t gatekeeper_state = 0;
    std::uint16_t stamps_found = 0;
    std::uint16_t princesses_found = 0;
    std::uint16_t princess_mystery_uses = 0;
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
    std::array<std::uint16_t, spell_count> spells{};
    std::array<bool, monster_history_size> encountered_monster_types{};

    bool alternate_map_set = false;
    bool has_bonus_healing_ring = false;
    int current_map_id = 0x11;
    int stat_cap_mode = 0;
    std::int16_t surface_return_map_id = 0;
    std::int16_t surface_return_region_x = 0;
    std::int16_t surface_return_region_y = 0;
    std::uint16_t random_encounter_threshold = 85;
    std::uint16_t saved_random_encounter_threshold = 85;
    std::uint16_t monster_repellent_steps = 0;
    std::uint16_t pending_relocation = 0;
    bool entered_hell_through_secret_passage = false;
    HellGatekeeperState hell_gatekeeper_state = HellGatekeeperState::absent;

private:
    void initialize(CharacterType type);
};

// Reconstructs the threshold check at 0x00445230 and the single-level growth
// routine at 0x00445450. A completed encounter grants at most one level.
[[nodiscard]] std::optional<int> experience_required_for_next_level(int level);
[[nodiscard]] LevelUpResult try_level_up(Player& player, MsvcRandom& random);

} // namespace yipe
