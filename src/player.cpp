#include "yipe/player.hpp"

#include <algorithm>
#include <array>
#include <stdexcept>

namespace yipe {

Player::Player() {
    initialize(CharacterType::warrior);
}

Player::Player(CharacterType type) {
    initialize(type);
}

void Player::initialize(CharacterType type) {
    name = "Yipie";
    level = 1;
    experience = 0;
    gold = 0;
    weapon_rank = 2;
    armor_rank = 2;
    boots_rank = 0;
    gloves_rank = 0;
    helmet_rank = 1;
    ring_rank = 0;
    necklace_rank = 0;
    difficulty = 2;
    equipment_upgrade_count = 0;
    loot_roll_penalty = 2;
    restoration_uses = 0;
    has_tree_climbing_shoes = false;
    has_rainbow_key = false;
    has_underworld_key = false;
    unknown_0674 = false;
    inventory.fill(0);
    inventory[0] = 1;
    spells.fill(0);
    spells[0] = 1;

    switch (type) {
    case CharacterType::warrior:
        combat_style = 2;
        strength = 16;
        dexterity = 13;
        intelligence = 10;
        health = 13;
        spell_points = 0;
        ability_points = 5;
        break;
    case CharacterType::rogue:
        combat_style = 3;
        strength = 11;
        dexterity = 15;
        intelligence = 12;
        health = 13;
        spell_points = 0;
        ability_points = 6;
        break;
    case CharacterType::wizard:
        combat_style = 1;
        strength = 11;
        dexterity = 12;
        intelligence = 16;
        health = 10;
        spell_points = 0;
        ability_points = 5;
        break;
    }

    maximum_health = health;
    maximum_spell_points = spell_points;
    maximum_ability_points = ability_points;
}

bool Player::adjust_health(int delta) {
    health += delta;
    health = std::min(health, maximum_health);
    return health > 0;
}

int Player::maximum_stat() const {
    if (unknown_0654_stat_cap_mode == 1) {
        return 40;
    }
    if (unknown_0654_stat_cap_mode == 2) {
        return 50;
    }
    return 30;
}

std::optional<int> experience_required_for_next_level(int level) {
    constexpr std::array thresholds{
        0, 200, 500, 1000, 1500, 2000, 2500, 3000, 4000, 5000,
        6000, 7100, 8200, 9300, 10400, 11600, 13250, 15100, 17200,
        19200, 21750, 23250, 25000, 27000, 30000, 33500, 37500, 42000,
    };
    if (level < 1 || level >= 100) {
        return std::nullopt;
    }
    if (level < static_cast<int>(thresholds.size())) {
        return thresholds[static_cast<std::size_t>(level)] + 1;
    }
    switch (level) {
    case 28:
        return 47001;
    case 29:
        return 52001;
    case 30:
        return 57001;
    case 31:
        return 63001;
    case 32:
        return 75001;
    case 33:
        return 90001;
    default:
        break;
    }
    if (level < 55) {
        return level * 3500 + 1;
    }
    if (level < 80) {
        return level * 4000 + 1;
    }
    return level * 5000 + 1;
}

LevelUpResult try_level_up(Player& player, MsvcRandom& random) {
    const std::optional<int> required = experience_required_for_next_level(player.level);
    if (!required || player.experience < *required) {
        return {};
    }

    LevelUpResult result;
    result.leveled_up = true;
    const int old_level = player.level;
    const int health_roll = random.next() % 4;
    result.health_increase = health_roll + 5;
    if (old_level > 10 && player.intelligence > 20) {
        result.health_increase = health_roll + 6;
    }
    if (old_level > 8) {
        ++result.health_increase;
    }
    if (old_level < 3 && player.difficulty == 1) {
        ++result.health_increase;
    }
    if (old_level > 20) {
        --result.health_increase;
    }
    if (old_level > 40) {
        --result.health_increase;
    }
    result.health_increase = std::max(result.health_increase, 3);
    if (old_level > 35) {
        result.health_increase = std::min(result.health_increase, 4);
    }
    player.health += result.health_increase;
    player.maximum_health += result.health_increase;
    ++player.level;

    result.spell_point_increase = random.next() % 4 + 3;
    if (player.level > 40) {
        result.spell_point_increase = std::min(result.spell_point_increase, 5);
    }
    player.spell_points += result.spell_point_increase;
    player.maximum_spell_points += result.spell_point_increase;
    if (player.restoration_uses > 0) {
        --player.restoration_uses;
    }

    int stat_chance = random.next() % 100 - player.difficulty;
    if (player.level == 5 || player.level == 10 || player.level == 15
        || player.level == 20 || player.level == 30) {
        stat_chance = 100;
    } else if (stat_chance < 73) {
        return result;
    }

    int stat_roll = random.next() % 8;
    if (stat_roll == 4 || stat_roll == 5 || stat_chance == 100) {
        stat_roll = player.combat_style == 1 ? 2
            : player.combat_style == 3 ? 3 : 1;
    }
    if (stat_roll == 1 && player.strength < player.maximum_stat()) {
        ++player.strength;
        result.stat_increase = StatIncrease::strength;
    } else if (stat_roll == 2 && player.intelligence < player.maximum_stat()) {
        ++player.intelligence;
        result.stat_increase = StatIncrease::intelligence;
    } else if (stat_roll == 3 && player.dexterity < player.maximum_stat()) {
        ++player.dexterity;
        result.stat_increase = StatIncrease::dexterity;
    }
    return result;
}

ItemUseResult Player::use_inventory_item(std::size_t slot, MsvcRandom& random,
                                         bool play_sound, bool in_combat) {
    if (slot >= inventory.size()) {
        throw std::out_of_range("inventory slot is out of range");
    }

    ItemUseResult result;
    const std::uint16_t item_id = inventory[slot];
    switch (item_id) {
    case 1: {
        if (health == maximum_health) {
            result.message = "You are already at full health";
            return result;
        }
        int healing = random.next() % intelligence + 10;
        if (unknown_0065_healing_bonus) {
            healing += random.next() % intelligence + 12;
        }
        inventory[slot] = 0;
        health = std::min(health + healing, maximum_health);
        result = {true, healing, play_sound ? 0x8a : 0, "Health restored"};
        return result;
    }
    case 2: {
        if (health == maximum_health) {
            result.message = "You are already at full health";
            return result;
        }
        int healing = random.next() % intelligence + 50 + intelligence;
        if (unknown_0065_healing_bonus) {
            healing += 12 + intelligence;
        }
        inventory[slot] = 0;
        health = std::min(health + healing, maximum_health);
        result = {true, healing, play_sound ? 0x8a : 0, "Health restored"};
        return result;
    }
    case 3: {
        if (spell_points == maximum_spell_points) {
            result.message = "Your spell points cannot be regenerated";
            return result;
        }
        int restored = random.next() % intelligence + 11 + intelligence;
        if (unknown_0065_healing_bonus) {
            restored += 2 + intelligence;
        }
        inventory[slot] = 0;
        spell_points = std::min(spell_points + restored, maximum_spell_points);
        result = {true, restored, 0, "Spell points restored"};
        return result;
    }
    case 4: {
        if (unknown_066a < 100) {
            unknown_067e = unknown_066a;
        }
        const int first_roll = random.next();
        int duration = random.next() % intelligence + 25 + first_roll % (level / 2 + 1);
        duration = std::min(duration, 80);
        unknown_0680 = static_cast<std::uint16_t>(unknown_0680 + duration);
        unknown_066a = 125;
        inventory[slot] = 0;
        result = {true, duration, play_sound ? 0x0e : 0,
                  "Roaming monsters will now stay away"};
        return result;
    }
    case 5:
        if (unknown_0664 == 1) {
            result.message = "You have already used a potion to return to the surface";
            return result;
        }
        if (unknown_004c < 40) {
            result.message = "You are already on the surface";
            return result;
        }
        if (unknown_0062) {
            result.message = "The surface potion has no effect here";
            return result;
        }
        if (in_combat) {
            result.message = "You cannot use this potion while fighting";
            return result;
        }
        unknown_0692 = 1;
        inventory[slot] = 0;
        return {true, 0, play_sound ? 0x0e : 0,
                "The surface potion will take effect"};
    case 6:
        if (strength >= maximum_stat()) {
            result.message = "Strength is already at its maximum";
            return result;
        }
        ++strength;
        inventory[slot] = 0;
        return {true, 1, play_sound ? 0x0e : 0, "Strength increased"};
    case 7:
        if (dexterity >= maximum_stat()) {
            result.message = "Dexterity is already at its maximum";
            return result;
        }
        ++dexterity;
        inventory[slot] = 0;
        return {true, 1, play_sound ? 0x0e : 0, "Dexterity increased"};
    case 8:
        if (intelligence >= maximum_stat()) {
            result.message = "Intelligence is already at its maximum";
            return result;
        }
        ++intelligence;
        inventory[slot] = 0;
        return {true, 1, play_sound ? 0x0e : 0, "Intelligence increased"};
    case 9: {
        const int increase = random.next() % 3 + 2;
        health += increase;
        maximum_health += increase;
        inventory[slot] = 0;
        return {true, increase, play_sound ? 0x0e : 0, "Maximum health increased"};
    }
    case 10: {
        const int increase = random.next() % 4 + 2;
        spell_points += increase;
        maximum_spell_points += increase;
        inventory[slot] = 0;
        return {true, increase, play_sound ? 0x0e : 0, "Maximum spell points increased"};
    }
    default:
        if (slot < carried_inventory_size) {
            inventory[slot] = 0;
        }
        result.message = "Invalid item; item removed";
        return result;
    }
}

} // namespace yipe
