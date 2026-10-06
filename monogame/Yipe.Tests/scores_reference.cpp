#include "yipe/auxiliary_data.hpp"
#include "yipe/player.hpp"
#include "yipe/audio.hpp"
#include <iostream>
#include <limits>
int main() {
    for (int difficulty : {0, 1, 2, 3, -1, 65538, 65539}) {
        auto scores = yipe::default_high_scores();
        for (int step = 0; step < 60; ++step) {
            yipe::Player p;
            p.name = "Hero" + std::to_string(step % 7);
            p.difficulty = difficulty;
            p.level = step % 20 + 1;
            p.gold = step % 6 * 100;
            p.quest_progress_total = step % 13 * 7;
            for (int i = 0; i < step % 21; ++i) p.inventory_values[i] = 1;
            const bool changed = yipe::update_high_scores(scores, p, step % 9 == 0);
            std::cout << difficulty << ',' << step << ',' << changed;
            for (const auto& entry : scores) std::cout << '|' << entry.name << ':' << entry.value;
            std::cout << '\n';
        }
    }
    for (int v : {std::numeric_limits<int>::min(), -1, 0, 1, 15, 16, 60, 61, 450, 451, std::numeric_limits<int>::max()})
        for (unsigned gate : {0u, 1u, 3u, 32768u, 65535u}) for (int difficulty : {1, 2, 3, 65538}) {
            yipe::Player p; p.level = v; p.gold = v; p.difficulty = difficulty;
            p.gatekeeper_state = static_cast<std::uint16_t>(gate); p.value_06a4 = gate == 3 ? 5 : 0;
            p.monster_defeats.fill(v);
            std::cout << v << ',' << gate << ',' << difficulty << '|' << yipe::high_score_code(p) << '\n';
        }
    for (const auto& effect : yipe::sound_effects()) std::cout << "audio|" << effect.id << '|' << effect.asset_name << '\n';
    for (int type = 1; type <= 41; ++type) std::cout << "monster|" << type << '|' << yipe::monster_sound_file_id(type) << '\n';
    for (int index = 0; index < 12; ++index) std::cout << "spell|" << index << '|' << yipe::spell_sound_id(index) << '\n';
}
