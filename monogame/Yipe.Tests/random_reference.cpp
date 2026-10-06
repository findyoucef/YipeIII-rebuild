// Compile with src/random.cpp to exercise the C++ implementation independently of C#.
// Reproduction commands and the reference digest are in Yipe.Tests/README.md.
#include "yipe/random.hpp"
#include <array>
#include <cstdint>
#include <iostream>
#include <locale>

int main() {
    std::cout.imbue(std::locale::classic());
    constexpr std::array<std::uint32_t, 8> seeds{
        0u, 1u, 2u, 0x7fffffffu, 0x80000000u, 0xffffffffu, 0xdeadbeefu, 2566201570u,
    };
    for (const auto seed : seeds) {
        yipe::MsvcRandom random(seed);
        for (int step = 0; step < 1024; ++step) {
            const int value = random.next();
            std::cout << seed << ',' << step << ',' << value << ',' << random.state() << '\n';
        }
    }
}
