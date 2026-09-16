#pragma once

#include "yipe/player.hpp"
#include "yipe/random.hpp"

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

// Reconstructs the King's quest dialog at 0x00418cd0. The caller supplies the
// local hour so the core behavior remains deterministic in tests.
[[nodiscard]] KingConversation talk_to_king(Player& player, MsvcRandom& random,
                                            int local_hour);

} // namespace yipe
