#pragma once

#include "yipe/encounter.hpp"

#include <array>
#include <cstddef>
#include <string_view>

namespace yipe {

enum class SpellUsage {
    non_combat,
    combat,
    either,
};

enum class SpellPower : int {
    low = 1,
    high = 2,
};

struct SpellDefinition {
    std::string_view name;
    std::string_view description;
    int price;
    int required_level;
    int hard_required_level;
    SpellUsage usage;
    std::array<int, 3> class_costs;
    bool high_power_costs_double;
};

// Catalog and class-specific costs recovered from the spell dialog at
// 0x00449a10/0x00449ed0/0x0044a400.
[[nodiscard]] const std::array<SpellDefinition, 12>& spell_definitions();
[[nodiscard]] int spell_required_level(const SpellDefinition& spell, int difficulty);
[[nodiscard]] int spell_point_cost(const Player& player, std::size_t spell,
                                   SpellPower power);

enum class SpellCastStatus {
    cast,
    unlearned,
    wrong_context,
    insufficient_spell_points,
    unavailable,
};

struct ExplorationSpellResult {
    SpellCastStatus status = SpellCastStatus::unavailable;
    int healing = 0;
    int repellent_duration = 0;
    bool location_changed = false;
};

// Shared player-side effects reconstructed from 0x004463f0. Combat-specific
// targeting and post-cast turns are handled by resolve_spell_turn below.
[[nodiscard]] ExplorationSpellResult cast_exploration_spell(
    Player& player, WorldLocation& location, std::size_t spell, SpellPower power,
    MsvcRandom& random);

struct CombatSpellResult {
    SpellCastStatus status = SpellCastStatus::unavailable;
    std::array<int, CombatEncounter::maximum_opponents> damage_by_slot{};
    int healing = 0;
    CombatTurn turn;
};

[[nodiscard]] CombatSpellResult resolve_spell_turn(
    Player& player, CombatEncounter& encounter, std::size_t spell,
    SpellPower power, MsvcRandom& random);

} // namespace yipe
