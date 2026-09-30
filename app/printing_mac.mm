#include "printing.hpp"

#import <AppKit/AppKit.h>
#include <SDL.h>

#include <cstring>
#include <limits>
#include <stdexcept>

@interface YipePrintView : NSView
@property(strong) NSImage* image;
@end

@implementation YipePrintView
- (void)drawRect:(NSRect)dirtyRect {
    static_cast<void>(dirtyRect);
    [NSGraphicsContext currentContext].imageInterpolation = NSImageInterpolationNone;
    [self.image drawInRect:self.bounds];
}
@end

namespace {

NSPrintOperation* game_print_operation(const SDL_Surface& source) {
    if (!source.format || source.format->format != SDL_PIXELFORMAT_RGBA32
        || !source.pixels || source.w <= 0 || source.h <= 0
        || source.w > std::numeric_limits<int>::max() / 4 || source.pitch < source.w * 4) {
        throw std::runtime_error("printing requires a valid RGBA game image");
    }
    NSBitmapImageRep* bitmap = [[NSBitmapImageRep alloc]
        initWithBitmapDataPlanes:nullptr pixelsWide:source.w pixelsHigh:source.h
        bitsPerSample:8 samplesPerPixel:4 hasAlpha:YES isPlanar:NO
        colorSpaceName:NSDeviceRGBColorSpace bitmapFormat:NSBitmapFormatAlphaNonpremultiplied
        bytesPerRow:source.w * 4 bitsPerPixel:32];
    if (!bitmap || !bitmap.bitmapData) {
        throw std::runtime_error("could not allocate the printable game image");
    }
    for (int row = 0; row < source.h; ++row) {
        std::memcpy(bitmap.bitmapData + row * bitmap.bytesPerRow,
                    static_cast<const unsigned char*>(source.pixels)
                        + static_cast<std::size_t>(row) * static_cast<std::size_t>(source.pitch),
                    static_cast<std::size_t>(source.w) * 4);
    }
    bitmap = [bitmap bitmapImageRepByRetaggingWithColorSpace:[NSColorSpace sRGBColorSpace]];
    if (!bitmap) throw std::runtime_error("could not set the print image's RGB color space");
    NSImage* image = [[NSImage alloc] initWithSize:NSMakeSize(source.w, source.h)];
    [image addRepresentation:bitmap];
    YipePrintView* view = [[YipePrintView alloc]
        initWithFrame:NSMakeRect(0, 0, source.w, source.h)];
    view.image = image;
    NSPrintInfo* info = [[NSPrintInfo sharedPrintInfo] copy];
    info.horizontalPagination = NSPrintingPaginationModeFit;
    info.verticalPagination = NSPrintingPaginationModeFit;
    info.horizontallyCentered = YES;
    info.verticallyCentered = YES;
    // Keep paper/printer preferences, but never reuse a previous PDF destination.
    info.jobDisposition = NSPrintSpoolJob;
    [info.dictionary removeObjectForKey:NSPrintJobSavingURL];
    NSPrintOperation* operation = [NSPrintOperation printOperationWithView:view printInfo:info];
    operation.jobTitle = @"Yipe! III - Current Map";
    operation.printPanel.options |= NSPrintPanelShowsPreview | NSPrintPanelShowsPaperSize
        | NSPrintPanelShowsOrientation | NSPrintPanelShowsScaling;
    return operation;
}

} // namespace

namespace yipe_app {

void show_print_dialog(const SDL_Surface& image) {
    @autoreleasepool {
        NSPrintOperation* operation = game_print_operation(image);
        // AppKit presents printing errors; NO also covers ordinary cancellation.
        if ([operation runOperation]) {
            [NSPrintInfo setSharedPrintInfo:operation.printInfo];
        }
    }
}

void show_page_setup() {
    @autoreleasepool {
        NSPrintInfo* info = [[NSPrintInfo sharedPrintInfo] copy];
        if ([[NSPageLayout pageLayout] runModalWithPrintInfo:info] == NSModalResponseOK) {
            [NSPrintInfo setSharedPrintInfo:info];
        }
    }
}

} // namespace yipe_app
