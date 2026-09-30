#pragma once

#include "yipe/encounter.hpp"
#include "yipe/map.hpp"

namespace yipe {

struct TimedHazard {
    int map_id;
    MapPosition first;
    MapPosition second;
    std::uint16_t floor;
    std::uint16_t hazard;
};

[[nodiscard]] std::optional<TimedHazard> timed_hazard_for_map(int map_id);

struct TimedHazardResult {
    bool changed = false;
    bool fell = false;
    int sound_id = 0;
    int damage = 0;
};

// The two cells change together, driven by the first cell's current state.
[[nodiscard]] TimedHazardResult advance_timed_hazard(
    Map& map, const MapVisibility& visibility, Player& player, MapPosition position,
    std::optional<RoamingMonster>& roaming, MsvcRandom& random);

class ExplorationTimer {
public:
    // 0x00412f90: view-local phase survives map changes, but not a reopened game.
    [[nodiscard]] int advance(Player& player, Map& map, MapPosition position);

private:
    int tick_ = 0;
};

// Called after map/monster updates, including any combat caused by a platform.
[[nodiscard]] bool advance_repellent_timer(Player& player);

} // namespace yipe
