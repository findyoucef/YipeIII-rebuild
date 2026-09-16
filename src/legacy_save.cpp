#include "yipe/legacy_save.hpp"

#include <cstddef>
#include <fstream>
#include <iterator>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace yipe {
namespace {

constexpr std::size_t fixed_payload_size = 5064;

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
            | static_cast<std::uint16_t>(static_cast<std::uint16_t>(bytes_[position_ + 1])
                                         << 8u));
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
    bool archive_bool() { return i32() != 0; }

    std::string archive_string() {
        std::uint32_t length = u8();
        if (length == 0xffu) {
            length = u16();
            if (length == 0xfffeu) {
                throw std::runtime_error(
                    "legacy .yp3 uses an unsupported Unicode CString");
            }
            if (length == 0xffffu) {
                length = u32();
            }
        }
        if (length > remaining()) {
            throw std::runtime_error("legacy .yp3 has invalid CString framing");
        }
        const auto first = bytes_.begin() + static_cast<std::ptrdiff_t>(position_);
        std::string value(first, first + static_cast<std::ptrdiff_t>(length));
        position_ += length;
        return value;
    }

    [[nodiscard]] std::size_t remaining() const { return bytes_.size() - position_; }

private:
    void require(std::size_t count) const {
        if (count > remaining()) {
            throw std::runtime_error("legacy .yp3 save is truncated");
        }
    }

    const std::vector<std::uint8_t>& bytes_;
    std::size_t position_ = 0;
};

int signed_u16(std::uint16_t value) {
    if (value <= static_cast<std::uint16_t>(std::numeric_limits<std::int16_t>::max())) {
        return value;
    }
    return static_cast<int>(value) - 0x10000;
}

int map_id_for(const WorldLocation& location) {
    return location.region_x - location.region_y * 6 + 0x11
        + location.map_layer * 0x28 + (location.alternate_map_set ? 0x62 : 0);
}

bool validation_matches(const LegacySave& save) {
    const Player& player = save.game.player;
    const std::int64_t expected = static_cast<std::int64_t>(player.gold) / 2
        + (static_cast<std::int64_t>(player.maximum_health) - player.health) * 2
        + player.ring_rank + player.helmet_rank + player.boots_rank
        + player.armor_rank + player.weapon_rank;
    const std::int64_t difference = static_cast<std::int64_t>(save.stored_validation)
        - expected;
    return difference >= -1 && difference <= 1;
}

} // namespace

LegacySave load_legacy_save(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) {
        throw std::runtime_error("could not open legacy .yp3 save");
    }
    const std::vector<std::uint8_t> bytes{std::istreambuf_iterator<char>(input),
                                           std::istreambuf_iterator<char>()};
    ArchiveReader reader(bytes);
    LegacySave save;
    Player& player = save.game.player;

    player.name = reader.archive_string();
    if (reader.remaining() != fixed_payload_size) {
        throw std::runtime_error("legacy .yp3 payload has an unsupported size");
    }

    player.dexterity = reader.i32();
    player.intelligence = reader.i32();
    player.strength = reader.i32();
    player.health = reader.i32();
    player.maximum_health = reader.i32();
    player.spell_points = reader.i32();
    player.maximum_spell_points = reader.i32();
    player.ability_points = reader.i32();
    player.maximum_ability_points = reader.i32();
    player.level = reader.i32();
    save.unknown_005c = reader.i32();
    player.weapon_rank = reader.i32();
    player.armor_rank = reader.i32();
    save.stored_validation = reader.i32();

    player.boots_rank = reader.u16();
    player.gloves_rank = reader.u16();
    player.helmet_rank = reader.u16();
    player.ring_rank = reader.u16();
    player.necklace_rank = reader.u16();
    save.unknown_0658 = reader.i32();
    save.unknown_007e = reader.u16();
    player.restoration_uses = reader.i32();
    save.unknown_007c = reader.u16();
    player.difficulty = signed_u16(reader.u16());
    save.unknown_0074 = reader.i32();
    save.unknown_0078 = reader.i32();

    save.unknown_006c = reader.archive_bool();
    save.unknown_0060 = reader.archive_bool();
    save.unknown_0061 = reader.archive_bool();
    player.unknown_0062 = reader.archive_bool();
    save.game.location.alternate_map_set = player.unknown_0062;
    save.unknown_0063 = reader.archive_bool();
    player.has_tree_climbing_shoes = reader.archive_bool();
    save.unknown_64c = reader.archive_bool();
    for (std::size_t index = 0; index < save.flags_0152.size(); ++index) {
        save.flags_0152[index] = reader.archive_bool();
        save.flags_0352[index] = reader.archive_bool();
    }
    for (std::uint16_t& value : save.values_010c) {
        value = reader.u16();
    }
    player.spells = save.values_010c;
    for (LegacyFlagValue& entry : save.flag_values) {
        entry.flag_0148 = reader.archive_bool();
        entry.value_0080 = reader.u16();
    }
    player.has_rainbow_key = save.flag_values[0].value_0080 != 0;
    player.has_underworld_key = save.flag_values[1].value_0080 != 0;

    for (std::size_t index = 0; index < save.inventory_entries.size(); ++index) {
        LegacyInventoryEntry& entry = save.inventory_entries[index];
        entry.item_id_0094 = reader.u16();
        entry.value_00d0 = reader.u16();
        entry.flag_012a = reader.archive_bool();
        player.inventory[index] = entry.item_id_0094;
    }
    for (LegacyExtendedEntry& entry : save.extended_entries) {
        entry.value_065e = reader.u16();
        entry.value_0584 = reader.i32();
        entry.flag_0552 = reader.archive_bool();
    }

    player.unknown_0664 = save.extended_entries[3].value_065e;
    player.unknown_066a = save.extended_entries[6].value_065e;
    player.unknown_0674 = save.extended_entries[11].value_065e == 1;
    player.combat_style = signed_u16(save.extended_entries[15].value_065e);
    player.unknown_067e = save.extended_entries[16].value_065e;
    player.unknown_0680 = save.extended_entries[17].value_065e;
    player.unknown_0692 = save.extended_entries[26].value_065e;
    player.has_bonus_healing_ring = save.extended_entries[31].value_065e == 1;

    save.game.location.position.x = signed_u16(reader.u16());
    save.game.location.position.y = signed_u16(reader.u16());
    save.game.location.region_x = signed_u16(reader.u16());
    save.game.location.region_y = signed_u16(reader.u16());
    save.game.location.map_layer = signed_u16(reader.u16());
    const std::int32_t stored_gold = reader.i32();
    if (stored_gold > std::numeric_limits<std::int32_t>::max() - 203) {
        throw std::runtime_error("legacy .yp3 contains an invalid transformed gold value");
    }
    player.gold = stored_gold + 203;
    player.experience = reader.i32();
    save.trailing_healing_flag = reader.archive_bool();
    save.game.location.map_id = map_id_for(save.game.location);
    save.validation_passed = validation_matches(save);
    return save;
}

} // namespace yipe
