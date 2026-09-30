#pragma once

struct SDL_Surface;

namespace yipe_app {

// The macOS print panel includes the page preview and Save as PDF controls.
void show_print_dialog(const SDL_Surface& image);
void show_page_setup();

} // namespace yipe_app
