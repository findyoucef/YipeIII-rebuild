#pragma once

#include "yipe/map.hpp"
#include "yipe/player.hpp"
#include "yipe/random.hpp"

#include <array>
#include <optional>
#include <string_view>
#include <utility>
#include <vector>

namespace yipe {

struct MonsterPlacement {
    int type_id = 0;
    MapPosition position;
    int slot = 0;
    int encounter_size = 0;
    int companion_type_1 = 0;
    int companion_type_2 = 0;
    bool flag_36c = false;
    bool flag_371 = false;
};

// Static placements reconstructed from the map-event initializer at 0x0041ec40.
// A few original records derive monster type or group size from difficulty and
// player level, so those values are supplied when materializing a map's records.
[[nodiscard]] std::vector<MonsterPlacement> original_monsters_for_map(
    int map_id, int difficulty = 2, int player_level = 1);

struct Monster {
    int type_id = 0;
    std::string_view name;
    int bitmap_resource_id = 0;
    int health = 0;
    int maximum_health = 0;
    int special_rating = 0; // Original combat-record field +0x300.
    int rank = 0;           // Original combat-record field +0x304.
    int attack_rating = 0;  // Original combat-record field +0x308.
    int defense_rating = 0; // Original combat-record field +0x30c.
    bool gatekeeper_empowered = false;
};

struct MonsterDefinition {
    int type_id = 0;
    std::string_view name;
    int string_resource_id = 0;
    int bitmap_resource_id = 0;
    int health_base = 0;
    int rank_and_health_range = 0;
    int attack_base = 0;
    int defense_base = 0;
    int special_rating = 0;
    int damage_minimum = 0;
    int damage_maximum = 0;
};

// Stats come from 0x00405640, damage ranges from 0x004042e0, names from
// resources 230-270, and matching bitmaps from resources 174-214.
[[nodiscard]] std::optional<MonsterDefinition> original_monster_definition(int type_id);

// The 42 fixed weapon damage ranges reconstructed from 0x00417ab0.
[[nodiscard]] std::pair<int, int> original_weapon_damage_range(int weapon_rank);

// Aggregate equipment ratings used by the combat calculations at 0x004451e0
// (player attacks) and 0x004451a0 (monster attacks).
[[nodiscard]] int player_attack_equipment_rating(const Player& player);
[[nodiscard]] int player_defense_equipment_rating(const Player& player);

enum class EquipmentUpgrade {
    none,
    weapon,
    armor,
    boots,
    gloves,
    helmet,
    ring,
    necklace,
};

struct CombatReward {
    int experience = 0;
    int gold = 0;
    EquipmentUpgrade equipment_upgrade = EquipmentUpgrade::none;
};

struct CombatEncounter {
    static constexpr std::size_t maximum_opponents = 3;

    std::array<Monster, maximum_opponents> monsters{};
    int selected_target = 0;
    CombatReward pending_reward;
    int map_environment = -1;
};

struct EncounterSetup {
    int player_level = 1;
    int difficulty = 2;
    int companion_type_1 = 0;
    int companion_type_2 = 0;
    int gatekeeper_state = 0;
    int map_environment = -1;
};

enum class CombatOutcome { ongoing, victory, defeat };

struct CombatTurn {
    int player_damage = 0;
    int monster_damage = 0;
    std::array<int, CombatEncounter::maximum_opponents> monster_damage_by_slot{};
    CombatOutcome outcome = CombatOutcome::ongoing;
    CombatReward reward;
    LevelUpResult level_up;
};

struct FleeResult {
    bool escaped = false;
    int monster_damage = 0;
    bool player_defeated = false;
    std::array<int, CombatEncounter::maximum_opponents> monster_damage_by_slot{};
};

// Reconstructs the combat setup used by a static map placement. An opponent
// count of zero selects the original level/random-generated group size. The
// extra random draw after primary-monster initialization is intentional and
// comes from the combat-dialog constructor at 0x004025e0.
[[nodiscard]] CombatEncounter start_monster_encounter(
    int type_id, int opponent_count, MsvcRandom& random, int player_level = 1);
[[nodiscard]] CombatEncounter start_monster_encounter(
    int type_id, int opponent_count, MsvcRandom& random, const EncounterSetup& setup);
[[nodiscard]] CombatEncounter start_monster_encounter(
    const MonsterPlacement& placement, const Player& player, MsvcRandom& random,
    int map_environment = -1);
[[nodiscard]] Monster start_single_monster_encounter(int type_id, MsvcRandom& random);

// Common combat formulas support one to three opponents from the recovered
// definition catalog, including the four original special abilities.
[[nodiscard]] CombatTurn resolve_attack_turn(Player& player, Monster& monster,
                                              MsvcRandom& random);
[[nodiscard]] CombatTurn resolve_attack_turn(Player& player, CombatEncounter& encounter,
                                              MsvcRandom& random);
[[nodiscard]] CombatTurn resolve_special_ability_turn(
    Player& player, Monster& monster, MsvcRandom& random);
[[nodiscard]] CombatTurn resolve_special_ability_turn(
    Player& player, CombatEncounter& encounter, MsvcRandom& random);
[[nodiscard]] CombatTurn resolve_monster_turn(Player& player, const Monster& monster,
                                               MsvcRandom& random);
[[nodiscard]] CombatTurn resolve_monster_turn(
    Player& player, const CombatEncounter& encounter, MsvcRandom& random);
[[nodiscard]] FleeResult try_flee(Player& player, const Monster& monster,
                                  MsvcRandom& random);
[[nodiscard]] FleeResult try_flee(Player& player, const CombatEncounter& encounter,
                                  MsvcRandom& random);

[[nodiscard]] std::string_view equipment_upgrade_name(EquipmentUpgrade upgrade);

} // namespace yipe
