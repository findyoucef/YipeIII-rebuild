#pragma once

#include "yipe/random.hpp"

#include <array>

namespace yipe {

constexpr int maximum_casino_bet = 250;
constexpr int initial_dart_aim = 36;

struct CasinoWager {
    int gold = 0;
    int bet = 0;
};

enum class WagerStatus {
    placed,
    insufficient_gold,
    maximum_reached,
    invalid_amount,
};

[[nodiscard]] WagerStatus place_casino_wager(CasinoWager& wager, int amount);
void clear_casino_wager(CasinoWager& wager);

struct SlotMachineState {
    int gold = 0;
    int credits = 0;
};

struct SlotSpinResult {
    bool spun = false;
    std::array<int, 3> symbols{};
    int payout = 0;
};

[[nodiscard]] bool insert_slot_credits(SlotMachineState& state);
void collect_slot_credits(SlotMachineState& state);
[[nodiscard]] SlotSpinResult spin_slots(SlotMachineState& state,
                                        MsvcRandom& random);

struct DotRaceResult {
    bool raced = false;
    int winner = 0;
    int payout = 0;
    std::array<int, 3> final_positions{};
};

// selected_dot is one-based, matching the three controls in dialog 361.
[[nodiscard]] DotRaceResult race_dots(CasinoWager& wager, int selected_dot,
                                      MsvcRandom& random);

struct DartThrowResult {
    int final_y = 0;
    int score = 0;
};

[[nodiscard]] int adjust_dart_aim(int current_y, bool upward);
[[nodiscard]] DartThrowResult throw_dart(int aim_y, int throw_index,
                                         MsvcRandom& random);
[[nodiscard]] int dart_round_payout(const std::array<int, 3>& scores, int bet);

} // namespace yipe
