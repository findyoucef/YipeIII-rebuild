#pragma once

#include "yipe/player.hpp"

#include <filesystem>

namespace yipe {

// Versioned, portable rebuild format. Legacy .yp3 import is a separate reader.
void save_player_atomic(const std::filesystem::path& path, const Player& player);
[[nodiscard]] Player load_player(const std::filesystem::path& path);

} // namespace yipe
