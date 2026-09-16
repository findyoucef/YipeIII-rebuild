#include "yipe/casino.hpp"

#include <algorithm>
#include <numeric>

namespace yipe {
namespace {

int random_slot_symbol(MsvcRandom& random) {
    const int roll = random.next() % 21;
    if (roll < 6) {
        return 5;
    }
    if (roll < 11) {
        return 15;
    }
    if (roll < 15) {
        return 35;
    }
    if (roll < 18) {
        return 60;
    }
    if (roll < 20) {
        return 80;
    }
    return 150;
}

int forced_slot_symbol(int roll) {
    if (roll < 64) {
        return 0;
    }
    if (roll < 74) {
        return 15;
    }
    if (roll < 79) {
        return 35;
    }
    if (roll < 83) {
        return 60;
    }
    if (roll < 85) {
        return 80;
    }
    if (roll == 85) {
        return 150;
    }
    return 5;
}

int final_slot_symbol(int forced_symbol, MsvcRandom& random) {
    return forced_symbol != 0 ? forced_symbol : random_slot_symbol(random);
}

} // namespace

WagerStatus place_casino_wager(CasinoWager& wager, int amount) {
    if (amount <= 0) {
        return WagerStatus::invalid_amount;
    }
    if (wager.bet + amount > maximum_casino_bet) {
        return WagerStatus::maximum_reached;
    }
    if (wager.gold < amount) {
        return WagerStatus::insufficient_gold;
    }
    wager.gold -= amount;
    wager.bet += amount;
    return WagerStatus::placed;
}

void clear_casino_wager(CasinoWager& wager) {
    wager.gold += wager.bet;
    wager.bet = 0;
}

bool insert_slot_credits(SlotMachineState& state) {
    if (state.gold < 10) {
        return false;
    }
    state.gold -= 10;
    state.credits += 10;
    return true;
}

void collect_slot_credits(SlotMachineState& state) {
    state.gold += state.credits;
    state.credits = 0;
}

SlotSpinResult spin_slots(SlotMachineState& state, MsvcRandom& random) {
    SlotSpinResult result;
    if (state.credits < 10) {
        return result;
    }
    state.credits -= 10;
    result.spun = true;

    const int forced_symbol = forced_slot_symbol(random.next() % 100);
    for (int frame = 0; frame < 25; ++frame) {
        if (frame < 17) {
            result.symbols[0] = frame == 16
                ? final_slot_symbol(forced_symbol, random)
                : random_slot_symbol(random);
        }
        if (frame < 21) {
            result.symbols[1] = frame == 20
                ? final_slot_symbol(forced_symbol, random)
                : random_slot_symbol(random);
        }
        result.symbols[2] = frame == 24
            ? final_slot_symbol(forced_symbol, random)
            : random_slot_symbol(random);
    }

    if (result.symbols[0] == result.symbols[1]
        && result.symbols[0] == result.symbols[2]) {
        result.payout = result.symbols[0];
        state.credits += result.payout;
    }
    return result;
}

DotRaceResult race_dots(CasinoWager& wager, int selected_dot,
                        MsvcRandom& random) {
    DotRaceResult result;
    if (wager.bet <= 0 || selected_dot < 1 || selected_dot > 3) {
        return result;
    }
    result.raced = true;
    const int bet = wager.bet;
    wager.bet = 0;

    // The dialog's horizontal unit is two pixels: the three dots start at
    // right edges 35, 36, and 36 and race to the line at 241 dialog units.
    result.final_positions = {70, 72, 72};
    while (*std::max_element(result.final_positions.begin(),
                             result.final_positions.end()) < 482) {
        for (int& position : result.final_positions) {
            position += random.next() % 15;
        }
    }

    if (result.final_positions[0] > result.final_positions[1]
        && result.final_positions[0] > result.final_positions[2]) {
        result.winner = 1;
    } else if (result.final_positions[1] > result.final_positions[0]
               && result.final_positions[1] > result.final_positions[2]) {
        result.winner = 2;
    } else {
        result.winner = 3;
    }
    if (result.winner == selected_dot) {
        result.payout = bet * 250 / 100;
        wager.gold += result.payout;
    }
    return result;
}

int adjust_dart_aim(int current_y, bool upward) {
    if (upward) {
        return std::max(16, current_y - 5);
    }
    return current_y > 75 ? current_y : current_y + 5;
}

DartThrowResult throw_dart(int aim_y, int throw_index, MsvcRandom& random) {
    DartThrowResult result;
    result.final_y = aim_y;
    // Dialog 461 uses the same two-pixel horizontal unit. The dart begins at
    // right edge 29 and the board begins at 225 dialog units.
    int right = 58;
    while (right < 450) {
        right += random.next() % 15 + 5;
        const int drift = random.next() % 100;
        if (drift > 55) {
            result.final_y += random.next() % 3 + 1;
        } else if (drift < 45) {
            result.final_y -= random.next() % 3 + 1;
        }
        result.final_y = std::clamp(result.final_y, 16, 75);
    }

    const bool triple = random.next() % 100 > 90;
    int double_threshold = result.final_y == 51 ? 74 : 84;
    if (throw_index == 1) {
        double_threshold -= 4;
    }
    const bool double_score = random.next() % 100 > double_threshold;

    if (result.final_y < 24 || result.final_y > 67) {
        return result;
    }
    if (result.final_y >= 42 && result.final_y <= 50) {
        result.score = result.final_y >= 45 && result.final_y <= 47 ? 50 : 25;
        return result;
    }
    result.score = result.final_y >= 35 && result.final_y <= 56
        ? random.next() % 11 + 10
        : random.next() % 9 + 1;
    if (double_score) {
        result.score *= 2;
    } else if (triple) {
        result.score *= 3;
    }
    return result;
}

int dart_round_payout(const std::array<int, 3>& scores, int bet) {
    if (bet <= 0) {
        return 0;
    }
    const int total = std::accumulate(scores.begin(), scores.end(), 0);
    if (total == 150) {
        return bet * 10;
    }
    if (total >= 120) {
        return bet * 8;
    }
    if (total >= 105) {
        return bet * 6;
    }
    if (total >= 88) {
        return bet * 4;
    }
    if (total >= 73) {
        return bet * 2;
    }
    return total >= 55 ? bet : 0;
}

} // namespace yipe
