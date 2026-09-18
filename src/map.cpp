#include "yipe/map.hpp"

#include "yipe/player.hpp"
#include "yipe/random.hpp"

#include <algorithm>
#include <stdexcept>
#include <utility>

namespace yipe {
namespace {

std::string_view other_action_message(int resource_id) {
    if (resource_id == 146) {
        return "You pile up the coins scattered about the floor and put them into your pocket.  In all, it looks like you gained about 180 coins.";
    }
    if (resource_id == 147) {
        return "You search the floor again.  It looks like you've already gathered up all the loose gold.";
    }
    constexpr std::array<std::string_view, 54> messages{
        "You find nothing of interest right here.",
        "Yummm!  That water sure tastes refreshing!",
        "You find nothing of interest underneath the bridge.",
        "You are standing in a narrow crack in the wall.  This must be some type of secret passage.",
        "You lie down and make a pretty snow angel in the cool winter snow.",
        "Looking under the bridge you find a small pile of gold!  You take a quick look to see that nobody is looking and you pocket the 600 gold pieces.",
        "Not only does the water taste great, but it heals some of your wounds!",
        "The water hole is temporarily dry.",
        "You take a moment to rest on the warm rock.",
        "The pretty flowers smell as good as they look.",
        "These flowers give off no distinctive smell, but they sure look nice.",
        "Upon looking at the skull closely, you decide you have no idea what type of animal it came from.",
        "The small plant you are standing on tickles your leg.",
        "You are standing on an old desert plant.  There is nothing of importance about it.",
        "A spike seems to be coming out of the wall right towards you!  You better move fast!",
        "There is an interesting crack in the ground here.  At the moment it doesn't seem big enough for you to fall into it.",
        "You don't notice anything out of the ordinary on the small stone path that you are standing on.",
        "", "", "", "",
        "The grounds shakes for a minute and suddenly part of the mountain range lowers into the ground before you!",
        "As you take a closer look at the flower, a bee pops out and stings you on the nose.  Ouch!!",
        "You notice that the sand feels soft under your feet.",
        "The sand feels warm from the hot sun.",
        "You pick up some sand and let it trickle through your fingers.",
        "Not finding anything of interest, you decide to sing a short song.",
        "",
        "You don't notice anything around you, however you do seem to feel a little safer standing on this road.",
        "This section of the road looks to be in good shape.  Some of the other sections don't seem to be as nice as this particular part.",
        "You have a nice view of the land from up here in this tree.  Sure is a pretty world.  Too bad there are so many monsters wandering around.",
        "From up in this tree it looks like something is moving down in the sand below you.  It's probably just a little sand bug.",
        "You get a nice warm feeling inside yourself when you look out upon the world of Yipe!  You feel pride in knowing that you're the one who saved the world!",
        "You are standing at the bottom of a mountain.  A few pebbles lay scattered around the sandy area below you.",
        "Some sand comes up onto the grass right here.  You don't find anything else interesting.",
        "You are standing right on top of a small plant in the middle of a grassy area.  You do not notice anything unusual about the plant.",
        "Below you is a sturdy rock that is halfway buried into the ground.  It looks just right for sitting on.",
        "You find nothing of interest in the grassy area that you are currently standing on.",
        "You have discovered a secret little path through the mountains right here.  There was no way to see this path from below.",
        "You find nothing of interest in this part of the cave.",
        "The cave floor below you feels a little chilly.  Other than that, there's nothing particularly interesting right here.",
        "You can't quite tell what seems so strange about the mountains right here.  Perhaps you should keep looking around this area.",
        "How did you get up on this mountain?!?  Hopefully you have a way to climb down from up here!",
        "The cave floor below you is surprisingly smooth.  Perhaps it has been worn down by the high number of monsters that have walked through here!",
        "Below you is a patch of snow.  Digging around in the snow you do not find anything of interest.",
        "The snow starts to melt away into the grass right here.",
        "You dig around in the snow for a few minutes and your work pays off.  Below the snow was a pile of around 400 gold coins!!",
        "After checking one more time in the snow, it looks like you missed a few gold coins the first time you checked.  You've found another 100 coins!",
        "At this point your hand is starting to get cold.  You should probably stop searching for gold coins.",
        "Ouch!!!  It looks like you've got frost bite!  The pain in your hand is just a little too much to keep searching.  You take a small amount of damange.",
        "As you take a moment to notice, you feel a slight breeze blowing throughout this cave.",
        "You are standing right on the entrance to an underground cave.",
        "While looking at the tally marks you notice the shape of the walls around you seem to be in the shape of a letter.",
        "Other than what you've already discovered, there is nothing else of interest here.",
    };
    if (resource_id < 1002 || resource_id > 1055
        || messages[static_cast<std::size_t>(resource_id - 1002)].empty()) {
        throw std::logic_error("unknown Other action message");
    }
    return messages[static_cast<std::size_t>(resource_id - 1002)];
}

bool is_interior(MapPosition position) {
    return position.x != 0 && position.x != Map::width - 1 && position.y != 0
           && position.y != Map::height - 1;
}

std::size_t map_index(MapPosition position) {
    return static_cast<std::size_t>(
        position.x * Map::height + position.y);
}

bool blocks_map_reveal(std::uint16_t tile) {
    switch (tile) {
    case 0x22:
    case 0x23:
    case 0x24:
    case 0x25:
    case 0x3e:
    case 0x3f:
    case 0x40:
    case 0x43:
    case 0x44:
    case 0x5a:
        return true;
    default:
        return false;
    }
}

void reveal_area(MapVisibility& visibility, const Map& map,
                 MapPosition center) {
    for (int x = center.x - 1; x <= center.x + 1; ++x) {
        for (int y = center.y - 1; y <= center.y + 1; ++y) {
            const MapPosition position{x, y};
            if (map.contains(position)) {
                visibility.visible[map_index(position)] = true;
            }
        }
    }
    for (int x = center.x - 1; x <= center.x + 1; ++x) {
        for (int y = center.y - 1; y <= center.y + 1; ++y) {
            const MapPosition position{x, y};
            if (!map.contains(position)) {
                continue;
            }
            const std::size_t index = map_index(position);
            if (visibility.expanded[index]) {
                continue;
            }
            visibility.expanded[index] = true;
            if (!blocks_map_reveal(map.tile(position))
                && visibility.visible[index]) {
                reveal_area(visibility, map, position);
            }
        }
    }
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

bool MapVisibility::contains(MapPosition position) const {
    return position.x >= 0 && position.x < Map::width
        && position.y >= 0 && position.y < Map::height
        && visible[map_index(position)];
}

void initialize_map_visibility(MapVisibility& visibility, const Map& map,
                               MapPosition player_position) {
    visibility.map_id = map.id();
    const bool fully_visible = map.id() < 0x29;
    visibility.visible.fill(fully_visible);
    visibility.expanded.fill(fully_visible);
    if (!fully_visible) {
        reveal_area(visibility, map, player_position);
    }
}

void reveal_map_from(MapVisibility& visibility, const Map& map,
                     MapPosition player_position) {
    if (visibility.map_id != map.id()) {
        initialize_map_visibility(visibility, map, player_position);
        return;
    }
    reveal_area(visibility, map, player_position);
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

std::optional<MapPosition> surface_return_position(const Map& map) {
    std::optional<MapPosition> result;
    for (int x = 1; x < Map::width - 1; ++x) {
        for (int y = 1; y < Map::height - 1; ++y) {
            const std::uint16_t tile = map.tile({x, y});
            if (tile == 0x36 || tile == 0x39) {
                result = MapPosition{x, y + 1};
            }
        }
    }
    return result;
}

PendingRelocation apply_pending_relocation(Player& player,
                                           WorldLocation& location) {
    const PendingRelocation relocation =
        static_cast<PendingRelocation>(player.pending_relocation);
    switch (relocation) {
    case PendingRelocation::none:
        return relocation;
    case PendingRelocation::surface:
        location.region_x = player.surface_return_region_x;
        location.region_y = player.surface_return_region_y;
        location.map_layer = 0;
        location.alternate_map_set = false;
        location.map_id = player.surface_return_map_id;
        break;
    case PendingRelocation::yipe:
        return_to_surface(location);
        break;
    case PendingRelocation::hell:
        enter_hell(player, location, true);
        break;
    case PendingRelocation::stone_circle:
        location = {{6, 8}, -2, -1, 0, false, 0x15};
        break;
    default:
        throw std::runtime_error("player has an invalid pending relocation");
    }
    player.pending_relocation = static_cast<std::uint16_t>(PendingRelocation::none);
    player.current_map_id = location.map_id;
    player.alternate_map_set = location.alternate_map_set;
    return relocation;
}

void enter_hell(Player& player, WorldLocation& location, bool through_secret_passage) {
    player.entered_hell_through_secret_passage = through_secret_passage;
    if (player.hell_gatekeeper_state == HellGatekeeperState::absent) {
        player.hell_gatekeeper_state = HellGatekeeperState::waiting;
    }
    location = {{6, 10}, -4, -1, 0, true, 0x75};
    player.current_map_id = location.map_id;
    player.alternate_map_set = true;
}

void initialize_map_actions(Player& player, Map& map) {
    if (map.id() != 0) {
        player.healing_water_available = true;
    }
    if (map.id() == 3 && player.stamps_found >= 5) {
        map.restore_tile({4, 7}, 0x46);
    }
}

OtherActionResult perform_other_action(Player& player, Map& map,
                                       MapPosition position, MsvcRandom& random) {
    const std::uint16_t tile = map.tile(position);
    int message = 1002;
    int sound = 0;
    switch (tile) {
    case 3: message = 1044; break;
    case 8:
    case 0x16:
    case 0x17:
    case 0x18:
    case 0x19:
    case 0x1a:
        message = 1018;
        if (map.id() != 0x11 && map.id() != 0x1b) {
            const int roll = random.next() % 50;
            message = roll < 29 ? 1018 : roll < 46 ? 1030 : 1031;
        }
        break;
    case 0x0b:
        message = 1004;
        if (map.id() == 0x21 && player.bridge_treasure_available) {
            player.bridge_treasure_available = false;
            player.gold += 600;
            player.experience += random.next() % 10 + 20;
            message = 1007;
        }
        break;
    case 0x1b: message = 1036; break;
    case 0x1f: message = random.next() % 100 < 71 ? 1046 : 1006; break;
    case 0x20: message = 1047; break;
    case 0x26:
        message = 1003;
        sound = 143;
        if (map.id() == 6) {
            message = 1009;
            sound = 0;
            if (player.healing_water_available && player.health < player.maximum_health) {
                if (random.next() % 100 < player.difficulty + 40) {
                    player.healing_water_available = false;
                }
                const int healing = random.next() % 10 + player.level
                    + (player.has_bonus_healing_ring ? player.intelligence : 0);
                static_cast<void>(player.adjust_health(healing));
                message = 1008;
                sound = 143;
            }
        }
        break;
    case 0x27: message = random.next() % 100 < 21 ? 1042 : 1041; break;
    case 0x28: message = random.next() % 100 < 26 ? 1045 : 1041; break;
    case 0x29:
    case 0x2a: message = 1017; break;
    case 0x2c: message = 1013; break;
    case 0x2d: {
        const int roll = random.next() % 4;
        message = roll == 0 ? 1002 : 1024 + roll;
        break;
    }
    case 0x2f: message = 1015; break;
    case 0x32: message = random.next() % 100 < 61 ? 1037 : 1014; break;
    case 0x33: message = random.next() % 100 < 41 ? 1038 : 1010; break;
    case 0x34:
        message = 1011;
        if (player.health > 3 && random.next() % 100 > 79) {
            if (!player.adjust_health(-2)) {
                player.health = 1;
            }
            message = 1024;
            sound = 50;
        }
        break;
    case 0x35:
        message = 1012;
        if (map.id() == 0x12) {
            map.restore_tile({3, 7}, 0x46);
            map.restore_tile({4, 7}, 0x46);
            message = 1023;
            sound = 152;
        }
        break;
    case 0x36:
    case 0x39: message = 1053; break;
    case 0x3c: message = 1016; break;
    case 0x42:
        player.pending_relocation = static_cast<std::uint16_t>(PendingRelocation::hell);
        return {};
    case 0x43:
    case 0x44: message = 1005; break;
    case 0x45: message = 1040; break;
    case 0x46:
        if (map.id() == 3) {
            player.pending_relocation = static_cast<std::uint16_t>(PendingRelocation::stone_circle);
            return {};
        }
        if (map.id() == 0x12) {
            message = 1043;
        }
        break;
    case 0x47:
        message = 1055;
        if (map.id() == 0x45) {
            message = player.cave_coins_collected ? 147 : 146;
            if (!player.cave_coins_collected) {
                player.cave_coins_collected = true;
                player.gold += 180;
            }
        }
        break;
    case 0x49: message = map.id() == 0x57 ? 1054 : 1055; break;
    case 0x53: message = random.next() % 100 < 31 ? 1052 : 1041; break;
    case 0x56:
    case 0x57:
        if (random.next() % 100 < 76) {
            const int roll = random.next() % 100;
            if (roll >= 61 && tile == 0x57) {
                message = 1033;
            }
        } else {
            message = player.gatekeeper_state < 3 ? 1032 : 1034;
        }
        break;
    case 0x65: message = 1035; break;
    case 0x69:
        if (player.snow_search_count == 0) {
            player.snow_search_count = 1;
            player.gold += random.next() % 10 + 401;
            message = 1048;
        } else if (player.snow_search_count == 1) {
            player.snow_search_count = 2;
            player.gold += random.next() % 10 + 101;
            message = 1049;
        } else if (player.snow_search_count == 2) {
            player.snow_search_count = 3;
            message = 1050;
        } else {
            if (!player.adjust_health(-5 - random.next() % 15)) {
                player.health = 1;
            }
            player.snow_search_count = 2;
            message = 1051;
            sound = 144;
        }
        break;
    default: {
        const int roll = random.next() % 100;
        if (roll >= 86 && tile == 2) {
            message = 1039;
        } else if (roll <= 9) {
            message = 1028;
        }
        break;
    }
    }
    return {message, sound, other_action_message(message)};
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
        if (!state.has_tree_climbing_shoes) {
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
        if (state.has_waterproof_shoe_spray) {
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

SecretTreeResult enter_secret_tree_code(Player& player, std::string_view code) {
    if (player.has_bonus_healing_ring) {
        return SecretTreeResult::already_solved;
    }
    if (code != "hhhhhh") {
        return SecretTreeResult::incorrect_code;
    }

    player.gold += 1500;
    player.experience += 250;
    if (player.restoration_uses > 0) {
        --player.restoration_uses;
    }
    player.has_bonus_healing_ring = true;
    return SecretTreeResult::reward_granted;
}

} // namespace yipe
