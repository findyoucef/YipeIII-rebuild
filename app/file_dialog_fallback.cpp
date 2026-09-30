#include "file_dialog.hpp"

#include <stdexcept>

namespace yipe_app {

std::vector<std::filesystem::path> recent_game_files() { return {}; }
void remember_game_file(const std::filesystem::path&) {}
void clear_recent_game_files() {}

std::optional<std::filesystem::path> choose_open_game_file(
    const std::filesystem::path&) {
    throw std::runtime_error("native file selection is not implemented on this platform");
}

std::optional<std::filesystem::path> choose_save_game_file(
    const std::filesystem::path&) {
    throw std::runtime_error("native file selection is not implemented on this platform");
}

} // namespace yipe_app
