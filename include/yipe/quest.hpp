#pragma once

#include "yipe/player.hpp"
#include "yipe/random.hpp"
#include "yipe/encounter.hpp"

#include <cstddef>
#include <string>
#include <string_view>

namespace yipe {

struct WorldLocation;

struct KingConversation {
    int greeting_resource_id = 721;
    int message_resource_id = 731;
    int footer_resource_id = 766;
    std::string greeting;
    std::string_view message;
    std::string_view footer;
};

enum class PrincessGift {
    gold = 1,
    experience = 2,
    kiss = 3,
    mystery = 4,
};

struct PrincessReward {
    bool rescued = false;
    int message_resource_id = 0;
    int amount = 0;
    std::string message;
};

struct ChestReward {
    bool opened = false;
    int experience = 0;
    int gold = 0;
    LootReward loot;
};

// 0x0040e880 / 0x004450d0: one-time collection at the player's current tile.
[[nodiscard]] ChestReward open_chest(Player& player, Map& map, MapPosition position,
                                     std::size_t slot, MsvcRandom& random);

struct CombatDefeatResult {
    int message_resource_id = 701;
    std::string_view message;
};

// Reconstructs combat defeat at 0x00403cf0/0x00411a90/0x00444fd0: choose the
// contextual advice, leave the player at one HP, count the death, and enter Hell.
[[nodiscard]] CombatDefeatResult resolve_combat_defeat(
    Player& player, WorldLocation& location, int monster_type,
    MsvcRandom& random);

// Reconstructs dialog 387 at 0x004133b0 and its payment branch at 0x0040e880.
[[nodiscard]] int gatekeeper_recovery_fee(const Player& player);
[[nodiscard]] int pay_gatekeeper_recovery(Player& player);

// Reconstructs the four one-time rescue rewards at 0x00446e60 and
// 0x004471d0-0x00447480. Rescue indexes come from each map's princess event.
[[nodiscard]] PrincessReward rescue_princess(Player& player,
                                             std::size_t rescue_index,
                                             PrincessGift gift,
                                             MsvcRandom& random);

// The ending sequence at 0x0040e2d0/0x0040e330 requires all 20 rescue slots and
// advances the post-battle state from 2 to 3.
[[nodiscard]] bool complete_gatekeeper_finale(Player& player);

// Reconstructs the King's quest dialog at 0x00418cd0. The caller supplies the
// local hour so the core behavior remains deterministic in tests.
[[nodiscard]] KingConversation talk_to_king(Player& player, MsvcRandom& random,
                                            int local_hour);

} // namespace yipe
