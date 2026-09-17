#include "yipe/spell.hpp"

#include <algorithm>
#include <stdexcept>

namespace yipe {
namespace {

constexpr std::array<SpellDefinition, 12> spells{{
    {"Return to Surface",
     "This spell returns you from an underground cave or dungeon back to the surface of Yipe!",
     50, 1, 1, SpellUsage::non_combat, {5, 5, 5}, false},
    {"Small Heal",
     "You can use this spell to heal some of your health points. Using the spell on high power increases the healing amount.",
     100, 2, 2, SpellUsage::either, {6, 10, 8}, true},
    {"Gobbler Attack",
     "This spell gobbles up some of your enemies' health. Use on high power to gobble all enemies present.",
     200, 3, 3, SpellUsage::combat, {10, 14, 12}, true},
    {"Insect Attack",
     "This spell creates an alien insect cloud that attacks your enemy. Use on high power to attack all enemies present at once.",
     300, 7, 7, SpellUsage::combat, {15, 20, 18}, true},
    {"Stinky Smell",
     "Upon casting this spell a very stinky smell will surround you and keep monsters from appearing for a short time.",
     250, 10, 10, SpellUsage::non_combat, {8, 14, 12}, true},
    {"Full Heal", "This spell will completely heal your health points.",
     500, 13, 13, SpellUsage::either, {30, 38, 33}, false},
    {"Brain Crush",
     "This spell further shrinks a monster's already small brain, causing massive damage.",
     800, 16, 18, SpellUsage::combat, {27, 34, 30}, true},
    {"Health Suction",
     "This bizarre spell will suck health out of your enemy and heal you by that same amount. Use on high power for higher damage and healing.",
     1000, 20, 22, SpellUsage::combat, {22, 28, 25}, true},
    {"Return to Yipe",
     "After casting this spell you will end up back in the town of Yipe! It can only be cast on the surface of the world.",
     500, 23, 24, SpellUsage::non_combat, {12, 15, 15}, false},
    {"Super Heal",
     "This powerful healing spell restores health and ability points, but costs many spell points to cast.",
     1000, 30, 32, SpellUsage::either, {50, 60, 55}, true},
    {"HP Shuffle",
     "This spell will cause the health points of all monsters you are currently fighting to randomly switch around.",
     750, 34, 35, SpellUsage::combat, {10, 14, 12}, false},
    {"Rabbit Replacement",
     "If there are two or three monsters one of them will be turned into a rabbit. The spell won't work with only one monster.",
     1000, 40, 42, SpellUsage::combat, {30, 36, 33}, false},
}};

bool usable_outside_combat(SpellUsage usage) {
    return usage == SpellUsage::non_combat || usage == SpellUsage::either;
}

SpellCastStatus validate_exploration_cast(const Player& player, std::size_t spell,
                                           SpellPower power) {
    if (spell >= spells.size()) {
        return SpellCastStatus::unavailable;
    }
    if (player.spells[spell] == 0) {
        return SpellCastStatus::unlearned;
    }
    if (!usable_outside_combat(spells[spell].usage)) {
        return SpellCastStatus::wrong_context;
    }
    if (player.spell_points < spell_point_cost(player, spell, power)) {
        return SpellCastStatus::insufficient_spell_points;
    }
    return SpellCastStatus::cast;
}

int apply_healing(Player& player, int amount) {
    const int actual = std::min(amount, player.maximum_health - player.health);
    player.adjust_health(actual);
    return actual;
}

} // namespace

const std::array<SpellDefinition, 12>& spell_definitions() {
    return spells;
}

int spell_required_level(const SpellDefinition& spell, int difficulty) {
    return difficulty == 3 ? spell.hard_required_level : spell.required_level;
}

int spell_point_cost(const Player& player, std::size_t spell, SpellPower power) {
    if (spell >= spells.size()) {
        throw std::out_of_range("spell index is out of range");
    }
    const std::size_t cost_index = player.combat_style == 1 ? 0
        : player.combat_style == 2 ? 1 : 2;
    int cost = spells[spell].class_costs[cost_index];
    if (power == SpellPower::high && spells[spell].high_power_costs_double) {
        cost *= 2;
    }
    return cost;
}

ExplorationSpellResult cast_exploration_spell(
    Player& player, WorldLocation& location, std::size_t spell, SpellPower power,
    MsvcRandom& random) {
    ExplorationSpellResult result;
    result.status = validate_exploration_cast(player, spell, power);
    if (result.status != SpellCastStatus::cast) {
        return result;
    }
    if (spell == 0 && (location.map_layer == 0 || location.alternate_map_set)) {
        result.status = SpellCastStatus::unavailable;
        return result;
    }
    if (spell == 8 && location.map_layer != 0) {
        result.status = SpellCastStatus::unavailable;
        return result;
    }

    const int power_value = static_cast<int>(power);
    player.spell_points -= spell_point_cost(player, spell, power);
    switch (spell) {
    case 0:
        player.pending_relocation =
            static_cast<std::uint16_t>(PendingRelocation::surface);
        result.location_changed = true;
        break;
    case 1: {
        int healing = (random.next() % 5 + 8 + player.intelligence / 2) * power_value;
        if (player.has_bonus_healing_ring) {
            healing += power_value * 6 + player.intelligence;
        }
        result.healing = apply_healing(player, healing);
        break;
    }
    case 4: {
        if (player.random_encounter_threshold < 100) {
            player.saved_random_encounter_threshold =
                player.random_encounter_threshold;
        }
        const int level_roll = random.next();
        const int intelligence_roll = random.next();
        int duration = (level_roll % (player.level / 2 + 1) + 9
                        + intelligence_roll % (player.intelligence + 1)) * power_value;
        duration = std::min(duration, 75);
        player.monster_repellent_steps =
            static_cast<std::uint16_t>(player.monster_repellent_steps + duration);
        player.random_encounter_threshold = 125;
        result.repellent_duration = duration;
        break;
    }
    case 5:
        result.healing = apply_healing(player, player.maximum_health - player.health);
        break;
    case 8:
        player.pending_relocation =
            static_cast<std::uint16_t>(PendingRelocation::yipe);
        result.location_changed = true;
        break;
    case 9: {
        int healing = (random.next() % 5 + 10 + player.intelligence / 2) * power_value
            + player.intelligence;
        if (player.has_bonus_healing_ring) {
            healing += power_value * 2 + player.intelligence;
        }
        result.healing = apply_healing(player, healing);
        if (power == SpellPower::low) {
            player.ability_points = std::min(player.ability_points + 1,
                                             player.maximum_ability_points);
        } else {
            player.ability_points = player.maximum_ability_points;
        }
        break;
    }
    default:
        result.status = SpellCastStatus::wrong_context;
        break;
    }
    return result;
}

} // namespace yipe
