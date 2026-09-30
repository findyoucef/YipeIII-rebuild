#include "yipe/exploration.hpp"

#include "yipe/player.hpp"
#include "yipe/random.hpp"

#include <algorithm>
#include <array>

namespace yipe {

std::optional<TimedHazard> timed_hazard_for_map(int map_id) {
    constexpr std::array<TimedHazard, 14> hazards{{
        {0x2a, {4, 7}, {4, 9}, 0x53, 0x3c},
        {0x2f, {8, 4}, {8, 5}, 0x53, 0x3c},
        {0x31, {9, 3}, {7, 5}, 0x29, 0x2a},
        {0x36, {1, 4}, {1, 5}, 0x28, 0x3c},
        {0x37, {4, 6}, {11, 3}, 0x28, 0x3c},
        {0x43, {10, 9}, {11, 7}, 0x29, 0x2a},
        {0x46, {9, 4}, {4, 6}, 0x27, 0x3c},
        {0x49, {5, 6}, {2, 3}, 0x29, 0x2a},
        {0x54, {1, 7}, {3, 9}, 0x28, 0x3c},
        {0x5e, {9, 3}, {7, 7}, 0x53, 0x3c},
        {0x62, {8, 2}, {3, 9}, 0x28, 0x3c},
        {0x74, {3, 9}, {1, 6}, 0x27, 0x3c},
        {0x79, {8, 5}, {3, 3}, 0x27, 0x24},
        {0x85, {5, 8}, {8, 5}, 0x27, 0x24},
    }};
    const auto found = std::find_if(hazards.begin(), hazards.end(),
                                    [map_id](const auto& entry) {
                                        return entry.map_id == map_id;
                                    });
    return found == hazards.end() ? std::nullopt : std::optional{*found};
}

TimedHazardResult advance_timed_hazard(
    Map& map, const MapVisibility& visibility, Player& player, MapPosition position,
    std::optional<RoamingMonster>& roaming, MsvcRandom& random) {
    const auto hazard = timed_hazard_for_map(map.id());
    if (!hazard || !visibility.contains(hazard->first)
        || !visibility.contains(hazard->second)) {
        return {};
    }
    const auto affected = [&](MapPosition cell) {
        return cell == hazard->first || cell == hazard->second;
    };
    const std::uint16_t old_tile = map.tile(hazard->first);
    std::uint16_t next_tile;
    TimedHazardResult result;
    bool remove_roaming = false;
    switch (old_tile) {
    case 0x24:
    case 0x2a:
    case 0x3c:
        next_tile = old_tile == 0x24 ? hazard->floor
                                   : static_cast<std::uint16_t>(hazard->hazard + 1);
        remove_roaming = true;
        if (affected(position) && old_tile != 0x24) {
            result.fell = old_tile == 0x2a;
            result.sound_id = result.fell ? 135 : 137;
            result.damage = result.fell ? std::max(3, player.maximum_health / 15)
                                       : std::max(5, player.maximum_health / 9);
            if (!player.adjust_health(-result.damage)) player.health = 1;
        }
        break;
    case 0x27:
    case 0x28:
    case 0x29:
    case 0x53:
        next_tile = hazard->hazard;
        remove_roaming = next_tile == 0x24;
        if (next_tile == 0x24 && affected(position)) {
            result.sound_id = 144;
            result.damage = apply_fire_wall(player, random);
        }
        break;
    case 0x2b:
    case 0x3d:
        next_tile = hazard->floor;
        break;
    default:
        return {};
    }
    map.restore_tile(hazard->first, next_tile);
    map.restore_tile(hazard->second, next_tile);
    if (remove_roaming && roaming && affected(roaming->position)) roaming.reset();
    result.changed = true;
    return result;
}

int ExplorationTimer::advance(Player& player, Map& map, MapPosition position) {
    const int tick = ++tick_;
    if (tick == 38 || tick == 76) {
        player.spell_points = std::clamp(player.spell_points + 2, 0,
                                         player.maximum_spell_points);
    } else if (tick == 57) {
        player.ability_points = std::clamp(player.ability_points + 1, 0,
                                           player.maximum_ability_points);
    } else if (map.id() == 5) {
        if (tick == 47 && map.tile({7, 8}) == 0x11) {
            map.restore_tile({7, 8}, 2);
        } else if ((tick < 47 || tick > 60) && map.tile({7, 8}) == 2
                   && position != MapPosition{7, 8}) {
            map.restore_tile({7, 8}, 0x11);
        }
    }
    if (tick == 76) tick_ = 0;
    return tick;
}

bool advance_repellent_timer(Player& player) {
    if (static_cast<std::int16_t>(player.monster_repellent_steps) <= 0) return false;
    if (--player.monster_repellent_steps != 0) return false;
    player.random_encounter_threshold = player.saved_random_encounter_threshold;
    if (static_cast<std::int16_t>(player.saved_random_encounter_threshold) > 100) {
        player.random_encounter_threshold =
            static_cast<std::uint16_t>(85 - player.difficulty);
    }
    return true;
}

} // namespace yipe
