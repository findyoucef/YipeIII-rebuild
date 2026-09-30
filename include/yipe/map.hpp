#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <string_view>

namespace yipe {

class MsvcRandom;
class Player;

struct MapPosition {
    int x = 0;
    int y = 0;

    friend bool operator==(const MapPosition&, const MapPosition&) = default;
};

struct MapEvent {
    MapPosition position{-1, -1};
    int value = -1;

    [[nodiscard]] bool has_position() const {
        return position.x >= 0 && position.y >= 0;
    }
};

struct PrincessEvent {
    MapPosition position{-1, -1};
    int portrait_variant = 0;
    std::size_t rescue_index = 0;

    [[nodiscard]] bool has_position() const {
        return position.x >= 0 && position.y >= 0;
    }
};

struct PursuingMonsterEvent {
    MapPosition position{-1, -1};
    int type_id = 0;
    int upgraded_type_id = 0;
    int upgrade_level = 0;

    [[nodiscard]] bool has_position() const {
        return position.x >= 0 && position.y >= 0;
    }

    [[nodiscard]] int type_for_level(int level) const {
        return upgraded_type_id > 0 && level >= upgrade_level
            ? upgraded_type_id : type_id;
    }
};

struct MapEvents {
    MapEvent message;
    MapEvent overlay;
    std::array<MapEvent, 2> depth;
    std::array<MapEvent, 2> linked;
    PrincessEvent princess;
    PursuingMonsterEvent pursuer;
    std::array<MapPosition, 4> chests{{{-1, -1}, {-1, -1}, {-1, -1}, {-1, -1}}};
};

struct MapVisibility;

class Map {
public:
    static constexpr int width = 13;
    static constexpr int height = 12;
    static constexpr std::size_t tile_count = width * height;
    using Tiles = std::array<std::uint16_t, tile_count>;

    Map(int id, int environment, Tiles tiles, MapEvents events = {});

    [[nodiscard]] int id() const { return id_; }
    [[nodiscard]] int environment() const { return environment_; }
    [[nodiscard]] const MapEvents& events() const { return events_; }
    [[nodiscard]] bool contains(MapPosition position) const;
    [[nodiscard]] std::uint16_t tile(MapPosition position) const;
    [[nodiscard]] int depth_action_at(MapPosition position) const;
    void restore_tile(MapPosition position, std::uint16_t tile);
    void open_door(MapPosition previous, MapPosition current);
    void remove_chest(std::size_t slot);
    void relocate_overlay_after_combat(MapPosition position);
    void restore_overlay_for_saved_position(MapPosition position);
    // Returns whether the moving platform carried the player.
    [[nodiscard]] bool advance_overlay(
        const MapVisibility& visibility, MapPosition& player_position,
        MsvcRandom& random, std::optional<MapPosition> roaming_position = std::nullopt);
    [[nodiscard]] bool toggle_linked_tiles();
    [[nodiscard]] std::optional<MapPosition> linked_destination() const;

private:
    void set_tile(MapPosition position, std::uint16_t tile);

    int id_;
    int environment_;
    Tiles tiles_;
    MapEvents events_;
};

struct MapVisibility {
    int map_id = -1;
    std::array<bool, Map::tile_count> visible{};
    std::array<bool, Map::tile_count> expanded{};

    [[nodiscard]] bool contains(MapPosition position) const;
};

// Surface maps begin fully revealed. Underground maps reproduce the original
// room-reveal flood through non-opaque cells at 0x0041cc00/0x0041cc50.
void initialize_map_visibility(MapVisibility& visibility, const Map& map,
                               MapPosition player_position);
void reveal_map_from(MapVisibility& visibility, const Map& map,
                     MapPosition player_position);

[[nodiscard]] Map starting_map();
[[nodiscard]] std::optional<Map> original_map(int id);
[[nodiscard]] std::array<MapPosition, 4> original_chests_for_map(int id);
// Maps 0x76-0x78 are generated from the current entry position rather than a
// static table. Reconstructs 0x0043fd80 and preserves its random-call order.
[[nodiscard]] Map generate_original_map(int id, MapPosition entry,
                                        MsvcRandom& random);

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
void ascend_to_surface(WorldLocation& location);
void return_to_surface(WorldLocation& location);
[[nodiscard]] std::optional<MapPosition> surface_return_position(const Map& map);

enum class PendingRelocation : std::uint16_t {
    none = 0,
    surface = 1,
    yipe = 2,
    hell = 3,
    stone_circle = 4,
};

// Applies the deferred relocation values written by the original item/spell/Other
// routines. The caller loads the destination map and, for a surface return,
// places the player below its final 0x36/0x39 entrance tile.
[[nodiscard]] PendingRelocation apply_pending_relocation(
    Player& player, WorldLocation& location);

// Common Hell entry; death-specific HP and counter changes belong to combat.
void enter_hell(Player& player, WorldLocation& location, bool through_secret_passage);

struct OtherActionResult {
    int message_resource_id = 0;
    int sound_id = 0;
    std::string_view message;
};

// Map-entry terrain changes and water refill at 0x0040de80.
void initialize_map_actions(Player& player, Map& map, MapPosition entry);

// Map-specific hints at 0x004121c0, selected by movement action 3 after
// handling water holes, trees, and the secret Hell tunnel separately.
[[nodiscard]] std::string_view map_movement_hint(const Player& player, int map_id);

// Reconstructs the tile-specific Other command at 0x00416440.
[[nodiscard]] OtherActionResult perform_other_action(
    Player& player, Map& map, MapPosition position, MsvcRandom& random);

// Reconstructs the layer changes at 0x00444ad0. Codes 0x0e and 0x10 descend;
// code 0x0f ascends. Leaving the original three layers returns to the surface.
[[nodiscard]] bool change_map_depth(WorldLocation& location, int original_code);

// Player state used by the original movement switch at 0x00444c10.
struct MovementState {
    bool has_waterproof_shoe_spray = false;
    bool has_rainbow_key = false;
    bool has_underworld_key = false;
    bool has_tree_climbing_shoes = false;
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

enum class SecretTreeResult {
    incorrect_code,
    reward_granted,
    already_solved,
};

// Reconstructs the secret-tree comparison and reward at 0x00448180 and the
// movement-action caller at 0x0040e880.
[[nodiscard]] SecretTreeResult enter_secret_tree_code(Player& player,
                                                      std::string_view code);

} // namespace yipe
