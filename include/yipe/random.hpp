#pragma once

#include <cstdint>

namespace yipe {

// Visual C++ rand() used by the original executable at 0x004583c8.
class MsvcRandom {
public:
    explicit MsvcRandom(std::uint32_t seed = 1) : state_(seed) {}

    [[nodiscard]] int next();
    [[nodiscard]] std::uint32_t state() const { return state_; }

private:
    std::uint32_t state_;
};

} // namespace yipe
