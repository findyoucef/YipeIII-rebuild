#include "yipe/auxiliary_data.hpp"

#include "yipe/player.hpp"

#include <algorithm>
#include <cstddef>
#include <fstream>
#include <iterator>
#include <limits>
#include <stdexcept>
#include <vector>

namespace yipe {
namespace {

class ArchiveReader {
public:
    explicit ArchiveReader(const std::vector<std::uint8_t>& bytes) : bytes_(bytes) {}

    std::uint8_t u8() {
        require(1);
        return bytes_[position_++];
    }

    std::uint16_t u16() {
        require(2);
        const auto value = static_cast<std::uint16_t>(
            static_cast<std::uint16_t>(bytes_[position_])
            | static_cast<std::uint16_t>(static_cast<std::uint16_t>(bytes_[position_ + 1]) << 8u));
        position_ += 2;
        return value;
    }

    std::uint32_t u32() {
        require(4);
        std::uint32_t value = 0;
        for (unsigned index = 0; index < 4; ++index) {
            value |= static_cast<std::uint32_t>(bytes_[position_ + index]) << (index * 8u);
        }
        position_ += 4;
        return value;
    }

    std::int32_t i32() { return static_cast<std::int32_t>(u32()); }

    std::string archive_string() {
        const auto [length, unicode] = string_length();
        if (unicode) {
            throw std::runtime_error("Unicode strings in original Yipe3.dat are unsupported");
        }
        require(length);
        const auto first = bytes_.begin() + static_cast<std::ptrdiff_t>(position_);
        std::string value(first, first + static_cast<std::ptrdiff_t>(length));
        position_ += length;
        return value;
    }

    [[nodiscard]] bool finished() const { return position_ == bytes_.size(); }

private:
    struct StringLength {
        std::size_t value;
        bool unicode;
    };

    StringLength string_length() {
        std::uint32_t length = u8();
        if (length != 0xffu) {
            return {length, false};
        }

        length = u16();
        if (length == 0xfffeu) {
            const StringLength nested = string_length();
            if (nested.unicode) {
                throw std::runtime_error("invalid nested Unicode string marker in Yipe3.dat");
            }
            return {nested.value, true};
        }
        if (length == 0xffffu) {
            length = u32();
        }
        if (length > static_cast<std::uint32_t>(std::numeric_limits<std::ptrdiff_t>::max())) {
            throw std::runtime_error("string in Yipe3.dat is too large");
        }
        return {length, false};
    }

    void require(std::size_t count) const {
        if (count > bytes_.size() - position_) {
            throw std::runtime_error("original Yipe3.dat is truncated");
        }
    }

    const std::vector<std::uint8_t>& bytes_;
    std::size_t position_ = 0;
};

} // namespace

HighScores default_high_scores() {
    HighScores scores;
    for (HighScoreEntry& entry : scores) {
        entry.name = "----";
    }
    return scores;
}

std::size_t high_score_group_offset(int difficulty) {
    // The original uses a signed short and falls back to the easy group.
    switch (static_cast<std::int16_t>(difficulty)) {
    case 2: return 15;
    case 3: return 30;
    default: return 0;
    }
}

bool update_high_scores(HighScores& scores, const Player& player, bool completed_game) {
    const HighScores previous = scores;
    const std::size_t group = high_score_group_offset(player.difficulty);
    if (completed_game) {
        const auto first = scores.begin() + static_cast<std::ptrdiff_t>(group + 12);
        const auto last = first + 3;
        const auto same_name = std::find_if(first, last, [&player](const HighScoreEntry& entry) {
            return entry.name == player.name;
        });
        if (same_name == last) {
            const auto empty = std::find_if(first, last, [](const HighScoreEntry& entry) {
                return entry.name == "----";
            });
            if (empty != last) {
                empty->name = player.name;
            }
        }
    }
    const int princesses = static_cast<int>(std::count_if(
        player.inventory_values.begin(), player.inventory_values.end(),
        [](std::uint16_t value) { return static_cast<std::int16_t>(value) > 0; }));
    const std::array values{player.quest_progress_total, princesses, player.gold, player.level};
    for (std::size_t category = 0; category < values.size(); ++category) {
        const std::size_t first = group + category * 3;
        std::size_t rank = 3;
        for (std::size_t index = 0; index < 3; ++index) {
            if (scores[first + index].name == player.name) {
                rank = index;
            }
        }
        const int value = values[category];
        if (rank == 3) {
            rank = 2;
        }
        if (value < scores[first + rank].value) {
            continue;
        }
        // New and existing names move ahead of equal scores.
        while (rank > 0 && scores[first + rank - 1].value <= value) {
            scores[first + rank] = scores[first + rank - 1];
            --rank;
        }
        scores[first + rank] = {player.name, value};
    }
    return scores != previous;
}

OriginalAuxiliaryData load_original_auxiliary_data(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) {
        throw std::runtime_error("could not open original Yipe3.dat");
    }
    const std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                           std::istreambuf_iterator<char>()};
    ArchiveReader reader(bytes);
    OriginalAuxiliaryData result;

    for (HighScoreEntry& entry : result.high_scores) {
        entry.name = reader.archive_string();
        entry.value = reader.i32();
    }
    for (std::uint16_t& key : result.keys_0174_to_0182) {
        key = reader.u16();
    }

    result.value_0190 = reader.i32();
    result.value_0170 = reader.i32();

    // The serializer writes these offsets as 184, 18c, 186, 188, 18a.
    result.keys_0184_to_018c[0] = reader.u16();
    result.keys_0184_to_018c[4] = reader.u16();
    result.keys_0184_to_018c[1] = reader.u16();
    result.keys_0184_to_018c[2] = reader.u16();
    result.keys_0184_to_018c[3] = reader.u16();
    result.value_016c = reader.i32();

    if (!reader.finished()) {
        throw std::runtime_error("original Yipe3.dat contains unexpected trailing data");
    }
    return result;
}

} // namespace yipe
