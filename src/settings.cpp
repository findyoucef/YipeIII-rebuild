#include "yipe/settings.hpp"

#include "yipe/auxiliary_data.hpp"

#include <algorithm>
#include <array>
#include <fstream>
#include <iterator>
#include <stdexcept>
#include <string>
#include <system_error>
#include <vector>

#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#endif

namespace yipe {
namespace {

constexpr std::array<std::uint8_t, 4> settings_magic{'Y', '3', 'S', 'T'};
constexpr std::uint16_t settings_version = 1;
constexpr std::size_t settings_size =
    settings_magic.size() + sizeof(settings_version) + sizeof(std::uint32_t) + 1
    + input_action_count * sizeof(std::uint16_t);

void append_u16(std::vector<std::uint8_t>& bytes, std::uint16_t value) {
    bytes.push_back(static_cast<std::uint8_t>(value & 0xffu));
    bytes.push_back(static_cast<std::uint8_t>((value >> 8u) & 0xffu));
}

void append_u32(std::vector<std::uint8_t>& bytes, std::uint32_t value) {
    for (unsigned shift = 0; shift < 32; shift += 8) {
        bytes.push_back(static_cast<std::uint8_t>(value >> shift));
    }
}

std::uint16_t read_u16(const std::vector<std::uint8_t>& bytes, std::size_t& position) {
    if (position + 2 > bytes.size()) {
        throw std::runtime_error("native settings file is truncated");
    }
    const std::uint16_t value = static_cast<std::uint16_t>(
        static_cast<std::uint16_t>(bytes[position])
        | static_cast<std::uint16_t>(
            static_cast<std::uint16_t>(bytes[position + 1]) << 8u));
    position += 2;
    return value;
}

std::uint32_t read_u32(const std::vector<std::uint8_t>& bytes, std::size_t& position) {
    if (position + 4 > bytes.size()) {
        throw std::runtime_error("native settings file is truncated");
    }
    std::uint32_t value = 0;
    for (unsigned shift = 0; shift < 32; shift += 8) {
        value |= static_cast<std::uint32_t>(bytes[position++]) << shift;
    }
    return value;
}

std::uint32_t crc32(const std::vector<std::uint8_t>& bytes, std::size_t begin) {
    std::uint32_t crc = 0xffffffffu;
    for (std::size_t index = begin; index < bytes.size(); ++index) {
        crc ^= bytes[index];
        for (int bit = 0; bit < 8; ++bit) {
            const std::uint32_t mask = 0u - (crc & 1u);
            crc = (crc >> 1u) ^ (0xedb88320u & mask);
        }
    }
    return ~crc;
}

} // namespace

std::string_view input_action_name(InputAction action) {
    constexpr std::array<std::string_view, input_action_count> names{
        "Northwest", "North", "Northeast", "West", "East", "Southwest",
        "South", "Southeast", "Attack", "Run", "Cast", "Use Item", "Special",
    };
    const std::size_t index = static_cast<std::size_t>(action);
    if (index >= names.size()) {
        throw std::out_of_range("input action is out of range");
    }
    return names[index];
}

std::string_view key_binding_name(std::uint16_t key) {
    switch (key) {
    case 0x08:
        return "Backspace";
    case 0x09:
        return "Tab";
    case 0x0d:
        return "Enter";
    case 0x1b:
        return "Escape";
    case 0x20:
        return "Space";
    case 0x21:
        return "Page Up";
    case 0x22:
        return "Page Down";
    case 0x23:
        return "End";
    case 0x24:
        return "Home";
    case 0x25:
        return "Left";
    case 0x26:
        return "Up";
    case 0x27:
        return "Right";
    case 0x28:
        return "Down";
    case 0x2d:
        return "Insert";
    case 0x2e:
        return "Delete";
    case 0x60:
        return "Numpad 0";
    case 0x61:
        return "Numpad 1";
    case 0x62:
        return "Numpad 2";
    case 0x63:
        return "Numpad 3";
    case 0x64:
        return "Numpad 4";
    case 0x65:
        return "Numpad 5";
    case 0x66:
        return "Numpad 6";
    case 0x67:
        return "Numpad 7";
    case 0x68:
        return "Numpad 8";
    case 0x69:
        return "Numpad 9";
    case 0x70:
        return "F1";
    case 0x71:
        return "F2";
    case 0x72:
        return "F3";
    case 0x73:
        return "F4";
    case 0x74:
        return "F5";
    case 0x75:
        return "F6";
    case 0x76:
        return "F7";
    case 0x77:
        return "F8";
    case 0x78:
        return "F9";
    case 0x79:
        return "F10";
    case 0x7a:
        return "F11";
    case 0x7b:
        return "F12";
    default:
        break;
    }
    if (key >= '0' && key <= '9') {
        constexpr std::array<std::string_view, 10> digits{
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        };
        return digits[static_cast<std::size_t>(key - '0')];
    }
    if (key >= 'A' && key <= 'Z') {
        constexpr std::array<std::string_view, 26> letters{
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        };
        return letters[static_cast<std::size_t>(key - 'A')];
    }
    return "Unknown";
}

GameSettings settings_from_original(const OriginalAuxiliaryData& data) {
    GameSettings settings;
    for (std::size_t index = 0; index < data.keys_0174_to_0182.size(); ++index) {
        settings.keys[index] = data.keys_0174_to_0182[index];
    }
    settings.keys[static_cast<std::size_t>(InputAction::attack)] =
        data.keys_0184_to_018c[4];
    settings.keys[static_cast<std::size_t>(InputAction::run)] =
        data.keys_0184_to_018c[0];
    settings.keys[static_cast<std::size_t>(InputAction::cast)] =
        data.keys_0184_to_018c[1];
    settings.keys[static_cast<std::size_t>(InputAction::use_item)] =
        data.keys_0184_to_018c[2];
    settings.keys[static_cast<std::size_t>(InputAction::special)] =
        data.keys_0184_to_018c[3];
    return settings;
}

void save_settings_atomic(const std::filesystem::path& path,
                          const GameSettings& settings) {
    std::vector<std::uint8_t> bytes;
    bytes.reserve(settings_size);
    bytes.insert(bytes.end(), settings_magic.begin(), settings_magic.end());
    append_u16(bytes, settings_version);
    append_u32(bytes, 0);
    const std::size_t payload_start = bytes.size();
    bytes.push_back(settings.sound_enabled ? 1 : 0);
    for (const std::uint16_t key : settings.keys) {
        if (key == 0) {
            throw std::runtime_error("cannot save an unbound input action");
        }
        append_u16(bytes, key);
    }
    const std::uint32_t checksum = crc32(bytes, payload_start);
    for (unsigned shift = 0; shift < 32; shift += 8) {
        bytes[settings_magic.size() + sizeof(settings_version) + shift / 8]
            = static_cast<std::uint8_t>(checksum >> shift);
    }

    std::error_code error;
    if (!path.parent_path().empty()) {
        std::filesystem::create_directories(path.parent_path(), error);
        if (error) {
            throw std::runtime_error("could not create native settings directory: "
                                     + error.message());
        }
    }
    std::filesystem::path temporary = path;
    temporary += ".tmp";
    {
        std::ofstream output(temporary, std::ios::binary | std::ios::trunc);
        if (!output) {
            throw std::runtime_error("could not create native settings file");
        }
        output.write(reinterpret_cast<const char*>(bytes.data()),
                     static_cast<std::streamsize>(bytes.size()));
        if (!output) {
            throw std::runtime_error("could not write native settings file");
        }
    }
#ifdef _WIN32
    if (!MoveFileExW(temporary.c_str(), path.c_str(),
                     MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
        std::filesystem::remove(temporary);
        throw std::system_error(static_cast<int>(GetLastError()),
                                std::system_category(),
                                "could not replace native settings file");
    }
#else
    std::filesystem::rename(temporary, path);
#endif
}

GameSettings load_settings(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) {
        throw std::runtime_error("could not open native settings file");
    }
    const std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                           std::istreambuf_iterator<char>()};
    if (bytes.size() != settings_size
        || !std::equal(settings_magic.begin(), settings_magic.end(), bytes.begin())) {
        throw std::runtime_error("native settings file has an invalid header or size");
    }

    std::size_t position = settings_magic.size();
    if (read_u16(bytes, position) != settings_version) {
        throw std::runtime_error("native settings file has an unsupported version");
    }
    const std::uint32_t stored_checksum = read_u32(bytes, position);
    if (stored_checksum != crc32(bytes, position)) {
        throw std::runtime_error("native settings file failed its checksum");
    }
    const std::uint8_t sound = bytes[position++];
    if (sound > 1) {
        throw std::runtime_error("native settings file has an invalid sound setting");
    }

    GameSettings settings;
    settings.sound_enabled = sound != 0;
    for (std::uint16_t& key : settings.keys) {
        key = read_u16(bytes, position);
        if (key == 0) {
            throw std::runtime_error("native settings file contains an unbound action");
        }
    }
    return settings;
}

} // namespace yipe
