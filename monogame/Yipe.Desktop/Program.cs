using Yipe.Core;

namespace Yipe.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--import-save")
        {
            try
            {
                using var maps = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
                var store = new GameSaveStore(args[2], MapCatalog.Load(maps));
                store.ImportAsync(args[1]).GetAwaiter().GetResult();
                Console.WriteLine($"Created {Path.GetFullPath(args[2])}. Source unchanged.");
                Console.WriteLine(Path.GetExtension(args[1]).ToLowerInvariant() switch
                {
                    ".json" => "Preview migration creates a default Warrior at the saved position; no character progress existed in this format.",
                    ".yp3" => "Original import preserves mapped state. RNG starts at 1; absent/unknown fields use the C++ defaults. Keep the original file.",
                    _ => "Imported C++ version 20 persistent state. Map-entry behavior runs when the new save is loaded.",
                });
                Console.WriteLine("See SAVE_FORMAT.md for supported import formats and inherited save behavior.");
                return 0;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                Console.Error.WriteLine($"Import failed: {error.Message}");
                return 1;
            }
        }
        var smoke = args.Length >= 1 && args[0] == "--smoke-test";
        var remaining = smoke ? args[1..] : args;
        string? screenshot = null;
        if (smoke && remaining.Length > 0 && !remaining[0].StartsWith("--"))
        { screenshot = remaining[0]; remaining = remaining[1..]; }
        var selectedSave = remaining.Length == 2 && remaining[0] == "--load-save" ? remaining[1] : null;
        if (remaining.Length != 0 && selectedSave is null)
        {
            Console.Error.WriteLine("Usage: Yipe.Desktop [--smoke-test [screenshot.png]] [--load-save game.y3m] | --import-save source destination.y3m");
            return 2;
        }
        try
        {
            using var game = new TownGame(smoke, screenshot, selectedSave);
            game.Run();
            if (smoke) Console.WriteLine("MonoGame graphics smoke test passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Yipe! III could not start: {error.Message}");
            return 1;
        }
    }
}
