// Independent C++ oracle for the C# town movement parity check.
// From the repository root after building yipe_core:
// c++ -std=c++20 -I include monogame/Yipe.Tests/town_reference.cpp \
//     build/libyipe_core.a -o /tmp/yipe-town-reference
// /tmp/yipe-town-reference | shasum -a 256
#include "yipe/map.hpp"
#include <array>
#include <iostream>

int main() {
    const auto map = yipe::starting_map();
    constexpr std::array directions{
        yipe::MapPosition{-1, 0}, yipe::MapPosition{1, 0},
        yipe::MapPosition{0, -1}, yipe::MapPosition{0, 1},
    };
    for (int x = 0; x < yipe::Map::width; ++x) {
        for (int y = 0; y < yipe::Map::height; ++y) {
            for (const auto direction : directions) {
                yipe::MapPosition position{x, y};
                const auto result = yipe::try_move(map, position, direction.x, direction.y);
                std::cout << x << ',' << y << ',' << direction.x << ',' << direction.y
                          << ',' << result.original_code << ',' << position.x << ','
                          << position.y << ',' << result.position_changed << '\n';
            }
        }
    }
}
