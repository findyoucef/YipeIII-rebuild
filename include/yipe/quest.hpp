#pragma once

#include "yipe/player.hpp"
#include "yipe/random.hpp"

#include <cstddef>
#include <string>
#include <string_view>

namespace yipe {

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
