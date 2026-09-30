// Use a separate application identity so native recent-document tests never
// read or clear the game's recent documents. CTest runs the phases in separate
// processes to check that AppKit persists the list across launches.
#include "../app/file_dialog.hpp"
#import <AppKit/AppKit.h>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string_view>

int main(int argc, char* argv[]) {
    @autoreleasepool {
        try {
            if (argc != 3 || ![[[NSBundle mainBundle] bundleIdentifier]
                    isEqualToString:@"org.yipe3rebuild.tests.filehistory"]) {
                throw std::runtime_error("recent-file tests require their isolated application bundle");
            }
            [NSApplication sharedApplication];
            const std::filesystem::path directory = argv[2];
            const auto first = directory / "Hero with spaces.y3r";
            const auto second = directory / "Héroïne.yp3";
            const std::string_view phase = argv[1];
            if (phase == "clear") {
                yipe_app::clear_recent_game_files();
                if (!yipe_app::recent_game_files().empty()) {
                    throw std::runtime_error("clear left recent documents behind");
                }
                std::filesystem::remove_all(directory);
            } else {
                if (phase == "write") {
                    yipe_app::clear_recent_game_files();
                    std::filesystem::create_directories(directory);
                    std::ofstream{first} << "native history fixture";
                    std::ofstream{second} << "import history fixture";
                    yipe_app::remember_game_file(first);
                    yipe_app::remember_game_file(second);
                    // Launch Services timestamps recent usage at a coarser
                    // resolution than these back-to-back test operations.
                    [[NSRunLoop currentRunLoop] runUntilDate:[NSDate dateWithTimeIntervalSinceNow:1.1]];
                    yipe_app::remember_game_file(directory / "." / first.filename());
                } else if (phase != "read") {
                    throw std::runtime_error("unknown test phase");
                }
                const auto files = yipe_app::recent_game_files();
                if (files.size() != 2 || !std::filesystem::equivalent(files[0], first)
                    || !std::filesystem::equivalent(files[1], second)) {
                    for (const auto& file : files) std::cerr << file << '\n';
                    throw std::runtime_error("recent files lost order, deduplication, path text, or persistence");
                }
            }
            std::cout << phase << " passed\n";
            return 0;
        } catch (const std::exception& error) {
            std::cerr << error.what() << '\n';
            return 1;
        }
    }
}
