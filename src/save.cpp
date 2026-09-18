#include "yipe/save.hpp"

#include "yipe/encounter.hpp"

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
constexpr std::uint16_t player_format_version = 1;
constexpr std::uint16_t progression_game_format_version = 2;
constexpr std::uint16_t ability_game_format_version = 3;
constexpr std::uint16_t movement_game_format_version = 4;
constexpr std::uint16_t map_changes_game_format_version = 5;
constexpr std::uint16_t spells_game_format_version = 6;
constexpr std::uint16_t quest_game_format_version = 7;
constexpr std::uint16_t princess_game_format_version = 8;
constexpr std::uint16_t monster_game_format_version = 9;
constexpr std::uint16_t surface_return_game_format_version = 10;
constexpr std::uint16_t monster_history_game_format_version = 11;
constexpr std::uint16_t game_format_version = 12;
constexpr std::uint16_t legacy_giant_beetle_placement_index = 5;

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
    [[nodiscard]] std::size_t remaining() const { return bytes_.size() - position_; }

private:
    void require(std::size_t count) const {
        if (count > bytes_.size() - position_) {
            throw std::runtime_error("save file is truncated");
        }
    }

    const std::vector<std::uint8_t>& bytes_;
    std::size_t position_ = 0;
};

std::vector<std::uint8_t> encode_player_payload(const Player& player) {
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
    append_u8(payload, player.alternate_map_set ? 1 : 0);
    append_u8(payload, player.has_bonus_healing_ring ? 1 : 0);
    append_i32(payload, player.current_map_id);
    append_i32(payload, player.stat_cap_mode);
    append_u16(payload, static_cast<std::uint16_t>(player.surface_return_map_id));
    append_u16(payload, player.random_encounter_threshold);
    append_u16(payload, player.saved_random_encounter_threshold);
    append_u16(payload, player.monster_repellent_steps);
    append_u16(payload, player.pending_relocation);
    return payload;
}

std::vector<std::uint8_t> encode_game_payload(const GameState& game) {
    std::vector<std::uint8_t> payload = encode_player_payload(game.player);
    append_i32(payload, game.player.experience);
    append_i32(payload, game.player.gold);
    append_i32(payload, game.player.weapon_rank);
    append_i32(payload, game.player.armor_rank);
    append_u16(payload, game.player.boots_rank);
    append_u16(payload, game.player.gloves_rank);
    append_u16(payload, game.player.helmet_rank);
    append_u16(payload, game.player.ring_rank);
    append_u16(payload, game.player.necklace_rank);
    append_i32(payload, game.player.difficulty);
    append_i32(payload, game.player.combat_style);
    append_i32(payload, game.player.equipment_upgrade_count);
    append_i32(payload, game.player.loot_roll_penalty);
    append_i32(payload, game.location.position.x);
    append_i32(payload, game.location.position.y);
    append_i32(payload, game.location.region_x);
    append_i32(payload, game.location.region_y);
    append_i32(payload, game.location.map_layer);
    append_u8(payload, game.location.alternate_map_set ? 1 : 0);
    append_i32(payload, game.location.map_id);
    append_u8(payload,
              std::find(game.defeated_monster_placements.begin(),
                        game.defeated_monster_placements.end(),
                        legacy_giant_beetle_placement_index)
                      != game.defeated_monster_placements.end()
                  ? 1 : 0);
    append_u32(payload, game.random_state);
    append_i32(payload, game.player.ability_points);
    append_i32(payload, game.player.maximum_ability_points);
    append_i32(payload, static_cast<int>(game.player.special_ability));
    append_i32(payload, game.player.special_ability_duration);
    append_i32(payload, game.player.enrage_base_dexterity);
    append_i32(payload, game.player.enrage_base_intelligence);
    append_i32(payload, game.player.enrage_base_strength);
    append_i32(payload, game.player.restoration_uses);
    append_u8(payload, game.player.has_waterproof_shoe_spray ? 1 : 0);
    append_u8(payload, game.player.has_rainbow_key ? 1 : 0);
    append_u8(payload, game.player.has_underworld_key ? 1 : 0);
    append_u8(payload, game.player.has_tree_climbing_shoes ? 1 : 0);
    if (game.map_tile_changes.size() > std::numeric_limits<std::uint32_t>::max()) {
        throw std::runtime_error("too many map tile changes to save");
    }
    append_u32(payload, static_cast<std::uint32_t>(game.map_tile_changes.size()));
    for (const MapTileChange& change : game.map_tile_changes) {
        append_i32(payload, change.map_id);
        append_i32(payload, change.position.x);
        append_i32(payload, change.position.y);
        append_u16(payload, change.tile);
    }
    for (const std::uint16_t spell : game.player.spells) {
        append_u16(payload, spell);
    }
    append_i32(payload, game.player.quest_score);
    append_u8(payload, game.player.has_lucky_rabbit_foot ? 1 : 0);
    for (const bool flag : game.player.quest_flags) {
        append_u8(payload, flag ? 1 : 0);
    }
    for (const std::uint16_t value : game.player.inventory_values) {
        append_u16(payload, value);
    }
    append_i32(payload, game.player.quest_progress_total);
    append_u16(payload, game.player.gatekeeper_state);
    append_u16(payload, game.player.stamps_found);
    append_u16(payload, game.player.princesses_found);
    append_u16(payload, game.player.princess_mystery_uses);
    if (game.defeated_monster_placements.size()
        > std::numeric_limits<std::uint32_t>::max()) {
        throw std::runtime_error("too many defeated monster placements to save");
    }
    append_u32(payload,
               static_cast<std::uint32_t>(game.defeated_monster_placements.size()));
    std::array<bool, original_monster_placement_count> seen_placements{};
    for (const std::uint16_t placement : game.defeated_monster_placements) {
        if (placement >= original_monster_placement_count
            || seen_placements[placement]) {
            throw std::runtime_error("invalid defeated monster placement in save state");
        }
        seen_placements[placement] = true;
        append_u16(payload, placement);
    }
    append_u16(payload,
               static_cast<std::uint16_t>(game.player.surface_return_region_x));
    append_u16(payload,
               static_cast<std::uint16_t>(game.player.surface_return_region_y));
    for (const bool encountered : game.player.encountered_monster_types) {
        append_u8(payload, encountered ? 1 : 0);
    }
    append_i32(payload, game.player.death_count);
    append_u16(payload, static_cast<std::uint16_t>(
                            game.player.hell_gatekeeper_state));
    append_u8(payload,
              game.player.entered_hell_through_secret_passage ? 1 : 0);
    return payload;
}

Player decode_player_payload(Reader& reader) {
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
    player.alternate_map_set = reader.u8() != 0;
    player.has_bonus_healing_ring = reader.u8() != 0;
    player.current_map_id = reader.i32();
    player.stat_cap_mode = reader.i32();
    player.surface_return_map_id = static_cast<std::int16_t>(reader.u16());
    player.random_encounter_threshold = reader.u16();
    player.saved_random_encounter_threshold = reader.u16();
    player.monster_repellent_steps = reader.u16();
    player.pending_relocation = reader.u16();
    return player;
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

void save_payload_atomic(const std::filesystem::path& path, std::uint16_t version,
                         const std::vector<std::uint8_t>& payload) {
    if (payload.size() > std::numeric_limits<std::uint32_t>::max()) {
        throw std::runtime_error("save payload is too large");
    }

    std::vector<std::uint8_t> file(magic.begin(), magic.end());
    append_u16(file, version);
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

struct DecodedSave {
    std::uint16_t version;
    std::vector<std::uint8_t> payload;
};

DecodedSave read_save(const std::filesystem::path& path) {
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
    if (version != player_format_version && version != progression_game_format_version
        && version != ability_game_format_version
        && version != movement_game_format_version
        && version != map_changes_game_format_version
        && version != spells_game_format_version
        && version != quest_game_format_version
        && version != princess_game_format_version
        && version != monster_game_format_version
        && version != surface_return_game_format_version
        && version != monster_history_game_format_version
        && version != game_format_version) {
        throw std::runtime_error("unsupported save version");
    }
    const std::uint32_t payload_size = header.u32();
    const std::uint32_t expected_crc = header.u32();
    if (payload_size != file.size() - 14) {
        throw std::runtime_error("save payload size does not match the file");
    }

    std::vector<std::uint8_t> payload(file.begin() + 14, file.end());
    if (crc32(payload) != expected_crc) {
        throw std::runtime_error("save checksum is invalid");
    }
    return {version, std::move(payload)};
}

} // namespace

void save_player_atomic(const std::filesystem::path& path, const Player& player) {
    save_payload_atomic(path, player_format_version, encode_player_payload(player));
}

Player load_player(const std::filesystem::path& path) {
    return load_game(path).player;
}

void save_game_atomic(const std::filesystem::path& path, const GameState& game) {
    save_payload_atomic(path, game_format_version, encode_game_payload(game));
}

GameState load_game(const std::filesystem::path& path) {
    DecodedSave save = read_save(path);
    Reader reader(save.payload);
    GameState game;
    game.player = decode_player_payload(reader);
    if (save.version == player_format_version) {
        if (!reader.finished()) {
            throw std::runtime_error("save payload contains unexpected trailing data");
        }
        return game;
    }

    game.player.experience = reader.i32();
    game.player.gold = reader.i32();
    game.player.weapon_rank = reader.i32();
    game.player.armor_rank = reader.i32();
    game.player.boots_rank = reader.u16();
    game.player.gloves_rank = reader.u16();
    game.player.helmet_rank = reader.u16();
    game.player.ring_rank = reader.u16();
    game.player.necklace_rank = reader.u16();
    game.player.difficulty = reader.i32();
    game.player.combat_style = reader.i32();
    game.player.equipment_upgrade_count = reader.i32();
    game.player.loot_roll_penalty = reader.i32();
    game.location.position.x = reader.i32();
    game.location.position.y = reader.i32();
    game.location.region_x = reader.i32();
    game.location.region_y = reader.i32();
    game.location.map_layer = reader.i32();
    game.location.alternate_map_set = reader.u8() != 0;
    game.location.map_id = reader.i32();
    const bool legacy_giant_beetle_defeated = reader.u8() != 0;
    game.random_state = reader.u32();
    if (save.version == progression_game_format_version) {
        game.player.maximum_ability_points =
            game.player.combat_style == 3 ? 6 : 5;
        game.player.ability_points = game.player.maximum_ability_points;
    } else {
        game.player.ability_points = reader.i32();
        game.player.maximum_ability_points = reader.i32();
        const int ability = reader.i32();
        if (ability < static_cast<int>(SpecialAbility::none)
            || ability > static_cast<int>(SpecialAbility::enrage)) {
            throw std::runtime_error("save contains an invalid special ability");
        }
        game.player.special_ability = static_cast<SpecialAbility>(ability);
        game.player.special_ability_duration = reader.i32();
        game.player.enrage_base_dexterity = reader.i32();
        game.player.enrage_base_intelligence = reader.i32();
        game.player.enrage_base_strength = reader.i32();
    }
    if (save.version >= movement_game_format_version) {
        game.player.restoration_uses = reader.i32();
        game.player.has_waterproof_shoe_spray = reader.u8() != 0;
        game.player.has_rainbow_key = reader.u8() != 0;
        game.player.has_underworld_key = reader.u8() != 0;
        game.player.has_tree_climbing_shoes = reader.u8() != 0;
    }
    if (save.version >= map_changes_game_format_version) {
        constexpr std::size_t encoded_change_size = 3 * sizeof(std::int32_t)
                                                    + sizeof(std::uint16_t);
        const std::uint32_t change_count = reader.u32();
        if (change_count > reader.remaining() / encoded_change_size) {
            throw std::runtime_error("save contains an invalid map tile change count");
        }
        game.map_tile_changes.reserve(change_count);
        for (std::uint32_t index = 0; index < change_count; ++index) {
            MapTileChange change;
            change.map_id = reader.i32();
            change.position.x = reader.i32();
            change.position.y = reader.i32();
            change.tile = reader.u16();
            if (change.map_id < 0 || change.position.x < 0
                || change.position.x >= Map::width || change.position.y < 0
                || change.position.y >= Map::height) {
                throw std::runtime_error("save contains an invalid map tile change");
            }
            game.map_tile_changes.push_back(change);
        }
    }
    if (save.version >= spells_game_format_version) {
        for (std::uint16_t& spell : game.player.spells) {
            spell = reader.u16();
        }
    }
    if (save.version >= quest_game_format_version) {
        game.player.quest_score = reader.i32();
        game.player.has_lucky_rabbit_foot = reader.u8() != 0;
        for (bool& flag : game.player.quest_flags) {
            flag = reader.u8() != 0;
        }
        for (std::uint16_t& value : game.player.inventory_values) {
            value = reader.u16();
        }
        game.player.quest_progress_total = reader.i32();
        game.player.gatekeeper_state = reader.u16();
        game.player.stamps_found = reader.u16();
        game.player.princesses_found = reader.u16();
    }
    if (save.version >= princess_game_format_version) {
        game.player.princess_mystery_uses = reader.u16();
    }
    if (save.version >= monster_game_format_version) {
        const std::uint32_t placement_count = reader.u32();
        if (placement_count > reader.remaining() / sizeof(std::uint16_t)) {
            throw std::runtime_error(
                "save contains an invalid defeated monster placement count");
        }
        game.defeated_monster_placements.reserve(placement_count);
        std::array<bool, original_monster_placement_count> seen_placements{};
        for (std::uint32_t index = 0; index < placement_count; ++index) {
            const std::uint16_t placement = reader.u16();
            if (placement >= original_monster_placement_count
                || seen_placements[placement]) {
                throw std::runtime_error(
                    "save contains an invalid defeated monster placement");
            }
            seen_placements[placement] = true;
            if (save.version == monster_game_format_version
                && original_monster_placement_respawns_after_map_reload(
                    placement)) {
                game.temporarily_defeated_monster_placements.push_back(
                    placement);
            } else {
                game.defeated_monster_placements.push_back(placement);
            }
        }
    } else if (legacy_giant_beetle_defeated) {
        game.defeated_monster_placements.push_back(
            legacy_giant_beetle_placement_index);
    }
    if (save.version >= surface_return_game_format_version) {
        game.player.surface_return_region_x =
            static_cast<std::int16_t>(reader.u16());
        game.player.surface_return_region_y =
            static_cast<std::int16_t>(reader.u16());
    } else {
        game.player.surface_return_region_x =
            static_cast<std::int16_t>(game.location.region_x);
        game.player.surface_return_region_y =
            static_cast<std::int16_t>(game.location.region_y);
    }
    if (save.version >= monster_history_game_format_version) {
        for (bool& encountered : game.player.encountered_monster_types) {
            encountered = reader.u8() != 0;
        }
    }
    if (save.version >= game_format_version) {
        game.player.death_count = reader.i32();
        const auto hell_gatekeeper_state = static_cast<std::int16_t>(reader.u16());
        if (hell_gatekeeper_state < static_cast<std::int16_t>(
                                        HellGatekeeperState::defeated)
            || hell_gatekeeper_state > static_cast<std::int16_t>(
                                         HellGatekeeperState::waiting)) {
            throw std::runtime_error(
                "save contains an invalid Hell Gatekeeper state");
        }
        game.player.hell_gatekeeper_state =
            static_cast<HellGatekeeperState>(hell_gatekeeper_state);
        game.player.entered_hell_through_secret_passage = reader.u8() != 0;
    }
    if (!reader.finished()) {
        throw std::runtime_error("save payload contains unexpected trailing data");
    }
    return game;
}

} // namespace yipe
