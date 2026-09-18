#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <string>

namespace yipe {

class Player;

struct HighScoreEntry {
    std::string name;
    std::int32_t value = 0;

    bool operator==(const HighScoreEntry&) const = default;
};

using HighScores = std::array<HighScoreEntry, 45>;

[[nodiscard]] HighScores default_high_scores();
[[nodiscard]] std::size_t high_score_group_offset(int difficulty);

// Four top-three categories and the first three finishers per difficulty,
// recovered from 0x00414540. Returns whether any record actually changed.
[[nodiscard]] bool update_high_scores(HighScores& scores, const Player& player,
                                      bool completed_game = false);
void save_high_scores_atomic(const std::filesystem::path& path, const HighScores& scores);
[[nodiscard]] HighScores load_high_scores(const std::filesystem::path& path);

struct OriginalAuxiliaryData {
    HighScores high_scores{};

    // Offset-based groups preserve the original serializer and in-memory order.
    std::array<std::uint16_t, 8> keys_0174_to_0182{};
    std::int32_t value_0190 = 0;
    std::int32_t value_0170 = 0;
    std::array<std::uint16_t, 5> keys_0184_to_018c{};
    std::int32_t value_016c = 0;
};

// Reads the original game's headerless CArchive payload used by Yipe3.dat.
[[nodiscard]] OriginalAuxiliaryData
load_original_auxiliary_data(const std::filesystem::path& path);

} // namespace yipe
