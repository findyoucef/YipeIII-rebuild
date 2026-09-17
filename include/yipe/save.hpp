#pragma once

#include "yipe/encounter.hpp"
#include "yipe/map.hpp"
#include "yipe/player.hpp"

#include <cstdint>
#include <filesystem>
#include <optional>
#include <vector>

namespace yipe {

// Versioned, portable rebuild format. Legacy .yp3 import is a separate reader.
void save_player_atomic(const std::filesystem::path& path, const Player& player);
[[nodiscard]] Player load_player(const std::filesystem::path& path);

struct MapTileChange {
    int map_id = 0;
    MapPosition position;
    std::uint16_t tile = 0;

    friend bool operator==(const MapTileChange&, const MapTileChange&) = default;
};

struct GameState {
    Player player;
    WorldLocation location;
    std::uint32_t random_state = 1;
    std::vector<MapTileChange> map_tile_changes;
    std::vector<std::uint16_t> defeated_monster_placements;
    // Repeatable placements stay absent only until another map is loaded.
    // This transient list is intentionally excluded from native saves.
    std::vector<std::uint16_t> temporarily_defeated_monster_placements;
    // Randomly spawned monsters are map-instance state in the original and
    // are intentionally excluded from native saves.
    std::optional<RoamingMonster> roaming_monster;
    // Map-authored pursuers also reset to their origin whenever a map loads.
    std::optional<PursuingMonster> pursuing_monster;
    MapVisibility map_visibility;
};

// Version 2 extended the player-only version 1 payload with progression,
// equipment, world location, encounter completion, and random state. Version 3
// added special-ability state; version 4 added movement-item and restoration
// state; version 5 added persistent map-tile changes; version 6 adds learned
// spells; version 7 adds the recovered King quest state; version 8 adds the
// princess mystery-reward counter; version 9 adds all defeated monster
// placement indices; version 10 adds the saved surface-return region; version
// 11 adds the original per-monster encounter-history flags while retaining all
// earlier readers.
void save_game_atomic(const std::filesystem::path& path, const GameState& game);
[[nodiscard]] GameState load_game(const std::filesystem::path& path);

} // namespace yipe
