#include "printing.hpp"

#include <stdexcept>

namespace yipe_app {

void show_print_dialog(const SDL_Surface&) {
    throw std::runtime_error("printing and preview are not implemented on this platform");
}

void show_page_setup() {
    throw std::runtime_error("page setup is not implemented on this platform");
}

} // namespace yipe_app
