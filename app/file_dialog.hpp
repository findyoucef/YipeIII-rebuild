#pragma once

#include <filesystem>
#include <optional>
#include <vector>

namespace yipe_app {

std::optional<std::filesystem::path> choose_open_game_file(
    const std::filesystem::path& initial_directory);
std::optional<std::filesystem::path> choose_save_game_file(
    const std::filesystem::path& suggested_path);
std::vector<std::filesystem::path> recent_game_files();
void remember_game_file(const std::filesystem::path& path);
void clear_recent_game_files();

} // namespace yipe_app
