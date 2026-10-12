using System.Diagnostics;
using System.Text.Json;
using Yipe.Core;
using Yipe.Desktop;

internal static class ReleasePersistenceChecks
{
    public static async Task Restart(string operation, string directory)
    {
        var catalog = GameSaveChecks.Catalog();
        if (operation == "--interrupt-save-loop")
        {
            var game = new GameSession(catalog);
            var store = new GameSaveStore(Path.Combine(directory, "interrupted.y3m"), catalog);
            for (var i = 1; ; i++)
            {
                game.Player.Gold = i;
                await store.SaveAsync(game);
                if (i == 1) { Console.WriteLine("READY"); Console.Out.Flush(); }
            }
        }
        foreach (var id in new[] { 17, 42, 49, 56, 117, 118, 119, 120 })
        {
            var path = Path.Combine(directory, $"room-{id}.y3m");
            var store = new GameSaveStore(path, catalog);
            if (operation == "--write-room-fixtures")
            {
                var layer = id >= 99 ? 0 : id >= 41 ? 1 : 0;
                var cell = id - 1 - layer * 40 - (id >= 99 ? 98 : 0);
                var location = new WorldLocation { MapId = id, MapLayer = layer, AlternateMapSet = id >= 99,
                    RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = new(5, 5) };
                var game = new GameSession(catalog, location: location, randomState: 12345);
                if (game.CurrentMap.Events.Overlay.Value > 0)
                    game.Location.Position = game.CurrentMap.Events.Overlay.Position;
                await store.SaveAsync(game);
                // Original entry rules regenerate generated rooms and reset hazard phase;
                // the established C++ import checks separately verify those rules.
                var resumed = GameSaveCodec.Decode(File.ReadAllBytes(path), catalog);
                File.WriteAllText(path + ".expected", ObserveContinuation(resumed, catalog));
            }
            else if (ObserveContinuation(await store.LoadAsync(), catalog) != File.ReadAllText(path + ".expected"))
                throw new InvalidOperationException($"Room {id}: fresh-process map/timer/platform/RNG continuation differs.");
        }
        Console.WriteLine(operation + " passed (eight normal/Hell/generated/timed/platform rooms).");
    }

    private static string ObserveContinuation(GameSession session, MapCatalog catalog)
    {
        var adventure = new Adventure(session);
        var snapshots = new List<object>();
        for (var tick = 0; tick < 80; tick++)
        {
            snapshots.Add(new {
                save = Convert.ToBase64String(GameSaveCodec.Encode(session, catalog)),
                tiles = Enumerable.Range(0, 13).SelectMany(x => Enumerable.Range(0, 12).Select(y => session.CurrentMap.Tile(new(x, y)))).ToArray(),
                overlay = session.CurrentMap.Events.Overlay.ToString(),
            });
            foreach (var unused in adventure.Tick()) { }
        }
        return JsonSerializer.Serialize(snapshots);
    }

    public static async Task Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "yipe-release-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await GameSaveChecks.RunChild("--write-room-fixtures", directory);
            await GameSaveChecks.RunChild("--read-room-fixtures", directory);
            check(true, "Fresh processes preserve eight room restorations and 80 timer/platform/RNG continuation ticks each");
            var path = Path.Combine(directory, "interrupted.y3m");
            var catalog = GameSaveChecks.Catalog();
            var store = new GameSaveStore(path, catalog);
            await store.SaveAsync(new GameSession(catalog));
            var info = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, UseShellExecute = false };
            if (Path.GetFileNameWithoutExtension(info.FileName) == "dotnet") info.ArgumentList.Add(typeof(ReleasePersistenceChecks).Assembly.Location);
            info.ArgumentList.Add("--interrupt-save-loop"); info.ArgumentList.Add(directory);
            using (var child = Process.Start(info)!)
            {
                try
                {
                    var ready = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
                    if (ready != "READY") throw new InvalidOperationException("Saving child did not become ready.");
                    child.Kill(); await child.WaitForExitAsync();
                }
                finally { if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(); } }
            }
            check((await store.LoadAsync()).Player.Gold >= 1,
                "Abruptly terminating a process repeatedly saving leaves a complete committed adventure");
            var committed = File.ReadAllBytes(path);
            // Separately model the partial staging file left by termination during a write.
            File.WriteAllBytes(Path.Combine(directory, ".interrupted.y3m.abandoned.tmp"), committed[..20]);
            check(GameSaveCodec.Encode(await store.LoadAsync(), catalog).SequenceEqual(committed),
                "A partial orphan staging file is never mistaken for the committed save");
            await store.SaveAsync(await store.LoadAsync());
            check(File.ReadAllBytes(path).SequenceEqual(committed), "Saving after an interrupted writer retains valid progress");
        }
        finally { Directory.Delete(directory, true); }
    }
}
