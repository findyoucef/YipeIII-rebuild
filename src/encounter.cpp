#include "yipe/encounter.hpp"
#include "yipe/spell.hpp"

#include <algorithm>
#include <cstdlib>
#include <stdexcept>

namespace yipe {

bool monster_placement_contains(const MonsterPlacement& placement,
                                MapPosition position) {
    const int width = placement.occupies_two_by_two_tiles ? 2 : 1;
    return position.x >= placement.position.x
        && position.x < placement.position.x + width
        && position.y >= placement.position.y
        && position.y < placement.position.y + width;
}

int player_attack_equipment_rating(const Player& player) {
    int value = player.weapon_rank;
    if (player.helmet_rank == 5) {
        ++value;
    } else if (player.helmet_rank == 6) {
        value += 2;
    } else if (player.helmet_rank == 7) {
        value += 4;
    }
    if (player.gloves_rank > 3) {
        ++value;
    }
    if (player.necklace_rank == 5) {
        ++value;
    } else if (player.necklace_rank == 6) {
        value += 2;
    }
    return value;
}

int player_defense_equipment_rating(const Player& player) {
    int value = player.armor_rank + player.boots_rank + player.gloves_rank
        + player.helmet_rank + player.ring_rank + player.necklace_rank;
    if (player.ring_rank == 5) {
        ++value;
    }
    if (player.boots_rank == 6) {
        ++value;
    }
    return value;
}

namespace {

int roll(MsvcRandom& random, int bound) {
    return random.next() % std::max(bound, 1);
}

bool map_event_at(const MapEvents& events, MapPosition position) {
    if ((events.message.has_position() && events.message.position == position)
        || (events.overlay.has_position() && events.overlay.position == position)
        || (events.princess.has_position()
            && events.princess.position == position)) {
        return true;
    }
    for (const MapEvent& event : events.depth) {
        if (event.has_position() && event.position == position) {
            return true;
        }
    }
    for (const MapEvent& event : events.linked) {
        if (event.has_position() && event.position == position) {
            return true;
        }
    }
    return false;
}

bool roaming_tile_allowed(std::uint16_t tile, int monster_type) {
    constexpr std::array<std::uint16_t, 43> generally_allowed{
        0x2d, 0x2f, 0x08, 0x29, 0x2a, 0x33, 0x02, 0x28, 0x37, 0x32, 0x34,
        0x35, 0x38, 0x53, 0x3c, 0x2c, 0x0b, 0x16, 0x17, 0x18, 0x19, 0x1a,
        0x26, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x20, 0x27, 0x46, 0x47, 0x48,
        0x49, 0x5d, 0x5e, 0x5f, 0x52, 0x65, 0x66, 0x67, 0x68, 0x69,
    };
    if (std::find(generally_allowed.begin(), generally_allowed.end(), tile)
        != generally_allowed.end()) {
        return true;
    }
    if (monster_type >= 0x19 && monster_type <= 0x1d
        && ((tile >= 0x0d && tile <= 0x15)
            || (tile >= 0x4a && tile <= 0x51))) {
        return true;
    }
    return monster_type == 6
        && (tile == 0x21 || tile == 0x2e || tile == 0x31
            || (tile >= 0x0e && tile <= 0x15));
}

bool roaming_position_available(
    const Map& map, int monster_type, MapPosition position,
    const std::vector<MonsterPlacement>& static_placements,
    std::optional<MapPosition> blocked_monster_position = std::nullopt) {
    if (!map.contains(position) || position.y >= Map::height - 1
        || !roaming_tile_allowed(map.tile(position), monster_type)
        || map_event_at(map.events(), position)) {
        return false;
    }
    if (blocked_monster_position && position == *blocked_monster_position) {
        return false;
    }
    return std::none_of(
        static_placements.begin(), static_placements.end(),
        [position](const MonsterPlacement& placement) {
            return monster_placement_contains(placement, position);
        });
}

bool roaming_monster_allowed_impl(int environment, int monster_type,
                                  bool alternate_map_set) {
    constexpr std::array<int, 42> surface_allowed{
        0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0,
        0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    };
    constexpr std::array<int, 42> required_environment{
        0, 1, 1, 1, -1, 1, -1, 1, 2, 0, 2, 2, 2, 3, 3, 0, 3, -1, 3, 4, 3,
        4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, -1, -1, -1, -1, -1, -1,
        -1,
    };
    if (monster_type < 1
        || monster_type >= static_cast<int>(surface_allowed.size())) {
        return false;
    }

    bool allowed = environment == 0
        ? surface_allowed[static_cast<std::size_t>(monster_type)] != 0
        : required_environment[static_cast<std::size_t>(monster_type)] == 0
            || required_environment[static_cast<std::size_t>(monster_type)]
                == environment;
    if (environment == 7
        && required_environment[static_cast<std::size_t>(monster_type)] >= 0) {
        allowed = true;
    }
    if (alternate_map_set
        && (monster_type < 3 || monster_type == 0x11
            || monster_type == 0x13)) {
        allowed = false;
    }
    return allowed;
}

int roaming_monster_candidate(
    int environment, int map_id, const Player& player,
    const RoamingEncounterUnlocks& unlocks, MsvcRandom& random) {
    int monster_type = 0;
    switch (environment) {
    case 0:
        monster_type = roll(random, player.level + 2) + 1;
        break;
    case 1:
        monster_type = roll(random, 7) + 1;
        break;
    case 2:
        monster_type =
            roll(random,
                 map_id < 0x51 && !player.encountered_monster_types[36] ? 9 : 13)
            + 2;
        break;
    case 3:
        monster_type =
            roll(random,
                 map_id > 0x50 || player.encountered_monster_types[37] ? 12 : 8)
            + 9;
        break;
    case 4:
        monster_type =
            roll(random,
                 map_id > 0x50 || player.encountered_monster_types[38] ? 16 : 14)
            + 9;
        break;
    case 5:
        monster_type =
            roll(random,
                 map_id > 0x50 || player.encountered_monster_types[39] ? 16 : 13)
            + 0x0f;
        break;
    case 6:
        monster_type =
            roll(random,
                 map_id < 0x51 && !player.encountered_monster_types[40] ? 3 : 5)
            + 0x1e;
        break;
    default: {
        const int limit = unlocks.default_environment_monster_limit >= 33 ? 33
            : unlocks.default_environment_monster_limit >= 24 ? 24 : 20;
        monster_type = roll(random, limit) + 1;
        break;
    }
    }
    if (player.level < 4) {
        monster_type = roll(random, player.level + 1) + 1;
    }
    if (monster_type > 40) {
        monster_type = 0x23 - roll(random, 0x0b);
    }
    return monster_type;
}

MapPosition step_toward(MapPosition from, MapPosition target) {
    if (from.x < target.x) {
        ++from.x;
    } else if (target.x < from.x) {
        --from.x;
    }
    if (from.y < target.y) {
        ++from.y;
    } else if (target.y < from.y) {
        --from.y;
    }
    return from;
}

MapPosition random_adjacent_step(MapPosition from, MsvcRandom& random) {
    switch (roll(random, 8)) {
    case 0:
        --from.x;
        --from.y;
        break;
    case 1: ++from.x; break;
    case 2: --from.x; break;
    case 3: ++from.y; break;
    case 4: --from.y; break;
    case 5:
        ++from.x;
        --from.y;
        break;
    case 6:
        --from.x;
        ++from.y;
        break;
    case 7:
        ++from.x;
        ++from.y;
        break;
    }
    from.x = std::max(from.x, 0);
    from.y = std::max(from.y, 0);
    if (from.x > 12) {
        from.x = 11;
    }
    if (from.y > 11) {
        from.y = 11;
    }
    return from;
}

int monster_attack_bonus(const Player& player, const Monster& monster,
                         MsvcRandom& random) {
    int value = 0;
    if (monster.type_id == 9 || monster.type_id == 0x0f || monster.type_id == 0x11
        || monster.type_id == 0x29) {
        value = roll(random, player.level) + 3;
    }
    if (monster.type_id > 0x22) {
        value += 10 + player.difficulty;
    }
    if (monster.type_id < 4 && player.level > 8) {
        value += 5 + player.level;
    }
    if (player.level > 10) {
        value += 2;
    }
    if (player.level > 20) {
        value += 3;
    }
    if (player.level > 30) {
        value += 4;
    }
    if (player.level > 40) {
        value += 5;
    }
    if (monster.gatekeeper_empowered) {
        value += 10 + roll(random, 10) + player.difficulty;
    }
    if (player.difficulty == 3) {
        ++value;
    }
    return value;
}

int calculate_player_damage(const Player& player, const Monster& monster,
                            MsvcRandom& random) {
    const auto [minimum_weapon_damage, maximum_weapon_damage] =
        original_weapon_damage_range(player.weapon_rank);

    const int strength_roll = roll(random, player.strength);
    const int weapon_rank_roll = roll(random, player.weapon_rank + 1);
    const int level_roll = roll(random, std::min(player.level, 40));
    int situational_hit_bonus = player.difficulty == 0 ? 0 : 2;
    if (monster.type_id == 4 && player.level < 5) {
        situational_hit_bonus += 10 - player.level;
    }
    if (player.special_ability == SpecialAbility::invisibility
        && player.special_ability_duration > 0) {
        situational_hit_bonus += 1 + roll(random, player.special_ability_duration);
    }
    if (player.has_lucky_rabbit_foot) {
        situational_hit_bonus += 1 + roll(random, 6);
    }
    const int situational_roll = roll(random, situational_hit_bonus + 1);
    const int equipment_roll = roll(random, player_attack_equipment_rating(player) + 1);

    int monster_special_defense = 0;
    if (player.difficulty == 3 && (monster.type_id == 0x0f || monster.type_id == 0x13)) {
        monster_special_defense = 4;
    }
    if (monster.gatekeeper_empowered) {
        monster_special_defense += player.difficulty + 5 + roll(random, 20);
    }
    const int special_defense_roll = roll(random, monster_special_defense + 1);

    int difficulty_base = monster.type_id;
    if (player.difficulty == 1) {
        difficulty_base = 5;
    } else if (player.difficulty == 2) {
        difficulty_base = 3;
    } else if (player.difficulty == 3) {
        difficulty_base = 1;
    }

    int hit_score = difficulty_base - special_defense_roll + equipment_roll
        + situational_roll + level_roll + 1 + weapon_rank_roll + strength_roll;
    hit_score -= roll(random, monster.rank);
    hit_score -= roll(random, monster.defense_rating);
    hit_score -= roll(random, monster.defense_rating);
    hit_score -= roll(random, std::max(monster.type_id - player.level, 1));
    hit_score -= roll(random, monster.attack_rating);
    if (player.special_ability == SpecialAbility::enrage
        && player.special_ability_duration > 0) {
        hit_score += 1 + roll(random, 3);
    }

    const int forced_hit_roll = roll(random, 100);
    const int capped_level = std::min(player.level, 40);
    if (hit_score < 1 && forced_hit_roll + capped_level / 2 > 91) {
        hit_score = 1;
    } else if (hit_score < 1) {
        return 0;
    }

    int damage = roll(random, maximum_weapon_damage - minimum_weapon_damage + 1)
        + minimum_weapon_damage + (player.strength > 19 ? 1 : 0);
    if (hit_score > 15) {
        damage += roll(random, capped_level / 2);
    }
    if (player.difficulty == 1) {
        ++damage;
    }
    if (player.combat_style == 2 && player.level < 10 && monster.type_id > 2
        && roll(random, 100) > 65) {
        ++damage;
    }
    if (player.special_ability == SpecialAbility::enrage
        && player.special_ability_duration > 0) {
        damage += 2 + roll(random, 3);
    }
    return damage;
}

std::pair<int, int> monster_damage_range(int type_id) {
    const std::optional<MonsterDefinition> definition = original_monster_definition(type_id);
    if (!definition) {
        throw std::runtime_error("monster type is outside the recovered table");
    }
    return {definition->damage_minimum, definition->damage_maximum};
}

int low_level_defense_range(int level) {
    if (level <= 4) {
        return 3;
    }
    if (level <= 9) {
        return 6;
    }
    if (level <= 14) {
        return 10;
    }
    if (level <= 19) {
        return 15;
    }
    if (level <= 27) {
        return 20;
    }
    return level <= 37 ? 29 : 35;
}

void clear_combat_status(Player& player) {
    if (player.special_ability == SpecialAbility::enrage) {
        if (player.enrage_base_dexterity > 0) {
            player.dexterity = player.enrage_base_dexterity;
        }
        if (player.enrage_base_intelligence > 0) {
            player.intelligence = player.enrage_base_intelligence;
        }
        if (player.enrage_base_strength > 0) {
            player.strength = player.enrage_base_strength;
        }
        player.enrage_base_dexterity = 0;
        player.enrage_base_intelligence = 0;
        player.enrage_base_strength = 0;
    }
    player.special_ability_duration = 0;
}

int calculate_monster_damage(Player& player, const Monster& monster,
                             MsvcRandom& random) {
    const int armor_roll = roll(random, player.armor_rank * 2);
    const int dexterity_roll = roll(random, player.dexterity);
    const int level_defense_roll = roll(random, low_level_defense_range(player.level));
    const int type_roll = roll(random, monster.type_id);
    const int first_attack_roll = roll(random, monster.attack_rating) + player.difficulty;
    const int second_attack_roll = roll(random, monster.attack_rating);
    const int special_roll = roll(random, monster_attack_bonus(player, monster, random) + 2);
    const int equipment_roll = roll(random, player_defense_equipment_rating(player) + 1);

    const int attack_score = first_attack_roll + second_attack_roll + type_roll + special_roll
        - armor_roll - dexterity_roll - level_defense_roll - equipment_roll;
    int forced_hit_threshold = 92;
    if (player.level > 5) {
        forced_hit_threshold = 87;
    }
    if (player.level > 12) {
        forced_hit_threshold = 75;
    }
    if (player.level > 23) {
        forced_hit_threshold = 60;
    }
    const int forced_hit_roll = roll(random, 100);
    const bool missed = attack_score < 1
        && forced_hit_threshold - monster.type_id / 2 >= forced_hit_roll;
    int damage = 0;
    if (!missed) {
        const auto [minimum_damage, maximum_damage] = monster_damage_range(monster.type_id);
        damage = roll(random, maximum_damage - minimum_damage + 1) + minimum_damage;
        damage += roll(random, monster.rank + 1);
        if (monster.gatekeeper_empowered) {
            damage += 5 + roll(random, 5);
        }
        if (attack_score > 15) {
            damage += roll(random, monster.rank);
        }
        if (player.difficulty == 3) {
            ++damage;
        } else if (player.difficulty == 1 && damage > 2) {
            --damage;
        }
        if ((monster.type_id == 7 || monster.type_id == 13 || monster.type_id == 15)
            && player.level > 9) {
            damage += player.difficulty;
        }
        if (monster.type_id > 34) {
            damage += player.difficulty;
        }
        if (damage > 3 && player.level < 3) {
            --damage;
        }
        if (damage > 2 && player.level == 1 && player.health < 6) {
            damage = 1;
        }
        if (damage > 10 && player_defense_equipment_rating(player) > 10) {
            damage -= 1 + roll(random, 6);
        }
        if (damage > 20 && player_defense_equipment_rating(player) > 20) {
            damage -= 2 + roll(random, 6);
        }
        if (damage > 28 && player_defense_equipment_rating(player) > 28) {
            damage -= 3 + roll(random, 6);
        }
        if (damage > 10 && player.health < 35) {
            damage -= 3;
        }
        if (damage > 20 && player.health < 50) {
            damage -= 5;
        }
        if (player.level == 1) {
            damage = std::min(damage, 5);
        }
        damage = std::max(damage, 0);
    }

    if (player.special_ability == SpecialAbility::invisibility
        && player.special_ability_duration > 0) {
        damage = 0;
        --player.special_ability_duration;
    } else if (player.special_ability == SpecialAbility::enrage
               && player.special_ability_duration > 0) {
        if (damage > 2) {
            --damage;
        }
        --player.special_ability_duration;
        if (player.special_ability_duration < 1) {
            clear_combat_status(player);
        }
    }
    return damage;
}

int backstab_damage(const Player& player, const Monster& monster,
                    MsvcRandom& random) {
    int dexterity_range = 2;
    if (player.dexterity > 20) {
        dexterity_range = 6;
    }
    if (player.dexterity > 30) {
        dexterity_range += 2;
    }
    if (player.dexterity > 40) {
        dexterity_range += 5;
    }

    const int health_range = std::max(monster.health / 5, 1);
    int damage = roll(random, health_range) + 3 + roll(random, dexterity_range);
    damage = std::max(damage, 6);
    if (monster.health > 45) {
        damage = std::max(damage, 10);
    }
    if (monster.health > 95) {
        damage = std::max(damage, 15);
    }
    return damage;
}

void start_enrage(Player& player, MsvcRandom& random) {
    player.enrage_base_dexterity = player.dexterity;
    player.enrage_base_intelligence = player.intelligence;
    player.enrage_base_strength = player.strength;
    player.dexterity += 1 + roll(random, player.dexterity / 2);
    player.strength += 2 + roll(random, player.strength / 2);
    player.intelligence += 1 + roll(random, player.intelligence / 2);
}

EquipmentUpgrade maybe_upgrade_equipment(Player& player, int drop_threshold,
                                         MsvcRandom& random) {
    bool forced_upgrade = false;
    if (player.level >= 15 && player.weapon_rank <= 12 && player.equipment_upgrade_count == 0) {
        forced_upgrade = true;
        drop_threshold = -10;
        player.equipment_upgrade_count = 1;
    } else if (player.level > 24 && player.weapon_rank < 15
               && player.equipment_upgrade_count < 2) {
        forced_upgrade = true;
        drop_threshold = -10;
        ++player.equipment_upgrade_count;
    }

    if (drop_threshold >= roll(random, 101) - player.loot_roll_penalty * 2) {
        return EquipmentUpgrade::none;
    }
    const int choice = forced_upgrade ? 1 : roll(random, 11);
    switch (choice) {
    case 0:
    case 7:
        if (player.helmet_rank < 6) {
            ++player.helmet_rank;
            return EquipmentUpgrade::helmet;
        }
        break;
    case 1: {
        const int maximum = player.stat_cap_mode == 2 ? 29 : 23;
        if (player.weapon_rank < maximum) {
            ++player.weapon_rank;
            return EquipmentUpgrade::weapon;
        }
        break;
    }
    case 2:
        if (player.armor_rank <= 25) {
            ++player.armor_rank;
            return EquipmentUpgrade::armor;
        }
        break;
    case 3:
    case 10:
        if (player.ring_rank <= 4) {
            ++player.ring_rank;
            return EquipmentUpgrade::ring;
        }
        break;
    case 4:
        if (player.necklace_rank <= 5) {
            ++player.necklace_rank;
            return EquipmentUpgrade::necklace;
        }
        break;
    case 5:
    case 8:
        if (player.gloves_rank <= 4) {
            ++player.gloves_rank;
            return EquipmentUpgrade::gloves;
        }
        break;
    case 6:
    case 9:
        if (player.boots_rank <= 4) {
            ++player.boots_rank;
            return EquipmentUpgrade::boots;
        }
        break;
    default:
        break;
    }
    return EquipmentUpgrade::none;
}

CombatReward roll_monster_reward(const Player& player, const Monster& monster,
                                 int primary_type_id, int selected_slot,
                                 MsvcRandom& random) {
    int experience = std::max(
        (monster.rank - player.level + 1 + monster.type_id) * 10, 5);
    int gold = experience + (monster.type_id < 6 ? 1 : monster.type_id * 3 / 2)
        - roll(random, player.level) + roll(random, monster.rank);
    gold = std::max(gold, monster.rank * 9);
    if (monster.type_id > 15) {
        gold += monster.rank;
    }
    if (monster.type_id < 10) {
        gold += roll(random, monster.type_id);
    } else {
        gold += 1 + roll(random, monster.type_id) + monster.type_id;
    }
    if (monster.type_id > 7 && experience < 20) {
        experience = roll(random, monster.type_id) + monster.type_id * 3;
    }

    const int slot_bonus = primary_type_id * selected_slot + 1;
    experience += slot_bonus;
    gold += roll(random, slot_bonus);
    return {experience, gold, EquipmentUpgrade::none};
}

int encounter_size(const CombatEncounter& encounter) {
    return static_cast<int>(std::count_if(
        encounter.monsters.begin(), encounter.monsters.end(),
        [](const Monster& monster) { return monster.type_id > 0; }));
}

EquipmentUpgrade finish_encounter_rewards(Player& player,
                                          const CombatEncounter& encounter,
                                          MsvcRandom& random) {
    player.experience += encounter.pending_reward.experience;
    player.gold += encounter.pending_reward.gold;

    const int primary_type_id = encounter.monsters[0].type_id;
    const int opponents = encounter_size(encounter);
    const int opponent_penalty = opponents == 1 ? 1 : opponents == 2 ? 3 : 6;
    const int drop_base = primary_type_id < 35 || primary_type_id == 41 ? 91 : 90;
    const int drop_threshold =
        player.level - primary_type_id / 4 - primary_type_id
        + drop_base - opponent_penalty;
    return maybe_upgrade_equipment(player, drop_threshold, random);
}

Monster initialize_monster(int type_id, MsvcRandom& random) {
    const std::optional<MonsterDefinition> definition = original_monster_definition(type_id);
    if (!definition) {
        throw std::runtime_error("monster type is outside the recovered table");
    }
    Monster monster{definition->type_id, definition->name, definition->bitmap_resource_id};
    monster.health = definition->health_base
        + roll(random, definition->rank_and_health_range);
    monster.maximum_health = monster.health;
    monster.special_rating = definition->special_rating;
    monster.rank = definition->rank_and_health_range;
    monster.attack_rating = definition->attack_base + roll(random, 2);
    monster.defense_rating = definition->defense_base + roll(random, 2);
    return monster;
}

bool has_living_monster(const CombatEncounter& encounter) {
    return std::any_of(encounter.monsters.begin(), encounter.monsters.end(),
                       [](const Monster& monster) { return monster.health > 0; });
}

int first_living_monster(const CombatEncounter& encounter) {
    for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
        if (encounter.monsters[slot].health > 0) {
            return static_cast<int>(slot);
        }
    }
    return 0;
}

CombatTurn resolve_counterattacks(Player& player, const CombatEncounter& encounter,
                                  MsvcRandom& random) {
    CombatTurn result;
    for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
        const Monster& monster = encounter.monsters[slot];
        if (monster.health < 1) {
            continue;
        }
        int damage = calculate_monster_damage(player, monster, random);
        if (slot == 2) {
            damage /= 2;
        }
        result.monster_damage_by_slot[slot] = damage;
        result.monster_damage += damage;
        player.adjust_health(-damage);
    }
    if (player.health < 1) {
        result.outcome = CombatOutcome::defeat;
        clear_combat_status(player);
    }
    return result;
}

void finish_defeated_target(Player& player, CombatEncounter& encounter,
                            int selected_slot, MsvcRandom& random,
                            CombatTurn& result) {
    Monster& target = encounter.monsters[static_cast<std::size_t>(selected_slot)];
    const CombatReward reward = roll_monster_reward(
        player, target, encounter.monsters[0].type_id, selected_slot, random);
    encounter.pending_reward.experience += reward.experience;
    encounter.pending_reward.gold += reward.gold;
    if (!has_living_monster(encounter)) {
        const int primary_type = encounter.monsters[0].type_id;
        result.outcome = CombatOutcome::victory;
        result.reward = encounter.pending_reward;
        result.reward.equipment_upgrade =
            finish_encounter_rewards(player, encounter, random);
        result.level_up = try_level_up(player, random);
        if (primary_type >= 35 && primary_type != 41
            && primary_type == encounter.map_environment + 34) {
            ++player.stamps_found;
        }
        if (primary_type == 40 && encounter.map_environment == 6) {
            player.gatekeeper_state = 1;
        } else if (primary_type == 41 && player.gatekeeper_state == 1
                   && player.has_underworld_key) {
            player.gatekeeper_state = 2;
        }
        clear_combat_status(player);
    } else {
        encounter.selected_target = first_living_monster(encounter);
    }
}

int living_monster_count(const CombatEncounter& encounter) {
    return static_cast<int>(std::count_if(
        encounter.monsters.begin(), encounter.monsters.end(),
        [](const Monster& monster) { return monster.health > 0; }));
}

int heal_player(Player& player, int amount) {
    const int actual = std::min(amount, player.maximum_health - player.health);
    player.adjust_health(actual);
    return actual;
}

void shuffle_monster_health(CombatEncounter& encounter, MsvcRandom& random) {
    std::array<int, CombatEncounter::maximum_opponents> portions{};
    std::array<int, CombatEncounter::maximum_opponents> living_slots{};
    int count = 0;
    for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
        const int health = encounter.monsters[slot].health;
        if (health < 1) {
            continue;
        }
        living_slots[static_cast<std::size_t>(count++)] = static_cast<int>(slot);
        const int limit = slot == 0 ? 50 : 55;
        int portion = roll(random, std::min(health, limit)) + 5;
        if (portion >= health) {
            portion = health - 1;
        }
        portions[slot] = portion;
    }

    if (count == 2) {
        const int first = living_slots[0];
        const int second = living_slots[1];
        encounter.monsters[static_cast<std::size_t>(first)].health +=
            portions[static_cast<std::size_t>(second)]
            - portions[static_cast<std::size_t>(first)];
        encounter.monsters[static_cast<std::size_t>(second)].health +=
            portions[static_cast<std::size_t>(first)]
            - portions[static_cast<std::size_t>(second)];
    } else if (count == 3) {
        const std::array old_health{
            encounter.monsters[0].health,
            encounter.monsters[1].health,
            encounter.monsters[2].health,
        };
        encounter.monsters[0].health = old_health[0] - portions[0] + portions[1];
        encounter.monsters[1].health = old_health[1] - portions[1] + portions[2];
        encounter.monsters[2].health = old_health[2] - portions[2] + portions[0];
    }
}

void replace_monster_with_rabbit(CombatEncounter& encounter) {
    const std::size_t slot = encounter.monsters[2].health > 0 ? 2 : 1;
    Monster& monster = encounter.monsters[slot];
    const std::optional<MonsterDefinition> rabbit = original_monster_definition(1);
    if (!rabbit) {
        throw std::logic_error("rabbit monster definition is unavailable");
    }
    monster.type_id = rabbit->type_id;
    monster.name = rabbit->name;
    monster.bitmap_resource_id = rabbit->bitmap_resource_id;
    monster.special_rating = 5;
    monster.attack_rating = 5;
    monster.defense_rating = 5;
    monster.gatekeeper_empowered = false;
}

} // namespace

bool should_spawn_roaming_monster(
    const Player& player, const WorldLocation& location,
    std::uint16_t destination_tile, MsvcRandom& random) {
    int threshold = player.random_encounter_threshold;
    if (destination_tile == 8
        || (destination_tile >= 0x16 && destination_tile <= 0x1a)) {
        threshold += 11;
    } else if (destination_tile == 0x2f || destination_tile == 0x32) {
        threshold -= 10;
    }
    if (location.map_id > 0x28) {
        threshold -= 5;
    }
    if (location.map_id > 0x50) {
        threshold -= 5;
    }
    if (player.health > 0 && player.maximum_health / player.health > 7) {
        threshold += 7 - player.difficulty;
    }
    return threshold < roll(random, 100);
}

bool roaming_monster_allowed(int environment, int monster_type,
                             bool alternate_map_set) {
    return roaming_monster_allowed_impl(
        environment, monster_type, alternate_map_set);
}

std::optional<RoamingMonster> spawn_roaming_monster(
    const Map& map, const Player& player, MapPosition player_position,
    const std::vector<MonsterPlacement>& static_placements,
    const MapVisibility& visibility, const RoamingEncounterUnlocks& unlocks,
    MsvcRandom& random,
    std::optional<MapPosition> pursuing_monster_position) {
    if (map.id() == 0x11 || map.id() == 8 || map.id() == 0x1b
        || map.id() == 0x75) {
        return std::nullopt;
    }

    int monster_type = 0;
    for (int attempt = 0; attempt < 15; ++attempt) {
        const int candidate = roaming_monster_candidate(
            map.environment(), map.id(), player, unlocks, random);
        if (roaming_monster_allowed_impl(
                map.environment(), candidate, player.alternate_map_set)) {
            monster_type = candidate;
            break;
        }
    }
    if (monster_type == 0) {
        return std::nullopt;
    }

    for (int attempt = 0; attempt < 10; ++attempt) {
        const MapPosition candidate{roll(random, 12), roll(random, 9)};
        if (candidate != player_position
            && visibility.contains(candidate)
            && roaming_position_available(
                map, monster_type, candidate, static_placements,
                pursuing_monster_position)) {
            return RoamingMonster{monster_type, candidate};
        }
    }
    return std::nullopt;
}

RoamingMonsterMove move_roaming_monster(
    RoamingMonster& monster, const Map& map, MapPosition player_position,
    const std::vector<MonsterPlacement>& static_placements,
    MsvcRandom& random,
    std::optional<MapPosition> pursuing_monster_position) {
    MapPosition candidate = step_toward(monster.position, player_position);
    for (int attempt = 0; attempt < 5; ++attempt) {
        if (roll(random, 100) < 9) {
            candidate = random_adjacent_step(monster.position, random);
            continue;
        }
        if (candidate == player_position) {
            return RoamingMonsterMove::encountered_player;
        }
        if (roaming_position_available(
                map, monster.type_id, candidate, static_placements,
                pursuing_monster_position)) {
            monster.position = candidate;
            return RoamingMonsterMove::moved;
        }
        candidate = random_adjacent_step(monster.position, random);
    }
    return RoamingMonsterMove::stayed;
}

RoamingMonsterUpdate update_roaming_monster(
    std::optional<RoamingMonster>& monster, const Map& map,
    const Player& player, const WorldLocation& location,
    std::uint16_t destination_tile,
    const std::vector<MonsterPlacement>& static_placements,
    const MapVisibility& visibility, const RoamingEncounterUnlocks& unlocks,
    MsvcRandom& random,
    std::optional<MapPosition> pursuing_monster_position) {
    const bool had_monster = monster.has_value();
    const bool spawn_requested = should_spawn_roaming_monster(
        player, location, destination_tile, random);
    if (!had_monster && spawn_requested) {
        monster = spawn_roaming_monster(
            map, player, location.position, static_placements, visibility,
            unlocks, random, pursuing_monster_position);
        return monster ? RoamingMonsterUpdate::spawned
                       : RoamingMonsterUpdate::none;
    }
    if (!had_monster) {
        return RoamingMonsterUpdate::none;
    }

    switch (move_roaming_monster(
        *monster, map, location.position, static_placements, random,
        pursuing_monster_position)) {
    case RoamingMonsterMove::stayed:
        return RoamingMonsterUpdate::none;
    case RoamingMonsterMove::moved:
        return RoamingMonsterUpdate::moved;
    case RoamingMonsterMove::encountered_player:
        return RoamingMonsterUpdate::encountered_player;
    }
    throw std::logic_error("unknown roaming monster movement result");
}

std::optional<PursuingMonster> pursuing_monster_for_map(
    const Map& map, int player_level) {
    const PursuingMonsterEvent& event = map.events().pursuer;
    if (!event.has_position()) {
        return std::nullopt;
    }
    return PursuingMonster{
        event.type_for_level(player_level), event.position, event.position};
}

RoamingMonsterMove move_pursuing_monster(
    PursuingMonster& monster, const Map& map, MapPosition player_position,
    const std::vector<MonsterPlacement>& static_placements,
    const std::optional<RoamingMonster>& roaming_monster,
    const MapVisibility& visibility, MsvcRandom& random) {
    if (!visibility.contains(monster.position)) {
        return RoamingMonsterMove::stayed;
    }
    const std::optional<MapPosition> roaming_position =
        roaming_monster
        ? std::optional<MapPosition>{roaming_monster->position}
        : std::nullopt;
    for (int attempt = 0; attempt < 5; ++attempt) {
        const MapPosition candidate = roll(random, 100) < 55
            ? random_adjacent_step(monster.position, random)
            : step_toward(monster.position, player_position);
        if (candidate == player_position) {
            return RoamingMonsterMove::encountered_player;
        }
        if (std::abs(candidate.x - monster.origin.x) > 2
            || std::abs(candidate.y - monster.origin.y) > 2) {
            continue;
        }
        if (roaming_position_available(
                map, monster.type_id, candidate, static_placements,
                roaming_position)) {
            monster.position = candidate;
            return RoamingMonsterMove::moved;
        }
    }
    return RoamingMonsterMove::stayed;
}

CombatEncounter start_monster_encounter(int type_id, int opponent_count,
                                        MsvcRandom& random, int player_level) {
    EncounterSetup setup;
    setup.player_level = player_level;
    return start_monster_encounter(type_id, opponent_count, random, setup);
}

CombatEncounter start_monster_encounter(int type_id, int opponent_count,
                                        MsvcRandom& random,
                                        const EncounterSetup& setup) {
    if (opponent_count < 0
        || opponent_count > static_cast<int>(CombatEncounter::maximum_opponents)) {
        throw std::invalid_argument("opponent count must be between zero and three");
    }

    const bool generated_count = opponent_count == 0;
    if (generated_count) {
        if (type_id + 3 < setup.player_level || roll(random, 100) > 80) {
            opponent_count = roll(random, 3) + 1;
        }
        if (opponent_count == 0 && setup.player_level < 3) {
            opponent_count = 1;
        }
    }

    CombatEncounter encounter;
    encounter.map_environment = setup.map_environment;
    encounter.monsters[0] = initialize_monster(type_id, random);
    int variant_roll = roll(random, 100);
    if (!generated_count) {
        variant_roll = 0;
    }

    if (opponent_count == 2) {
        int second_type_id = type_id;
        if (variant_roll >= 86 && type_id <= 40) {
            second_type_id = type_id + 1;
        } else if (variant_roll > 80 && type_id > 2) {
            second_type_id = type_id - 1;
        }
        encounter.monsters[1] = initialize_monster(second_type_id, random);
    } else if (opponent_count == 3) {
        encounter.monsters[1] = initialize_monster(type_id, random);
        int third_type_id = type_id;
        if (variant_roll >= 66 && type_id <= 40) {
            third_type_id = type_id + 1;
        } else if (variant_roll >= 56 && type_id >= 3) {
            third_type_id = type_id - 1;
        }
        encounter.monsters[2] = initialize_monster(third_type_id, random);
    }

    if (type_id > 34) {
        int companion_type_1 = setup.companion_type_1;
        int companion_type_2 = setup.companion_type_2;
        bool replace_companions = companion_type_1 != 0 || companion_type_2 != 0;
        if (type_id == 41 && setup.gatekeeper_state != 0) {
            encounter.monsters[0].gatekeeper_empowered = true;
            encounter.monsters[0].health = encounter.monsters[0].health * 4
                + setup.player_level * 6;
            companion_type_1 = setup.difficulty + 37;
            companion_type_2 = 37;
            replace_companions = true;
        }

        if (replace_companions) {
            encounter.monsters[1] = companion_type_1 > 0
                ? initialize_monster(companion_type_1, random) : Monster{};
            encounter.monsters[2] = companion_type_2 > 0
                ? initialize_monster(companion_type_2, random) : Monster{};
        }
        encounter.monsters[0].health += setup.player_level;
        if (setup.difficulty == 3) {
            encounter.monsters[0].health += setup.player_level;
        }
        encounter.monsters[0].maximum_health = encounter.monsters[0].health;
    }
    return encounter;
}

CombatEncounter start_monster_encounter(const MonsterPlacement& placement,
                                        Player& player, MsvcRandom& random,
                                        int map_environment) {
    CombatEncounter encounter = start_monster_encounter(
        placement.type_id, placement.encounter_size, random,
        {player.level, player.difficulty, placement.companion_type_1,
         placement.companion_type_2, player.gatekeeper_state, map_environment});
    for (const Monster& monster : encounter.monsters) {
        if (monster.type_id >= 0
            && monster.type_id
                < static_cast<int>(player.encountered_monster_types.size())) {
            player.encountered_monster_types[
                static_cast<std::size_t>(monster.type_id)] = true;
        }
    }
    return encounter;
}

Monster start_single_monster_encounter(int type_id, MsvcRandom& random) {
    return start_monster_encounter(type_id, 1, random).monsters[0];
}

CombatTurn resolve_attack_turn(Player& player, Monster& monster, MsvcRandom& random) {
    CombatEncounter encounter;
    encounter.monsters[0] = monster;
    CombatTurn result = resolve_attack_turn(player, encounter, random);
    monster = encounter.monsters[0];
    return result;
}

CombatTurn resolve_attack_turn(Player& player, CombatEncounter& encounter,
                               MsvcRandom& random) {
    if (player.health < 1 || encounter.selected_target < 0
        || encounter.selected_target
            >= static_cast<int>(CombatEncounter::maximum_opponents)
        || encounter.monsters[static_cast<std::size_t>(encounter.selected_target)].health < 1) {
        throw std::logic_error("cannot attack after combat has ended");
    }

    CombatTurn result;
    Monster& target =
        encounter.monsters[static_cast<std::size_t>(encounter.selected_target)];
    result.player_damage = calculate_player_damage(player, target, random);
    target.health -= result.player_damage;
    const bool target_defeated = target.health < 1;
    const int selected_slot = encounter.selected_target;

    if (has_living_monster(encounter)) {
        CombatTurn counterattacks = resolve_counterattacks(player, encounter, random);
        result.monster_damage = counterattacks.monster_damage;
        result.monster_damage_by_slot = counterattacks.monster_damage_by_slot;
        if (counterattacks.outcome == CombatOutcome::defeat) {
            result.outcome = CombatOutcome::defeat;
            return result;
        }
    }

    if (target_defeated) {
        finish_defeated_target(player, encounter, selected_slot, random, result);
    }
    return result;
}

CombatTurn resolve_special_ability_turn(Player& player, Monster& monster,
                                         MsvcRandom& random) {
    CombatEncounter encounter;
    encounter.monsters[0] = monster;
    CombatTurn result = resolve_special_ability_turn(player, encounter, random);
    monster = encounter.monsters[0];
    return result;
}

CombatTurn resolve_special_ability_turn(Player& player, CombatEncounter& encounter,
                                         MsvcRandom& random) {
    if (player.health < 1 || encounter.selected_target < 0
        || encounter.selected_target
            >= static_cast<int>(CombatEncounter::maximum_opponents)
        || encounter.monsters[static_cast<std::size_t>(encounter.selected_target)].health < 1) {
        throw std::logic_error("cannot use an ability after combat has ended");
    }

    CombatTurn result;
    if (player.special_ability == SpecialAbility::none || player.ability_points < 3) {
        return result;
    }

    const int selected_slot = encounter.selected_target;
    Monster& target = encounter.monsters[static_cast<std::size_t>(selected_slot)];
    switch (player.special_ability) {
    case SpecialAbility::backstab:
        result.player_damage = backstab_damage(player, target, random);
        player.ability_points -= 3;
        target.health -= result.player_damage;
        if (target.health < 1) {
            finish_defeated_target(player, encounter, selected_slot, random, result);
        }
        break;
    case SpecialAbility::frighten: {
        Monster& third = encounter.monsters[2];
        if (third.health < 1) {
            return result;
        }
        encounter.pending_reward.experience += third.attack_rating + 2;
        player.ability_points -= 3;
        third.type_id = 0;
        third.health = 0;
        if (selected_slot == 2) {
            if (has_living_monster(encounter)) {
                encounter.selected_target = first_living_monster(encounter);
            } else {
                result.outcome = CombatOutcome::victory;
                result.reward = encounter.pending_reward;
                result.reward.equipment_upgrade =
                    finish_encounter_rewards(player, encounter, random);
                result.level_up = try_level_up(player, random);
                clear_combat_status(player);
            }
        }
        break;
    }
    case SpecialAbility::invisibility:
        player.special_ability_duration = 3 + roll(random, player.dexterity / 5);
        if (encounter.monsters[1].health > 0) {
            player.special_ability_duration *= 2;
        }
        if (encounter.monsters[2].health > 0) {
            player.special_ability_duration *= 4;
        }
        player.ability_points -= 3;
        break;
    case SpecialAbility::enrage:
        player.special_ability_duration = 3 + roll(random, player.strength / 4);
        if (encounter.monsters[1].health > 0) {
            player.special_ability_duration *= 2;
        }
        if (encounter.monsters[2].health > 0) {
            player.special_ability_duration += 6;
        }
        start_enrage(player, random);
        player.ability_points -= 3;
        break;
    case SpecialAbility::none:
        break;
    }
    return result;
}

CombatTurn resolve_monster_turn(Player& player, const Monster& monster, MsvcRandom& random) {
    CombatEncounter encounter;
    encounter.monsters[0] = monster;
    return resolve_monster_turn(player, encounter, random);
}

CombatTurn resolve_monster_turn(Player& player, const CombatEncounter& encounter,
                                MsvcRandom& random) {
    if (player.health < 1 || !has_living_monster(encounter)) {
        throw std::logic_error("cannot continue a combat turn after combat has ended");
    }
    return resolve_counterattacks(player, encounter, random);
}

FleeResult try_flee(Player& player, const Monster& monster, MsvcRandom& random) {
    CombatEncounter encounter;
    encounter.monsters[0] = monster;
    return try_flee(player, encounter, random);
}

FleeResult try_flee(Player& player, const CombatEncounter& encounter,
                    MsvcRandom& random) {
    if (player.health < 1 || !has_living_monster(encounter)) {
        throw std::logic_error("cannot flee after combat has ended");
    }

    const Monster& primary_monster = encounter.monsters[0];
    const int capped_level = std::min(player.level, 35);
    int chance = capped_level - primary_monster.type_id + 68;
    if (player.difficulty == 1) {
        chance = capped_level - primary_monster.type_id + 86;
    } else if (player.difficulty == 3) {
        chance = capped_level - primary_monster.type_id + 63;
    }
    if (capped_level < 4) {
        chance += 50;
    }
    if (player.health < 10) {
        chance += 5;
    }
    if (roll(random, 100) <= chance) {
        player.gold = std::max(player.gold + 1 - primary_monster.type_id, 0);
        clear_combat_status(player);
        return {true, 0, false, {}};
    }

    FleeResult result;
    for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
        const Monster& monster = encounter.monsters[slot];
        if (monster.health < 1) {
            continue;
        }
        int damage = calculate_monster_damage(player, monster, random);
        if (slot == 2) {
            damage /= 2;
        }
        if (player.level < 6 && player.health < 7) {
            damage = std::max(damage - 2, 1);
        }
        if (player.health - damage < 0) {
            damage /= 2;
        }
        if (damage > 10) {
            damage -= 3;
        }
        damage = std::max(damage, 1);
        result.monster_damage_by_slot[slot] = damage;
        result.monster_damage += damage;
        player.adjust_health(-damage);
    }
    result.player_defeated = player.health < 1;
    if (result.player_defeated) {
        clear_combat_status(player);
    }
    return result;
}

CombatSpellResult resolve_spell_turn(Player& player, CombatEncounter& encounter,
                                     std::size_t spell, SpellPower power,
                                     MsvcRandom& random) {
    CombatSpellResult result;
    const auto& definitions = spell_definitions();
    if (spell >= definitions.size()) {
        return result;
    }
    if (player.spells[spell] == 0) {
        result.status = SpellCastStatus::unlearned;
        return result;
    }
    if (definitions[spell].usage == SpellUsage::non_combat) {
        result.status = SpellCastStatus::wrong_context;
        return result;
    }
    const int cost = spell_point_cost(player, spell, power);
    if (player.spell_points < cost) {
        result.status = SpellCastStatus::insufficient_spell_points;
        return result;
    }
    if ((spell == 10 || spell == 11) && living_monster_count(encounter) < 2) {
        return result;
    }
    if (player.health < 1 || !has_living_monster(encounter)
        || encounter.selected_target < 0
        || encounter.selected_target
            >= static_cast<int>(CombatEncounter::maximum_opponents)
        || encounter.monsters[static_cast<std::size_t>(encounter.selected_target)].health < 1) {
        throw std::logic_error("cannot cast a spell after combat has ended");
    }

    result.status = SpellCastStatus::cast;
    player.spell_points -= cost;
    const int power_value = static_cast<int>(power);
    int damage = 0;
    switch (spell) {
    case 1: {
        int healing = (roll(random, 5) + 8 + player.intelligence / 2) * power_value;
        if (player.has_bonus_healing_ring) {
            healing += power_value * 6 + player.intelligence;
        }
        result.healing = heal_player(player, healing);
        break;
    }
    case 2:
        damage = power_value + player.intelligence / 2
            + (random.next() & 3) + (random.next() & 3);
        break;
    case 3:
        damage = power_value + roll(random, 6) + player.intelligence + 14;
        break;
    case 5:
        result.healing = heal_player(player, player.maximum_health - player.health);
        break;
    case 6: {
        const int intelligence_roll = random.next();
        const int ten_roll = random.next();
        damage = player.intelligence / 3 + ten_roll % 10 + player.intelligence
            + intelligence_roll % std::max(player.intelligence, 1) + 30
            + power_value * 2;
        break;
    }
    case 7:
        damage = roll(random, power_value + player.intelligence) + 20
            + player.intelligence / 2;
        result.healing = heal_player(player, damage * power_value);
        break;
    case 9: {
        int healing = (roll(random, 5) + 10 + player.intelligence / 2) * power_value
            + player.intelligence;
        if (player.has_bonus_healing_ring) {
            healing += power_value * 2 + player.intelligence;
        }
        result.healing = heal_player(player, healing);
        if (power == SpellPower::low) {
            player.ability_points = std::min(player.ability_points + 1,
                                             player.maximum_ability_points);
        } else {
            player.ability_points = player.maximum_ability_points;
        }
        break;
    }
    case 10:
        shuffle_monster_health(encounter, random);
        break;
    case 11:
        replace_monster_with_rabbit(encounter);
        break;
    default:
        break;
    }

    if (damage > 0) {
        if (power == SpellPower::low) {
            const int slot = encounter.selected_target;
            Monster& target = encounter.monsters[static_cast<std::size_t>(slot)];
            int applied = damage;
            if (target.special_rating > 0) {
                applied -= std::max(applied / target.special_rating, 1);
            }
            applied = std::max(applied, 1);
            result.damage_by_slot[static_cast<std::size_t>(slot)] = applied;
            result.turn.player_damage += applied;
            target.health -= applied;
            if (target.health < 1) {
                finish_defeated_target(player, encounter, slot, random, result.turn);
            }
        } else {
            int remaining_damage = damage;
            for (std::size_t slot = 0; slot < encounter.monsters.size(); ++slot) {
                Monster& target = encounter.monsters[slot];
                if (target.health < 1) {
                    continue;
                }
                if (target.special_rating > 0) {
                    const int minimum_reduction = slot == 1 ? 1 : 2;
                    remaining_damage -= std::max(
                        remaining_damage / target.special_rating, minimum_reduction);
                }
                remaining_damage = std::max(remaining_damage, slot == 1 ? 2 : 1);
                const int applied = slot == 1
                    ? remaining_damage : remaining_damage + roll(random, 3);
                result.damage_by_slot[slot] = applied;
                result.turn.player_damage += applied;
                target.health -= applied;
                if (target.health < 1) {
                    finish_defeated_target(player, encounter, static_cast<int>(slot),
                                           random, result.turn);
                }
            }
        }
    }

    if (result.turn.outcome != CombatOutcome::victory && has_living_monster(encounter)) {
        const CombatTurn counterattacks = resolve_counterattacks(player, encounter, random);
        result.turn.monster_damage = counterattacks.monster_damage;
        result.turn.monster_damage_by_slot = counterattacks.monster_damage_by_slot;
        result.turn.outcome = counterattacks.outcome;
    }
    return result;
}

std::string_view equipment_upgrade_name(EquipmentUpgrade upgrade) {
    switch (upgrade) {
    case EquipmentUpgrade::weapon:
        return "weapon";
    case EquipmentUpgrade::armor:
        return "armor";
    case EquipmentUpgrade::boots:
        return "boots";
    case EquipmentUpgrade::gloves:
        return "gloves";
    case EquipmentUpgrade::helmet:
        return "helmet";
    case EquipmentUpgrade::ring:
        return "ring";
    case EquipmentUpgrade::necklace:
        return "necklace";
    case EquipmentUpgrade::none:
        return {};
    }
    return {};
}

} // namespace yipe
