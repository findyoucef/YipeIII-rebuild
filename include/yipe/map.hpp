#pragma once

#include <array>
#include <cstddef>
#include <cstdint>

namespace yipe {

struct MapPosition {
    int x = 0;
    int y = 0;

    friend bool operator==(const MapPosition&, const MapPosition&) = default;
};

class Map {
public:
    static constexpr int width = 13;
    static constexpr int height = 12;
    static constexpr std::size_t tile_count = width * height;
    using Tiles = std::array<std::uint16_t, tile_count>;

    Map(int id, int environment, Tiles tiles);

    [[nodiscard]] int id() const { return id_; }
    [[nodiscard]] int environment() const { return environment_; }
    [[nodiscard]] bool contains(MapPosition position) const;
    [[nodiscard]] std::uint16_t tile(MapPosition position) const;

private:
    int id_;
    int environment_;
    Tiles tiles_;
};

[[nodiscard]] Map starting_map();

// These flags preserve unresolved player offsets used by the original movement
// switch at 0x00444c10.
struct MovementState {
    bool flag_0064 = false;
    bool flag_0080 = false;
    bool flag_0082 = false;
    bool flag_0674 = false;
};

struct MovementResult {
    int original_code = 0;
    bool position_changed = false;
};

[[nodiscard]] MovementResult try_move(const Map& map, MapPosition& position,
                                      int delta_x, int delta_y,
                                      const MovementState& state = {});

} // namespace yipe
