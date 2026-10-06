#include "yipe/item_names.hpp"
#include <iostream>
int main() {
    for (int mode = 0; mode < 3; ++mode) {
        const auto names = mode == 1 ? yipe::load_item_names("assets/original/Yipe3Items.txt") : yipe::embedded_item_names(mode == 2);
        for (std::size_t i = 0; i < names.armor.size(); ++i) std::cout << mode << "|A|" << i + 1 << '|' << names.armor[i] << '\n';
        for (std::size_t i = 0; i < names.weapons.size(); ++i) std::cout << mode << "|W|" << i + 1 << '|' << names.weapons[i] << '\n';
    }
}
