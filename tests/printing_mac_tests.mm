// Exercise the native print operation itself without submitting a printer job.
#include "../app/printing_mac.mm"

#include <cmath>
#include <algorithm>
#include <array>
#include <filesystem>
#include <iostream>
#include <memory>
#include <vector>

namespace {
void require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}

void check_pdf(NSURL* url, NSSize expected_paper, NSPrintInfo* info, double expected_scale) {
    CGPDFDocumentRef document = CGPDFDocumentCreateWithURL((__bridge CFURLRef)url);
    require(document != nullptr, "native printing did not produce a readable PDF");
    require(CGPDFDocumentGetNumberOfPages(document) == 1, "map printing must fit one page");
    CGPDFPageRef page = CGPDFDocumentGetPage(document, 1);
    const CGRect paper = CGPDFPageGetBoxRect(page, kCGPDFMediaBox);
    require(std::abs(paper.size.width - expected_paper.width) < 1
                && std::abs(paper.size.height - expected_paper.height) < 1,
            "printed page ignored paper size/orientation");
    const auto width = static_cast<std::size_t>(std::ceil(paper.size.width));
    const auto height = static_cast<std::size_t>(std::ceil(paper.size.height));
    std::vector<unsigned char> pixels(width * height * 4, 255);
    CGColorSpaceRef colors = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
    CGContextRef context = CGBitmapContextCreate(pixels.data(), width, height, 8,
        width * 4, colors, static_cast<CGBitmapInfo>(kCGImageAlphaPremultipliedLast)
                              | kCGBitmapByteOrder32Big);
    CGColorSpaceRelease(colors);
    require(context != nullptr, "could not rasterize printed page");
    CGContextDrawPDFPage(context, page);
    CGContextRelease(context);
    CGPDFDocumentRelease(document);
    std::array<std::size_t, 4> counts{};
    std::array<std::size_t, 4> sum_x{}, sum_y{};
    std::size_t left = width, right = 0, bottom = height, top = 0;
    for (std::size_t y = 0; y < height; ++y) {
        for (std::size_t x = 0; x < width; ++x) {
            const auto* pixel = pixels.data() + (y * width + x) * 4;
            // ColorSync can change channel values during PDF rasterization;
            // classify the four saturated colors while measuring their geometry.
            const int color = pixel[0] > 200 && pixel[1] < 80 && pixel[2] < 80 ? 0
                : pixel[0] < 80 && pixel[1] > 200 && pixel[2] < 80 ? 1
                : pixel[0] < 80 && pixel[1] < 80 && pixel[2] > 200 ? 2
                : pixel[0] > 200 && pixel[1] > 200 && pixel[2] < 80 ? 3 : -1;
            if (color < 0) continue;
            ++counts[static_cast<std::size_t>(color)];
            sum_x[static_cast<std::size_t>(color)] += x;
            sum_y[static_cast<std::size_t>(color)] += y;
            left = std::min(left, x); right = std::max(right, x);
            bottom = std::min(bottom, y); top = std::max(top, y);
        }
    }
    const double expected_area = 416 * 384 * expected_scale * expected_scale / 4;
    for (const auto count : counts) {
        if (std::abs(static_cast<double>(count) - expected_area) >= expected_area * 0.03) {
            throw std::runtime_error("PDF quadrant area " + std::to_string(count)
                + " differs from " + std::to_string(expected_area));
        }
    }
    require(sum_x[0] / counts[0] < sum_x[1] / counts[1]
                && sum_x[2] / counts[2] < sum_x[3] / counts[3]
                && sum_y[0] / counts[0] < sum_y[2] / counts[2]
                && sum_y[1] / counts[1] < sum_y[3] / counts[3],
            "PDF flipped or transposed the game image");
    require(std::abs(static_cast<double>(right - left + 1) - 416 * expected_scale) < 2
                && std::abs(static_cast<double>(top - bottom + 1) - 384 * expected_scale) < 2,
            "PDF distorted the map or ignored scaling/fit");
    const NSRect margins = NSMakeRect(info.leftMargin, info.bottomMargin,
        paper.size.width - info.leftMargin - info.rightMargin,
        paper.size.height - info.topMargin - info.bottomMargin);
    const NSRect printable = NSIntersectionRect(margins, info.imageablePageBounds);
    if (std::abs(static_cast<double>(left + right + 1) / 2 - NSMidX(printable)) >= 2
        || std::abs(static_cast<double>(top + bottom + 1) / 2
                    - (paper.size.height - NSMidY(printable))) >= 2) {
        throw std::runtime_error("PDF map bounds " + std::to_string(left) + ","
            + std::to_string(bottom) + " to " + std::to_string(right) + "," + std::to_string(top)
            + " were not centered inside the printable page margins");
    }
}
}

int main(int argc, char* argv[]) {
    try {
        require(argc == 2 || (argc == 3 && std::string_view(argv[2]) == "--cancel-panels"),
                "expected a PDF output directory and optional --cancel-panels");
        @autoreleasepool {
            [NSApplication sharedApplication];
            const std::filesystem::path output = std::filesystem::absolute(argv[1]);
            std::filesystem::create_directories(output);
            std::unique_ptr<SDL_Surface, decltype(&SDL_FreeSurface)> surface(
                SDL_CreateRGBSurfaceWithFormat(0, 416, 384, 32, SDL_PIXELFORMAT_RGBA32),
                SDL_FreeSurface);
            require(surface != nullptr, "could not create a test map image");
            for (int scenario = 0; scenario < 4; ++scenario) {
                const std::array<Uint32, 4> colors{
                    SDL_MapRGBA(surface->format, 255, 0, 0, 255),
                    SDL_MapRGBA(surface->format, 0, 255, 0, 255),
                    SDL_MapRGBA(surface->format, 0, 0, 255, 255),
                    SDL_MapRGBA(surface->format, 255, 255, 0, 255)};
                for (int quadrant = 0; quadrant < 4; ++quadrant) {
                    const SDL_Rect rect{quadrant % 2 * 208, quadrant / 2 * 192, 208, 192};
                    require(SDL_FillRect(surface.get(), &rect, colors[static_cast<std::size_t>(quadrant)]) == 0,
                            "could not fill the test image");
                }
                NSPrintOperation* operation = game_print_operation(*surface);
                require((operation.printPanel.options & NSPrintPanelShowsPreview) != 0,
                        "native print panel omitted the preview");
                require(operation.printInfo.jobDisposition == NSPrintSpoolJob
                            && operation.printInfo.dictionary[NSPrintJobSavingURL] == nil,
                        "a new print operation reused an earlier PDF destination");
                NSPrintInfo* info = operation.printInfo;
                info.orientation = NSPaperOrientationPortrait;
                info.paperSize = scenario == 2 ? NSMakeSize(300, 300) : NSMakeSize(612, 792);
                if (scenario == 1) info.orientation = NSPaperOrientationLandscape;
                info.leftMargin = info.rightMargin = info.topMargin = info.bottomMargin = 36;
                info.scalingFactor = scenario == 3 ? 0.5 : 1;
                info.jobDisposition = NSPrintSaveJob;
                const auto path = output / ("map-" + std::to_string(scenario) + ".pdf");
                NSURL* url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path.c_str()]];
                info.dictionary[NSPrintJobSavingURL] = url;
                operation.showsPrintPanel = NO;
                operation.showsProgressPanel = NO;
                // The operation must own its pixels after capture has returned.
                SDL_FillRect(surface.get(), nullptr, SDL_MapRGBA(surface->format, 255, 255, 255, 255));
                require([operation runOperation], "native PDF print operation failed");
                const double scale = scenario == 2 ? 228.0 / 416 : scenario == 3 ? 0.5 : 1;
                const NSSize expected_paper = scenario == 1 ? NSMakeSize(792, 612)
                    : scenario == 2 ? NSMakeSize(300, 300) : NSMakeSize(612, 792);
                check_pdf(url, expected_paper, info, scale);
                [NSPrintInfo setSharedPrintInfo:info];
            }
            SDL_Surface invalid{};
            bool rejected = false;
            try { game_print_operation(invalid); }
            catch (const std::runtime_error&) { rejected = true; }
            require(rejected, "invalid print images must report an error");
            if (argc == 3) {
                [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];
                [NSApp finishLaunching];
                for (bool setup : {true, false}) {
                    NSPrintInfo* before = [NSPrintInfo sharedPrintInfo];
                    const CGFloat scale = before.scalingFactor;
                    __block bool saw_panel = false;
                    NSTimer* timer = [NSTimer timerWithTimeInterval:0.1 repeats:YES
                        block:^(NSTimer* tick) {
                            if ([NSApp modalWindow]) {
                                saw_panel = true;
                                [NSApp stopModalWithCode:NSModalResponseCancel];
                                [tick invalidate];
                            }
                        }];
                    [[NSRunLoop mainRunLoop] addTimer:timer forMode:NSModalPanelRunLoopMode];
                    [[NSRunLoop mainRunLoop] addTimer:timer forMode:NSRunLoopCommonModes];
                    if (setup) yipe_app::show_page_setup();
                    else yipe_app::show_print_dialog(*surface);
                    [timer invalidate];
                    require(saw_panel && [NSPrintInfo sharedPrintInfo] == before
                                && before.scalingFactor == scale,
                            "native panel cancellation did not preserve print preferences");
                }
                std::cout << "Native Page Setup and Print/Preview panels opened and canceled\n";
            }
        }
        std::cout << "Native printing: preview options, PDF output, paper/orientation, scale/fit, pixels, and destination reset passed\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
