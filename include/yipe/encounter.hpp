#pragma once

#include "yipe/map.hpp"
#include "yipe/player.hpp"
#include "yipe/random.hpp"

#include <span>
#include <string_view>

namespace yipe {

struct MonsterPlacement {
    int type_id = 0;
    MapPosition position;
    int encounter_size = 0;
    bool large = false;
};

// Static placements reconstructed from the map-event initializer at 0x0041ec40.
// The catalog currently exposes the first reachable encounter while the rest of
// the event routine is being converted to generated data.
[[nodiscard]] std::span<const MonsterPlacement> original_monsters_for_map(int map_id);

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
};

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

enum class CombatOutcome { ongoing, victory, defeat };

struct CombatTurn {
    int player_damage = 0;
    int monster_damage = 0;
    CombatOutcome outcome = CombatOutcome::ongoing;
    CombatReward reward;
};

struct FleeResult {
    bool escaped = false;
    int monster_damage = 0;
    bool player_defeated = false;
};

// Reconstructs the fixed-size combat setup used by a static map placement. The
// extra random draw after monster initialization is intentional and comes from
// the combat-dialog constructor at 0x004025e0.
[[nodiscard]] Monster start_single_monster_encounter(int type_id, MsvcRandom& random);

// Current combat coverage is the first reachable Giant Beetle encounter. These
// routines preserve the original random-call order for its ordinary attack,
// counterattack, rewards, equipment-upgrade roll, and flee action.
[[nodiscard]] CombatTurn resolve_attack_turn(Player& player, Monster& monster,
                                              MsvcRandom& random);
[[nodiscard]] CombatTurn resolve_monster_turn(Player& player, const Monster& monster,
                                               MsvcRandom& random);
[[nodiscard]] FleeResult try_flee(Player& player, const Monster& monster,
                                  MsvcRandom& random);

[[nodiscard]] std::string_view equipment_upgrade_name(EquipmentUpgrade upgrade);

} // namespace yipe
