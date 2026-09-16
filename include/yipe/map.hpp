#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>

namespace yipe {

class MsvcRandom;
class Player;

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
[[nodiscard]] std::optional<Map> original_map(int id);

struct WorldLocation {
    MapPosition position{5, 5};
    int region_x = 0;
    int region_y = 0;
    int map_layer = 0;
    bool alternate_map_set = false;
    int map_id = 0x11;
};

// Reconstructs edge wrapping and map-ID selection at 0x00444ad0/0x00444b50.
[[nodiscard]] bool cross_map_edge(WorldLocation& location);
void return_to_surface(WorldLocation& location);

// Reconstructs the layer changes at 0x00444ad0. Codes 0x0e and 0x10 descend;
// code 0x0f ascends. Leaving the original three layers returns to the surface.
[[nodiscard]] bool change_map_depth(WorldLocation& location, int original_code);

// Player state used by the original movement switch at 0x00444c10. Offset
// +0x674 remains unnamed.
struct MovementState {
    bool has_tree_climbing_shoes = false;
    bool has_rainbow_key = false;
    bool has_underworld_key = false;
    bool unknown_0674 = false;
};

struct MovementResult {
    int original_code = 0;
    bool position_changed = false;
};

[[nodiscard]] MovementResult try_move(const Map& map, MapPosition& position,
                                      int delta_x, int delta_y,
                                      const MovementState& state = {});

// Special movement actions dispatched at 0x0040e880.
[[nodiscard]] bool use_restoration_tile(Player& player);
[[nodiscard]] int apply_fire_wall(Player& player, MsvcRandom& random);

} // namespace yipe
