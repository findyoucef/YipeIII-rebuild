#pragma once

#include "yipe/map.hpp"
#include "yipe/player.hpp"

#include <cstdint>
#include <filesystem>

namespace yipe {

// Versioned, portable rebuild format. Legacy .yp3 import is a separate reader.
void save_player_atomic(const std::filesystem::path& path, const Player& player);
[[nodiscard]] Player load_player(const std::filesystem::path& path);

struct GameState {
    Player player;
    WorldLocation location;
    bool giant_beetle_defeated = false;
    std::uint32_t random_state = 1;
};

// Version 2 extends the player-only version 1 payload with progression,
// equipment, world location, encounter completion, and random state.
void save_game_atomic(const std::filesystem::path& path, const GameState& game);
[[nodiscard]] GameState load_game(const std::filesystem::path& path);

} // namespace yipe
