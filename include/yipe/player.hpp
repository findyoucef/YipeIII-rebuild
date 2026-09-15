#pragma once

#include "yipe/random.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <string>

namespace yipe {

enum class CharacterType : std::uint8_t {
    warrior = 1,
    rogue = 2,
    wizard = 3,
};

struct ItemUseResult {
    bool consumed = false;
    int amount = 0;
    int sound_id = 0;
    std::string message;
};

class Player {
public:
    static constexpr std::size_t inventory_size = 30;

    Player();
    explicit Player(CharacterType type);

    // Returns true while the player remains alive. Original address: 0x00444a00.
    bool adjust_health(int delta);

    // Reconstructs the consumable-item switch at 0x00445750.
    ItemUseResult use_inventory_item(std::size_t slot, MsvcRandom& random,
                                     bool play_sound = true, bool in_combat = false);

    std::string name;
    int dexterity = 0;
    int intelligence = 0;
    int strength = 0;
    int health = 0;
    int maximum_health = 0;
    int spell_points = 0;
    int maximum_spell_points = 0;
    int level = 1;
    std::array<std::uint16_t, inventory_size> inventory{};

    // Meanings are not yet established; names retain their original object offsets.
    bool unknown_0062 = false;
    bool unknown_0065_healing_bonus = false;
    int unknown_004c = 17;
    int unknown_0654_stat_cap_mode = 0;
    std::uint16_t unknown_0664 = 0;
    std::uint16_t unknown_066a = 85;
    std::uint16_t unknown_067e = 85;
    std::uint16_t unknown_0680 = 0;
    std::uint16_t unknown_0692 = 0;

private:
    void initialize(CharacterType type);
    [[nodiscard]] int stat_cap() const;
};

} // namespace yipe
