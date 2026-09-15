#include "yipe/random.hpp"

namespace yipe {

int MsvcRandom::next() {
    state_ = state_ * 0x343fdu + 0x269ec3u;
    return static_cast<int>((state_ >> 16u) & 0x7fffu);
}

} // namespace yipe
