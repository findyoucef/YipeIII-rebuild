#include "file_dialog.hpp"

#import <AppKit/AppKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

#include <string>

namespace yipe_app {

std::vector<std::filesystem::path> recent_game_files() {
    @autoreleasepool {
        std::vector<std::filesystem::path> files;
        for (NSURL* url in [[NSDocumentController sharedDocumentController] recentDocumentURLs]) {
            if ([url isFileURL]) files.emplace_back([[url path] fileSystemRepresentation]);
        }
        return files;
    }
}

void remember_game_file(const std::filesystem::path& path) {
    @autoreleasepool {
        const auto absolute = std::filesystem::absolute(path).lexically_normal().string();
        NSURL* url = [NSURL fileURLWithFileSystemRepresentation:absolute.c_str()
                                                  isDirectory:NO relativeToURL:nil];
        [[NSDocumentController sharedDocumentController] noteNewRecentDocumentURL:url];
    }
}

void clear_recent_game_files() {
    @autoreleasepool {
        [[NSDocumentController sharedDocumentController] clearRecentDocuments:nil];
    }
}

std::optional<std::filesystem::path> choose_open_game_file(
    const std::filesystem::path& initial_directory) {
    @autoreleasepool {
        NSOpenPanel* panel = [NSOpenPanel openPanel];
        [panel setCanChooseDirectories:NO];
        [panel setAllowsMultipleSelection:NO];
        [panel setAllowedContentTypes:@[
            [UTType typeWithFilenameExtension:@"y3r"],
            [UTType typeWithFilenameExtension:@"yp3"]]];
        [panel setMessage:@"Open a native Yipe! III save or import an original .yp3 save."];
        const std::string directory = initial_directory.string();
        [panel setDirectoryURL:[NSURL fileURLWithPath:
            [NSString stringWithUTF8String:directory.c_str()] isDirectory:YES]];
        if ([panel runModal] != NSModalResponseOK) {
            return std::nullopt;
        }
        return std::filesystem::path([[[panel URL] path] UTF8String]);
    }
}

std::optional<std::filesystem::path> choose_save_game_file(
    const std::filesystem::path& suggested_path) {
    @autoreleasepool {
        NSSavePanel* panel = [NSSavePanel savePanel];
        [panel setAllowedContentTypes:@[[UTType typeWithFilenameExtension:@"y3r"]]];
        [panel setMessage:@"Save this game in the rebuild's native .y3r format."];
        const std::string directory = suggested_path.parent_path().string();
        const std::string name = suggested_path.filename().string();
        [panel setDirectoryURL:[NSURL fileURLWithPath:
            [NSString stringWithUTF8String:directory.c_str()] isDirectory:YES]];
        [panel setNameFieldStringValue:[NSString stringWithUTF8String:name.c_str()]];
        if ([panel runModal] != NSModalResponseOK) {
            return std::nullopt;
        }
        return std::filesystem::path([[[panel URL] path] UTF8String]);
    }
}

} // namespace yipe_app
