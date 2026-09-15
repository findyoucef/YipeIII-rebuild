#include "yipe/map.hpp"

#include <stdexcept>
#include <utility>

namespace yipe {
namespace {

constexpr Map::Tiles starting_tiles{
    3,  3,  2, 2, 2, 2,  2,  2,  2,  2,  2,  2,
    3,  57, 8, 8, 8, 8,  25, 2,  2,  50, 2,  2,
    3,  3,  2, 2, 15, 14, 11, 89, 89, 89, 14, 14,
    14, 14, 14, 11, 16, 2, 22, 2,  2,  2,  2,  2,
    2,  2,  2, 2, 2, 2,  22, 2,  2,  2,  2,  2,
    2,  2,  2, 2, 2, 2,  22, 2,  2,  2,  2,  2,
    2,  2,  2, 2, 5, 8,  1,  8,  25, 2,  2,  2,
    8,  25, 2, 2, 22, 2, 22, 2,  26, 8,  8,  8,
    2,  26, 8, 8, 6, 8,  7,  2,  2,  2,  2,  2,
    2,  2,  2, 2, 2, 2,  2,  2,  2,  2,  2,  2,
    3,  2,  2, 2, 2, 2,  2,  2,  2,  2,  2,  3,
    3,  48, 2, 2, 2, 2,  2,  2,  2,  2,  10, 3,
    3,  3,  2, 2, 2, 2,  2,  2,  2,  3,  3,  3,
};

bool is_interior(MapPosition position) {
    return position.x != 0 && position.x != Map::width - 1 && position.y != 0
           && position.y != Map::height - 1;
}

MovementResult update_position(MapPosition& position, MapPosition target, int interior_code,
                               int edge_code) {
    position = target;
    return {is_interior(target) ? interior_code : edge_code, true};
}

} // namespace

Map::Map(int id, int environment, Tiles tiles)
    : id_(id), environment_(environment), tiles_(std::move(tiles)) {}

bool Map::contains(MapPosition position) const {
    return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
}

std::uint16_t Map::tile(MapPosition position) const {
    if (!contains(position)) {
        throw std::out_of_range("map position is outside the 13x12 grid");
    }
    return tiles_[static_cast<std::size_t>(position.x * height + position.y)];
}

Map starting_map() {
    return Map(0x11, 0, starting_tiles);
}

MovementResult try_move(const Map& map, MapPosition& position, int delta_x, int delta_y,
                        const MovementState& state) {
    const MapPosition target{position.x + delta_x, position.y + delta_y};
    if (!map.contains(target)) {
        return {};
    }

    const std::uint16_t tile = map.tile(target);
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
        return update_position(position, target, 1, 2);
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
        position = target;
        return {0x0e, true};
    case 0x3e:
    case 0x3f:
    case 0x40:
        if (tile == 0x3f && !state.flag_0080) {
            return {0x13, false};
        }
        if (tile == 0x40 && !state.flag_0082) {
            return {0x14, false};
        }
        position = target;
        return {0x12, true};
    case 0x41:
        return {0x11, false};
    case 0x52:
        return {0x1b, false};
    case 0x55:
        return {0x16, false};
    case 0x56:
    case 0x57:
        if (!state.flag_0674) {
            return {};
        }
        [[fallthrough]];
    case 0x26:
    case 0x42:
    case 0x46:
    case 0x47:
    case 0x48:
    case 0x49:
        return update_position(position, target, 3, 2);
    case 0x58:
    case 0x59:
        if (state.flag_0064) {
            position = target;
            return {1, true};
        }
        return {};
    case 0x5a:
        return update_position(position, target, 0x19, 2);
    case 0x5d:
    case 0x5e:
    case 0x5f:
        return update_position(position, target, 0x1a, 2);
    case 0x71:
        position = target;
        return {0x18, true};
    default:
        return {};
    }
}

} // namespace yipe
