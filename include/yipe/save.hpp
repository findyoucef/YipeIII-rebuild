#pragma once

#include "yipe/map.hpp"
#include "yipe/player.hpp"

#include <cstdint>
#include <filesystem>
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
};

// Version 2 extended the player-only version 1 payload with progression,
// equipment, world location, encounter completion, and random state. Version 3
// added special-ability state; version 4 added movement-item and restoration
// state; version 5 added persistent map-tile changes; version 6 adds learned
// spells; version 7 adds the recovered King quest state; version 8 adds the
// princess mystery-reward counter; version 9 adds all defeated monster
// placement indices while retaining all readers.
void save_game_atomic(const std::filesystem::path& path, const GameState& game);
[[nodiscard]] GameState load_game(const std::filesystem::path& path);

} // namespace yipe
