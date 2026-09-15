#include "yipe/save.hpp"

#include <algorithm>
#include <array>
#include <cstdint>
#include <fstream>
#include <iterator>
#include <limits>
#include <stdexcept>
#include <string>
#include <system_error>
#include <vector>

#ifdef _WIN32
#include <windows.h>
#endif

namespace yipe {
namespace {

constexpr std::array<std::uint8_t, 4> magic = {'Y', '3', 'R', 'S'};
constexpr std::uint16_t format_version = 1;

void append_u8(std::vector<std::uint8_t>& output, std::uint8_t value) {
    output.push_back(value);
}

void append_u16(std::vector<std::uint8_t>& output, std::uint16_t value) {
    output.push_back(static_cast<std::uint8_t>(value));
    output.push_back(static_cast<std::uint8_t>(value >> 8u));
}

void append_u32(std::vector<std::uint8_t>& output, std::uint32_t value) {
    for (unsigned shift = 0; shift < 32; shift += 8) {
        output.push_back(static_cast<std::uint8_t>(value >> shift));
    }
}

void append_i32(std::vector<std::uint8_t>& output, int value) {
    static_assert(sizeof(int) >= sizeof(std::int32_t));
    if (value < std::numeric_limits<std::int32_t>::min()
        || value > std::numeric_limits<std::int32_t>::max()) {
        throw std::runtime_error("player value is outside the save format's 32-bit range");
    }
    append_u32(output, static_cast<std::uint32_t>(static_cast<std::int32_t>(value)));
}

std::uint32_t crc32(const std::vector<std::uint8_t>& bytes) {
    std::uint32_t crc = 0xffffffffu;
    for (const std::uint8_t byte : bytes) {
        crc ^= byte;
        for (int bit = 0; bit < 8; ++bit) {
            const std::uint32_t mask = 0u - (crc & 1u);
            crc = (crc >> 1u) ^ (0xedb88320u & mask);
        }
    }
    return ~crc;
}

class Reader {
public:
    explicit Reader(const std::vector<std::uint8_t>& bytes) : bytes_(bytes) {}

    std::uint8_t u8() {
        require(1);
        return bytes_[position_++];
    }

    std::uint16_t u16() {
        require(2);
        const std::uint16_t value = static_cast<std::uint16_t>(bytes_[position_])
            | static_cast<std::uint16_t>(bytes_[position_ + 1] << 8u);
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

    int i32() {
        return static_cast<int>(static_cast<std::int32_t>(u32()));
    }

    std::string string() {
        const std::size_t length = u16();
        require(length);
        std::string value(bytes_.begin() + static_cast<std::ptrdiff_t>(position_),
                          bytes_.begin() + static_cast<std::ptrdiff_t>(position_ + length));
        position_ += length;
        return value;
    }

    [[nodiscard]] bool finished() const { return position_ == bytes_.size(); }

private:
    void require(std::size_t count) const {
        if (count > bytes_.size() - position_) {
            throw std::runtime_error("save file is truncated");
        }
    }

    const std::vector<std::uint8_t>& bytes_;
    std::size_t position_ = 0;
};

std::vector<std::uint8_t> encode_payload(const Player& player) {
    if (player.name.size() > std::numeric_limits<std::uint16_t>::max()) {
        throw std::runtime_error("player name is too long to save");
    }

    std::vector<std::uint8_t> payload;
    append_u16(payload, static_cast<std::uint16_t>(player.name.size()));
    payload.insert(payload.end(), player.name.begin(), player.name.end());
    append_i32(payload, player.dexterity);
    append_i32(payload, player.intelligence);
    append_i32(payload, player.strength);
    append_i32(payload, player.health);
    append_i32(payload, player.maximum_health);
    append_i32(payload, player.spell_points);
    append_i32(payload, player.maximum_spell_points);
    append_i32(payload, player.level);
    for (const std::uint16_t item : player.inventory) {
        append_u16(payload, item);
    }
    append_u8(payload, player.unknown_0062 ? 1 : 0);
    append_u8(payload, player.unknown_0065_healing_bonus ? 1 : 0);
    append_i32(payload, player.unknown_004c);
    append_i32(payload, player.unknown_0654_stat_cap_mode);
    append_u16(payload, player.unknown_0664);
    append_u16(payload, player.unknown_066a);
    append_u16(payload, player.unknown_067e);
    append_u16(payload, player.unknown_0680);
    append_u16(payload, player.unknown_0692);
    return payload;
}

void write_file(const std::filesystem::path& path, const std::vector<std::uint8_t>& bytes) {
    std::ofstream output(path, std::ios::binary | std::ios::trunc);
    if (!output) {
        throw std::runtime_error("could not open temporary save file");
    }
    output.write(reinterpret_cast<const char*>(bytes.data()),
                 static_cast<std::streamsize>(bytes.size()));
    output.close();
    if (!output) {
        throw std::runtime_error("could not finish writing temporary save file");
    }
}

void replace_file(const std::filesystem::path& temporary,
                  const std::filesystem::path& destination) {
#ifdef _WIN32
    if (!MoveFileExW(temporary.c_str(), destination.c_str(),
                     MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
        throw std::system_error(static_cast<int>(GetLastError()), std::system_category(),
                                "could not replace save file");
    }
#else
    std::filesystem::rename(temporary, destination);
#endif
}

} // namespace

void save_player_atomic(const std::filesystem::path& path, const Player& player) {
    const std::vector<std::uint8_t> payload = encode_payload(player);
    if (payload.size() > std::numeric_limits<std::uint32_t>::max()) {
        throw std::runtime_error("save payload is too large");
    }

    std::vector<std::uint8_t> file(magic.begin(), magic.end());
    append_u16(file, format_version);
    append_u32(file, static_cast<std::uint32_t>(payload.size()));
    append_u32(file, crc32(payload));
    file.insert(file.end(), payload.begin(), payload.end());

    const std::filesystem::path temporary = path.string() + ".tmp";
    try {
        write_file(temporary, file);
        replace_file(temporary, path);
    } catch (...) {
        std::error_code ignored;
        std::filesystem::remove(temporary, ignored);
        throw;
    }
}

Player load_player(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) {
        throw std::runtime_error("could not open save file");
    }
    const std::vector<std::uint8_t> file{std::istreambuf_iterator<char>(input),
                                         std::istreambuf_iterator<char>()};
    if (file.size() < 14 || !std::equal(magic.begin(), magic.end(), file.begin())) {
        throw std::runtime_error("not a Yipe III rebuild save file");
    }

    Reader header(file);
    for (std::size_t index = 0; index < magic.size(); ++index) {
        static_cast<void>(header.u8());
    }
    const std::uint16_t version = header.u16();
    if (version != format_version) {
        throw std::runtime_error("unsupported save version");
    }
    const std::uint32_t payload_size = header.u32();
    const std::uint32_t expected_crc = header.u32();
    if (payload_size != file.size() - 14) {
        throw std::runtime_error("save payload size does not match the file");
    }

    const std::vector<std::uint8_t> payload(file.begin() + 14, file.end());
    if (crc32(payload) != expected_crc) {
        throw std::runtime_error("save checksum is invalid");
    }

    Reader reader(payload);
    Player player;
    player.name = reader.string();
    player.dexterity = reader.i32();
    player.intelligence = reader.i32();
    player.strength = reader.i32();
    player.health = reader.i32();
    player.maximum_health = reader.i32();
    player.spell_points = reader.i32();
    player.maximum_spell_points = reader.i32();
    player.level = reader.i32();
    for (std::uint16_t& item : player.inventory) {
        item = reader.u16();
    }
    player.unknown_0062 = reader.u8() != 0;
    player.unknown_0065_healing_bonus = reader.u8() != 0;
    player.unknown_004c = reader.i32();
    player.unknown_0654_stat_cap_mode = reader.i32();
    player.unknown_0664 = reader.u16();
    player.unknown_066a = reader.u16();
    player.unknown_067e = reader.u16();
    player.unknown_0680 = reader.u16();
    player.unknown_0692 = reader.u16();
    if (!reader.finished()) {
        throw std::runtime_error("save payload contains unexpected trailing data");
    }
    return player;
}

} // namespace yipe
