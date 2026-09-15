#include "yipe/auxiliary_data.hpp"

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
