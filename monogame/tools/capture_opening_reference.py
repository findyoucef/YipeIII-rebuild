#!/usr/bin/env python3
"""Run the unchanged original application tests and export their 42 earned saves."""
from pathlib import Path
import os
import shlex
import subprocess

root = Path(__file__).resolve().parents[2]
build = root / 'build/monogame-reference'
build.mkdir(parents=True, exist_ok=True)
source = (root / 'tests/app_shortcut_tests.cpp').read_text()
source = source.replace('int main(int argc, char* argv[]) {', 'int shortcut_reference_main(int argc, char* argv[]) {')
source = source.replace('#include "../app/main.cpp"', '#include "' + str(root / 'app/main.cpp') + '"')
source = source.replace('#include "full_game_driver.hpp"', '#include "' + str(root / 'tests/full_game_driver.hpp') + '"')
source += r'''
int main(int argc, char* argv[]) {
    if (argc != 2) return 2;
    const std::filesystem::path output = argv[1];
    char* test_args[] = {argv[0], nullptr};
    const int result = shortcut_reference_main(1, test_args);
    if (result != 0) return result;
    if (route_checkpoints.size() != 42) throw std::runtime_error("missing earned checkpoints");
    std::filesystem::create_directories(output);
    for (std::size_t i=0; i<route_checkpoints.size(); ++i) {
        yipe::save_game_atomic(output / (std::to_string(i) + ".y3r"), route_checkpoints[i]);
    }
    std::cout << "Exported 42 earned opening checkpoints.\n";
    // Reopen earned progress through the real application and visit the King.
    test_directory = output / "return-run";
    std::filesystem::create_directories(test_directory);
    const SdlSession sdl;
    const RuntimeAssets assets = runtime_assets(argv[0]);
    Window window(SDL_CreateWindow("Earned King return", 0, 0, map_width, map_height, SDL_WINDOW_HIDDEN), SDL_DestroyWindow);
    Renderer renderer(SDL_CreateRenderer(window.get(), -1, SDL_RENDERER_SOFTWARE), SDL_DestroyRenderer);
    const GameTextures textures = load_game_textures(renderer.get(), assets.bitmaps);
    AudioPlayer audio(assets); yipe::GameSettings settings; settings.sound_enabled = false;
    auto scores = yipe::default_high_scores();
    scripted_clock = true; pending_ticks = 0; scripted_now = 0; route_combat = false;
    dialog_choices = {{"Spells", 1, "Return to Surface"}};
    SDL_FlushEvents(SDL_FIRSTEVENT, SDL_LASTEVENT);
    auto key = [](SDL_Keycode sym) { SDL_Event e{}; e.type=SDL_KEYDOWN; e.key.keysym.sym=sym; if(SDL_PushEvent(&e)!=1)throw std::runtime_error(SDL_GetError()); };
    key(SDLK_c);
    key(SDLK_KP_3);
    for(int i=0;i<8;++i)key(SDLK_RIGHT);
    key(SDLK_KP_9);
    for(int i=0;i<3;++i)key(SDLK_UP);
    key(SDLK_F5); key(SDLK_q);
    auto path = output / "king-return.y3r";
    auto earned = yipe::load_game(output / "41.y3r");
    const auto exit = run_game(window.get(), renderer.get(), textures, earned, path,
        test_directory / "settings.y3r", test_directory / "scores.y3r", scores, settings, audio);
    auto returned = yipe::load_game(path);
    std::cout << "Return: map " << returned.location.map_id << " (" << returned.location.position.x << ',' << returned.location.position.y
              << ") gold " << returned.player.gold << " flags " << returned.player.quest_flags[0] << returned.player.quest_flags[1] << returned.player.quest_flags[2] << '\n';
    if (exit.choice != StartupChoice::quit || returned.player.gold != earned.player.gold + 500
        || !returned.player.quest_flags[2] || returned.location.map_id != 17)
        throw std::runtime_error("earned return did not receive the King's first-stamp reward");
    std::filesystem::remove_all(test_directory);
    std::cout << "Earned return to King: 500 gold verified.\n";
}
'''
cpp = build / 'opening_reference.cpp'
cpp.write_text(source)
exe = build / 'opening-reference'
sdl = shlex.split(subprocess.check_output(['pkg-config', '--cflags', '--libs', 'sdl2'], text=True))
subprocess.run(['c++', '-std=c++20', '-O1', '-I', str(root / 'include'),
    '-DYIPE_TEST_EVIDENCE_DIR="' + str(root / 'evidence/original-runtime') + '"', str(cpp),
    *map(str, sorted((root / 'src').glob('*.cpp'))), *map(str, sorted((root / 'src/generated').glob('*.cpp'))),
    *sdl, '-o', str(exe)], cwd=root, check=True)
subprocess.run([str(exe), str(root / 'monogame/Yipe.Tests/Fixtures/Opening/Reference')], cwd=root,
    env={**os.environ, 'SDL_VIDEODRIVER': 'dummy', 'SDL_AUDIODRIVER': 'dummy'}, check=True)
