#include "yipe/encounter.hpp"

#include <algorithm>
#include <stdexcept>

namespace yipe {
namespace {

int roll(MsvcRandom& random, int bound) {
    return random.next() % std::max(bound, 1);
}

int player_attack_equipment(const Player& player) {
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

int player_defense_equipment(const Player& player) {
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
    const int situational_hit_bonus = player.difficulty == 0 ? 0 : 2;
    const int situational_roll = roll(random, situational_hit_bonus + 1);
    const int equipment_roll = roll(random, player_attack_equipment(player) + 1);

    int monster_special_defense = 0;
    if (player.difficulty == 3 && (monster.type_id == 0x0f || monster.type_id == 0x13)) {
        monster_special_defense = 4;
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

int calculate_monster_damage(const Player& player, const Monster& monster,
                             MsvcRandom& random) {
    const int armor_roll = roll(random, player.armor_rank * 2);
    const int dexterity_roll = roll(random, player.dexterity);
    const int level_defense_roll = roll(random, low_level_defense_range(player.level));
    const int type_roll = roll(random, monster.type_id);
    const int first_attack_roll = roll(random, monster.attack_rating) + player.difficulty;
    const int second_attack_roll = roll(random, monster.attack_rating);
    const int special_roll = roll(random, monster_attack_bonus(player, monster, random) + 2);
    const int equipment_roll = roll(random, player_defense_equipment(player) + 1);

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
    if (attack_score < 1 && forced_hit_threshold - monster.type_id / 2 >= forced_hit_roll) {
        return 0;
    }

    const auto [minimum_damage, maximum_damage] = monster_damage_range(monster.type_id);
    int damage = roll(random, maximum_damage - minimum_damage + 1) + minimum_damage;
    damage += roll(random, monster.rank + 1);
    if (attack_score > 15) {
        damage += roll(random, monster.rank);
    }
    if (player.difficulty == 3) {
        ++damage;
    } else if (player.difficulty == 1 && damage > 2) {
        --damage;
    }
    if (damage > 3 && player.level < 3) {
        --damage;
    }
    if (damage > 2 && player.level == 1 && player.health < 6) {
        damage = 1;
    }
    if (damage > 10 && player_defense_equipment(player) > 10) {
        damage -= 1 + roll(random, 6);
    }
    if (damage > 20 && player_defense_equipment(player) > 20) {
        damage -= 2 + roll(random, 6);
    }
    if (damage > 28 && player_defense_equipment(player) > 28) {
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
    return std::max(damage, 0);
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
        const int maximum = player.unknown_0654_stat_cap_mode == 2 ? 29 : 23;
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

CombatReward award_victory(Player& player, const Monster& monster, MsvcRandom& random) {
    const int base_experience = std::max(
        (monster.rank - player.level + 1 + monster.type_id) * 10, 5);
    int gold = base_experience + (monster.type_id < 6 ? 1 : monster.type_id * 3 / 2)
        - roll(random, player.level) + roll(random, monster.rank);
    gold = std::max(gold, monster.rank * 9);
    if (monster.type_id < 10) {
        gold += roll(random, monster.type_id);
    } else {
        gold += 1 + roll(random, monster.type_id) + monster.type_id;
    }

    const int experience = base_experience + 1;
    gold += roll(random, 1);
    player.experience += experience;
    player.gold += gold;

    const int drop_threshold = player.level - monster.type_id / 4 - monster.type_id;
    return {experience, gold,
            maybe_upgrade_equipment(player, drop_threshold + 90, random)};
}

} // namespace

Monster start_single_monster_encounter(int type_id, MsvcRandom& random) {
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
    static_cast<void>(roll(random, 100));
    return monster;
}

CombatTurn resolve_attack_turn(Player& player, Monster& monster, MsvcRandom& random) {
    if (player.health < 1 || monster.health < 1) {
        throw std::logic_error("cannot attack after combat has ended");
    }

    CombatTurn result;
    result.player_damage = calculate_player_damage(player, monster, random);
    monster.health -= result.player_damage;
    if (monster.health < 1) {
        result.outcome = CombatOutcome::victory;
        result.reward = award_victory(player, monster, random);
        return result;
    }

    result.monster_damage = calculate_monster_damage(player, monster, random);
    player.adjust_health(-result.monster_damage);
    if (player.health < 1) {
        result.outcome = CombatOutcome::defeat;
    }
    return result;
}

CombatTurn resolve_monster_turn(Player& player, const Monster& monster, MsvcRandom& random) {
    if (player.health < 1 || monster.health < 1) {
        throw std::logic_error("cannot continue a combat turn after combat has ended");
    }

    CombatTurn result;
    result.monster_damage = calculate_monster_damage(player, monster, random);
    player.adjust_health(-result.monster_damage);
    if (player.health < 1) {
        result.outcome = CombatOutcome::defeat;
    }
    return result;
}

FleeResult try_flee(Player& player, const Monster& monster, MsvcRandom& random) {
    const int capped_level = std::min(player.level, 35);
    int chance = capped_level - monster.type_id + 68;
    if (player.difficulty == 1) {
        chance = capped_level - monster.type_id + 86;
    } else if (player.difficulty == 3) {
        chance = capped_level - monster.type_id + 63;
    }
    if (capped_level < 4) {
        chance += 50;
    }
    if (player.health < 10) {
        chance += 5;
    }
    if (roll(random, 100) <= chance) {
        player.gold = std::max(player.gold + 1 - monster.type_id, 0);
        return {true, 0, false};
    }

    int damage = calculate_monster_damage(player, monster, random);
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
    player.adjust_health(-damage);
    return {false, damage, player.health < 1};
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
