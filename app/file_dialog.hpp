#pragma once

#include <filesystem>
#include <optional>

namespace yipe_app {

std::optional<std::filesystem::path> choose_open_game_file(
    const std::filesystem::path& initial_directory);
std::optional<std::filesystem::path> choose_save_game_file(
    const std::filesystem::path& suggested_path);

} // namespace yipe_app
