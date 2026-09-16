#include "yipe/map.hpp"

#include "yipe/player.hpp"
#include "yipe/random.hpp"

#include <algorithm>
#include <stdexcept>
#include <utility>

namespace yipe {
namespace {

bool is_interior(MapPosition position) {
    return position.x != 0 && position.x != Map::width - 1 && position.y != 0
           && position.y != Map::height - 1;
}

void set_generated_tile(Map::Tiles& tiles, int x, int y, std::uint16_t tile) {
    tiles[static_cast<std::size_t>(x * Map::height + y)] = tile;
}

struct GeneratedRoom {
    int minimum_x = 0;
    int minimum_y = 0;
    int maximum_x = 0;
    int maximum_y = 0;
};

int random_between(MsvcRandom& random, int minimum, int maximum) {
    return minimum + random.next() % (maximum - minimum + 1);
}

MovementResult update_position(const Map& map, MapPosition& position, MapPosition target,
                               int interior_code, int edge_code) {
    position = target;
    const int depth_action = map.depth_action_at(target);
    return {depth_action != 0 ? depth_action
                              : is_interior(target) ? interior_code : edge_code,
            true};
}

} // namespace

Map::Map(int id, int environment, Tiles tiles, MapEvents events)
    : id_(id), environment_(environment), tiles_(std::move(tiles)), events_(events) {}

bool Map::contains(MapPosition position) const {
    return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
}

std::uint16_t Map::tile(MapPosition position) const {
    if (!contains(position)) {
        throw std::out_of_range("map position is outside the 13x12 grid");
    }
    return tiles_[static_cast<std::size_t>(position.x * height + position.y)];
}

void Map::set_tile(MapPosition position, std::uint16_t value) {
    if (!contains(position)) {
        throw std::out_of_range("map position is outside the 13x12 grid");
    }
    tiles_[static_cast<std::size_t>(position.x * height + position.y)] = value;
}

int Map::depth_action_at(MapPosition position) const {
    for (const MapEvent& event : events_.depth) {
        if (event.has_position() && event.position == position) {
            return event.value == 0x3a ? 0x0f : 0x10;
        }
    }
    return 0;
}

void Map::restore_tile(MapPosition position, std::uint16_t value) {
    set_tile(position, value);
}

void Map::open_door(MapPosition previous, MapPosition current) {
    set_tile(current, tile(previous));
}

bool Map::toggle_linked_tiles() {
    const MapEvent& first = events_.linked[0];
    const MapEvent& second = events_.linked[1];
    if (!first.has_position() || !contains(first.position) || first.value < 0
        || second.value < 0
        || (second.has_position() && !contains(second.position))) {
        return false;
    }
    const bool currently_toggled = tile(first.position) == second.value;
    set_tile(first.position, static_cast<std::uint16_t>(
                                 currently_toggled ? first.value : second.value));
    if (second.has_position()) {
        set_tile(second.position, static_cast<std::uint16_t>(
                                      currently_toggled ? second.value : first.value));
    }
    return true;
}

std::optional<MapPosition> Map::linked_destination() const {
    if (events_.linked[0].has_position() && contains(events_.linked[0].position)) {
        return events_.linked[0].position;
    }
    return std::nullopt;
}

Map starting_map() {
    std::optional<Map> map = original_map(0x11);
    if (!map) {
        throw std::logic_error("generated map catalog does not contain the starting map");
    }
    return *map;
}

Map generate_original_map(int id, MapPosition entry, MsvcRandom& random) {
    if (id < 0x76 || id > 0x78) {
        throw std::invalid_argument("generated map ID must be between 0x76 and 0x78");
    }
    if (entry.x < 1 || entry.x >= Map::width - 1
        || entry.y < 1 || entry.y >= Map::height - 1) {
        throw std::invalid_argument("generated map entry must be an interior tile");
    }

    Map::Tiles tiles;
    tiles.fill(0x24);
    std::array<GeneratedRoom, 8> rooms{};
    rooms[0] = {10, 1, 12, 2};

    const int generated_room_count = random.next() % 3 + 4;
    const int size_penalty = generated_room_count / 2;
    for (int index = 1; index < generated_room_count; ++index) {
        const int y_span = random.next() % (6 - size_penalty) + 1;
        const int x_span = random.next() % (5 - size_penalty) + 1;
        const int minimum_y = random.next() % (10 - y_span) + 3;
        const int minimum_x = random.next() % (11 - x_span) + 1;
        const GeneratedRoom room{
            minimum_x,
            minimum_y,
            minimum_x + x_span,
            std::min(minimum_y + y_span, 10),
        };
        rooms[static_cast<std::size_t>(index)] = room;
        for (int x = room.minimum_x; x < room.maximum_x; ++x) {
            for (int y = room.minimum_y; y < room.maximum_y; ++y) {
                set_generated_tile(tiles, x, y, 0x27);
            }
        }
    }

    int room_count = generated_room_count;
    rooms[static_cast<std::size_t>(room_count++)] = {1, 1, 2, 2};
    set_generated_tile(tiles, 11, 1, 0x27);
    set_generated_tile(tiles, 1, 1, 0x27);
    set_generated_tile(tiles, 1, 2, 0x27);
    set_generated_tile(tiles, 2, 1, 0x27);
    set_generated_tile(tiles, 2, 2, 0x27);
    if (entry != MapPosition{1, 1}) {
        rooms[static_cast<std::size_t>(room_count++)] =
            {entry.x, entry.y, entry.x, entry.y};
        set_generated_tile(tiles, entry.x, entry.y, 0x27);
    }

    for (int index = 0; index < room_count - 1; ++index) {
        const GeneratedRoom& first = rooms[static_cast<std::size_t>(index)];
        const GeneratedRoom& second = rooms[static_cast<std::size_t>(index + 1)];
        const MapPosition from{
            random_between(random, first.minimum_x, first.maximum_x),
            random_between(random, first.minimum_y, first.maximum_y),
        };
        const MapPosition to{
            random_between(random, second.minimum_x, second.maximum_x),
            random_between(random, second.minimum_y, second.maximum_y),
        };
        const int x_step = from.x <= to.x ? 1 : -1;
        for (int x = from.x;; x += x_step) {
            set_generated_tile(tiles, x, from.y, 0x27);
            if (x == to.x) {
                break;
            }
        }
        const int y_step = from.y <= to.y ? 1 : -1;
        for (int y = from.y;; y += y_step) {
            set_generated_tile(tiles, to.x, y, 0x27);
            if (y == to.y) {
                break;
            }
        }
    }

    set_generated_tile(tiles, 0, 1, 0x27);
    set_generated_tile(tiles, 0, 2, 0x24);
    set_generated_tile(tiles, 11, 2, 0x24);
    set_generated_tile(tiles, 12, 0, 0x24);
    set_generated_tile(tiles, 12, 1, 0x27);
    set_generated_tile(tiles, 12, 2, 0x24);

    for (int x = 1; x <= 10; ++x) {
        const std::size_t column_start =
            static_cast<std::size_t>(x * Map::height + 1);
        for (std::size_t offset = 0; offset < 11; ++offset) {
            std::uint16_t& tile = tiles[column_start + offset];
            std::uint16_t& next_y = tiles[column_start + offset + 1];
            const std::uint16_t next_x =
                tiles[column_start + offset + Map::height];
            const std::uint16_t diagonal =
                tiles[column_start + offset + Map::height + 1];
            if (tile == 0x24 && next_x == 0x27 && next_y == 0x27
                && diagonal == 0x24) {
                tile = 0x27;
            }
            if ((tile == 0x27 || tile == 0x28) && next_x == 0x24
                && next_y == 0x24 && diagonal == 0x27) {
                next_y = 0x27;
            }
        }
    }

    return Map(id, 7, std::move(tiles));
}

bool cross_map_edge(WorldLocation& location) {
    bool crossed = false;
    if (location.position.x == 0) {
        --location.region_x;
        location.position.x = Map::width - 2;
        crossed = true;
    } else if (location.position.x == Map::width - 1) {
        ++location.region_x;
        location.position.x = 1;
        crossed = true;
    }

    if (location.position.y == 0) {
        ++location.region_y;
        location.position.y = Map::height - 2;
        crossed = true;
    } else if (location.position.y == Map::height - 1) {
        --location.region_y;
        location.position.y = 1;
        crossed = true;
    }
    if (!crossed) {
        return false;
    }

    if (location.region_x == 2) {
        location.region_x = -4;
    } else if (location.region_x == -5) {
        location.region_x = 1;
    }
    if (location.region_y == 3) {
        location.region_y = -3;
    } else if (location.region_y == -4) {
        location.region_y = 2;
    }

    location.map_id = location.region_x - location.region_y * 6 + 0x11
                      + location.map_layer * 0x28
                      + (location.alternate_map_set ? 0x62 : 0);
    return true;
}

void ascend_to_surface(WorldLocation& location) {
    location.map_layer = 0;
    location.map_id = location.region_x - location.region_y * 6 + 0x11
                      + (location.alternate_map_set ? 0x62 : 0);
}

void return_to_surface(WorldLocation& location) {
    location.position = {4, 5};
    location.region_x = 0;
    location.region_y = 0;
    location.map_layer = 0;
    location.alternate_map_set = false;
    location.map_id = 0x11;
}

bool change_map_depth(WorldLocation& location, int original_code) {
    int layer_change = 0;
    if (original_code == 0x0f) {
        layer_change = -1;
    } else if (original_code == 0x0e || original_code == 0x10) {
        layer_change = 1;
    } else {
        return false;
    }

    static_cast<void>(cross_map_edge(location));
    location.map_layer += layer_change;
    if (location.map_layer < 0 || location.map_layer > 2) {
        return_to_surface(location);
        return true;
    }
    location.map_id = location.region_x - location.region_y * 6 + 0x11
                      + location.map_layer * 0x28
                      + (location.alternate_map_set ? 0x62 : 0);
    return true;
}

MovementResult try_move(const Map& map, MapPosition& position, int delta_x, int delta_y,
                        const MovementState& state) {
    const MapPosition target{position.x + delta_x, position.y + delta_y};
    if (!map.contains(target)) {
        return {};
    }

    std::uint16_t tile = map.tile(target);
    if (map.events().overlay.has_position() && map.events().overlay.position == target
        && map.events().overlay.value > 0) {
        tile = 0x71;
    }
    switch (tile) {
    case 1:
        return {8, false};
    case 2:
    case 8:
    case 0x0b:
    case 0x16:
    case 0x17:
    case 0x18:
    case 0x19:
    case 0x1a:
    case 0x1b:
    case 0x1c:
    case 0x1d:
    case 0x1e:
    case 0x1f:
    case 0x20:
    case 0x27:
    case 0x28:
    case 0x29:
    case 0x2a:
    case 0x2c:
    case 0x2d:
    case 0x2f:
    case 0x32:
    case 0x33:
    case 0x34:
    case 0x35:
    case 0x3c:
    case 0x43:
    case 0x44:
    case 0x45:
    case 0x53:
    case 0x65:
    case 0x66:
    case 0x67:
    case 0x68:
    case 0x69:
        return update_position(map, position, target, 1, 2);
    case 4:
        return {0x0d, false};
    case 5:
        return {7, false};
    case 6:
        return {5, false};
    case 7:
        return {6, false};
    case 10:
        return {9, false};
    case 0x0c:
        return {0x0c, false};
    case 0x24:
        return {0x17, false};
    case 0x30:
        return {0x15, false};
    case 0x36:
    case 0x39:
        return update_position(map, position, target, 0x0e, 0x0e);
    case 0x3e:
    case 0x3f:
    case 0x40:
        if (tile == 0x3f && !state.has_rainbow_key) {
            return {0x13, false};
        }
        if (tile == 0x40 && !state.has_underworld_key) {
            return {0x14, false};
        }
        return update_position(map, position, target, 0x12, 0x12);
    case 0x41:
        return {0x11, false};
    case 0x52:
        return {0x1b, false};
    case 0x55:
        return {0x16, false};
    case 0x56:
    case 0x57:
        if (!state.unknown_0674) {
            return {};
        }
        [[fallthrough]];
    case 0x26:
    case 0x42:
    case 0x46:
    case 0x47:
    case 0x48:
    case 0x49:
        return update_position(map, position, target, 3, 2);
    case 0x58:
    case 0x59:
        if (state.has_tree_climbing_shoes) {
            return update_position(map, position, target, 1, 1);
        }
        return {};
    case 0x5a:
        return update_position(map, position, target, 0x19, 2);
    case 0x5d:
    case 0x5e:
    case 0x5f:
        return update_position(map, position, target, 0x1a, 2);
    case 0x71:
        return update_position(map, position, target, 0x18, 0x18);
    default:
        return {};
    }
}

bool use_restoration_tile(Player& player) {
    if (player.restoration_uses >= 7 - player.difficulty) {
        return false;
    }
    ++player.restoration_uses;
    player.health = player.maximum_health;
    player.ability_points = player.maximum_ability_points;
    player.spell_points = player.maximum_spell_points;
    return true;
}

int apply_fire_wall(Player& player, MsvcRandom& random) {
    const int damage = random.next() % std::max(player.difficulty, 1) + 2
                       + random.next() % std::max(player.level, 1);
    if (!player.adjust_health(-damage)) {
        player.health = 1;
    }
    return damage;
}

} // namespace yipe
