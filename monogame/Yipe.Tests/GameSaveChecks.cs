using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;
using Yipe.Desktop;

internal static class GameSaveChecks
{
    public static MapCatalog Catalog()
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
        return MapCatalog.Load(source);
    }

    // Deliberately varied values cover every persisted property, including metadata and
    // wrapped counters. Reflection is test-only, independent of the codec's field list.
    public static GameSession Fixture(MapCatalog catalog)
    {
        var p = new Player();
        foreach (var property in typeof(Player).GetProperties())
        {
            var number = property.Name.Sum(c => (int)c);
            if (property.PropertyType.IsArray)
            {
                var array = (Array)property.GetValue(p)!;
                for (var i = 0; i < array.Length; i++)
                {
                    object value = array switch
                    {
                        ushort[] => (ushort)((i + 1) * 1777 % 65536),
                        byte[] => (byte)(i % 16),
                        bool[] => i % 2 == 0,
                        int[] => i % 2 == 0 ? int.MinValue + i : int.MaxValue - i,
                        _ => throw new InvalidOperationException("Unknown persisted array."),
                    };
                    array.SetValue(value, i);
                }
            }
            else if (property.CanWrite)
            {
                object value = property.PropertyType == typeof(string) ? "Yipie 雪 🗡"
                    : property.PropertyType == typeof(bool) ? property.Name != nameof(Player.MonsterDefeatHistoryComplete)
                    : property.PropertyType == typeof(ushort) ? (ushort)(32768 + number)
                    : property.PropertyType == typeof(short) ? (short)-number
                    : property.PropertyType == typeof(SpecialAbility) ? SpecialAbility.Enrage
                    : property.PropertyType == typeof(HellGatekeeperState) ? HellGatekeeperState.Defeated
                    : number * 7;
                property.SetValue(p, value);
            }
        }
        p.Intelligence = 17; p.Level = 12; p.Difficulty = 3; p.Specialty = 2;
        p.Health = 4; p.MaximumHealth = 200; p.SpellPoints = 7; p.MaximumSpellPoints = 150;
        p.AbilityPoints = 2; p.MaximumAbilityPoints = 12; p.MiscellaneousShopStage = 4;
        p.PendingRelocation = 0; p.SurfaceReturnMapId = 21; p.SurfaceReturnRegionX = -2; p.SurfaceReturnRegionY = -1;
        p.Inventory[0] = 1; p.Inventory[28] = ushort.MaxValue; p.InventoryValues[0] = 32768;
        return new(catalog, p, new() { MapId = 61, RegionX = -2, RegionY = -1, MapLayer = 1, Position = new(8, 7) },
            uint.MaxValue, [new(17, new(6, 5), 0x27), new(61, new(10, 8), 0x27)], [0, 5, 185]);
    }

    public static async Task Restart(string operation, string path)
    {
        var catalog = Catalog();
        var store = new GameSaveStore(path, catalog);
        if (operation == "--write-save-fixture") await store.SaveAsync(Fixture(catalog));
        else
        {
            var loaded = await store.LoadAsync();
            var expected = Fixture(catalog);
            if (!GameSaveCodec.Encode(expected, catalog).SequenceEqual(GameSaveCodec.Encode(loaded, catalog)))
                throw new InvalidOperationException("Fresh-process full-state comparison failed.");
            if (loaded.UseInventoryItem(0) != expected.UseInventoryItem(0)
                || loaded.Random.State != expected.Random.State
                || !GameSaveCodec.Encode(expected, catalog).SequenceEqual(GameSaveCodec.Encode(loaded, catalog)))
                throw new InvalidOperationException("Fresh-process RNG/item continuation failed.");
        }
        Console.WriteLine(operation + " passed.");
    }

    public static async Task Run(Action<bool, string> check)
    {
        var catalog = Catalog();
        var original = Fixture(catalog);
        var bytes = GameSaveCodec.Encode(original, catalog);
        var restored = GameSaveCodec.Decode(bytes, catalog);
        var versionOne = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "full-save-v1.y3m"));
        check(bytes.SequenceEqual(versionOne), "The checked-in version 1 fixture fixes the original byte layout");
        check(GameSaveCodec.Encode(GameSaveCodec.Decode(versionOne, catalog), catalog).SequenceEqual(versionOne),
            "The original version 1 fixture remains readable and stable");
        var persistedProperties = typeof(Player).GetProperties().Where(p => p.CanWrite || p.PropertyType.IsArray).ToArray();
        check(persistedProperties.Length == 72, "Save coverage explicitly accounts for 65 character scalars and seven arrays");
        foreach (var property in persistedProperties)
        {
            var a = property.GetValue(original.Player);
            var b = property.GetValue(restored.Player);
            check(a is Array array ? b is Array copy && !ReferenceEquals(array, copy)
                    && array.Cast<object>().SequenceEqual(copy.Cast<object>()) : Equals(a, b),
                "Full-state round trip preserves Player." + property.Name);
        }
        check(GameSaveCodec.Encode(restored, catalog).SequenceEqual(bytes), "The versioned full-state snapshot round trips exactly");
        check(restored.DefeatedMonsterPlacements.SequenceEqual(new ushort[] { 0, 5, 185 })
            && restored.TileChanges.SequenceEqual(original.TileChanges), "Persistent map edits and all placement ID boundaries round trip");
        check(restored.Location.Position == new MapPosition(8, 7) && restored.Location.RegionX == -2 && restored.Location.RegionY == -1
            && restored.Location.MapLayer == 1 && restored.Location.MapId == 61 && !restored.Location.AlternateMapSet,
            "Complete world coordinates survive saving");
        var rollExpected = new MsvcRandom(uint.MaxValue);
        var rollsMatch = true;
        for (var i = 0; i < 1024; i++) rollsMatch &= restored.Random.Next() == rollExpected.Next() && restored.Random.State == rollExpected.State;
        check(rollsMatch, "A loaded RNG continues all 1024 draws and states from its exact saved state");
        restored.Player.Inventory[0] = 9; restored.Player.Gold = 0; restored.DefeatedMonsterPlacements.Clear();
        check(original.Player.Inventory[0] == 1 && original.Player.Gold != 0 && original.DefeatedMonsterPlacements.Count == 3,
            "Loaded character arrays and progression are independent of the source session");

        foreach (var type in Enum.GetValues<CharacterType>())
        {
            var session = new GameSession(catalog, new Player(type));
            check(GameSaveCodec.Encode(GameSaveCodec.Decode(GameSaveCodec.Encode(session, catalog), catalog), catalog)
                .SequenceEqual(GameSaveCodec.Encode(session, catalog)), $"A fresh {type} saves and reopens without default substitution");
        }
        var hell = new GameSession(catalog, location: new() { MapId = 117, RegionX = -4, RegionY = -1,
            AlternateMapSet = true, Position = new(6, 10) });
        var hellCopy = GameSaveCodec.Decode(GameSaveCodec.Encode(hell, catalog), catalog);
        check(hellCopy.Location.AlternateMapSet && hellCopy.Player.AlternateMapSet && hellCopy.CurrentMap.Id == 117,
            "Alternate map-set state survives a full save");

        var entry = new GameSession(catalog);
        entry.Player.HealingWaterAvailable = false;
        entry.CurrentMap.RestoreTile(new(5, 5), 0x53);
        var reopened = GameSaveCodec.Decode(GameSaveCodec.Encode(entry, catalog), catalog);
        check(reopened.Player.HealingWaterAvailable && reopened.CurrentMap.Tile(new(5, 5)) == 2,
            "Reopening reapplies original entry rules and discards unsaved map-instance edits");
        entry.Player.PendingRelocation = (ushort)PendingRelocation.Hell;
        reopened = GameSaveCodec.Decode(GameSaveCodec.Encode(entry, catalog), catalog);
        check(entry.Player.PendingRelocation == 3 && entry.Location.MapId == 17 && reopened.Location.MapId == 117
            && reopened.Player.PendingRelocation == 0 && reopened.Player.HellGatekeeperState == HellGatekeeperState.Waiting,
            "Reopening resolves queued relocation on a new session without mutating the saved source");

        var platformMap = catalog.Ids.Select(id => catalog.CreateMap(id)!).First(m => m.Events.Overlay.Value > 0);
        var platformLocation = LocationFor(platformMap.Id, new(5, 5));
        var rider = new GameSession(catalog, location: platformLocation, tileChanges: [new(platformMap.Id, new(5, 5), 0x0c)]);
        reopened = GameSaveCodec.Decode(GameSaveCodec.Encode(rider, catalog), catalog);
        check(reopened.CurrentMap.Events.Overlay.Position == reopened.Location.Position,
            "Full-save reopen retains D7 platform support without serializing platform phase");

        var simple = GameSaveCodec.Encode(new(catalog), catalog);
        var allTruncationsRejected = true;
        for (var length = 0; length < simple.Length; length++)
            allTruncationsRejected &= Rejects(simple[..length]);
        check(allTruncationsRejected, "Every truncated prefix of a complete save is rejected");
        check(Rejects(new byte[GameSaveCodec.MaximumFileSize + 1]), "Oversized save bytes are rejected");
        check(Rejects("{\"Version\":1,\"MapId\":17,\"X\":5,\"Y\":5}"u8.ToArray())
            && Rejects("Y3RS legacy save"u8.ToArray()), "Preview and C++ formats cannot be mistaken for a full MonoGame save");
        var damaged = (byte[])simple.Clone(); damaged[^1] ^= 1;
        check(Rejects(damaged), "A payload bit flip fails the integrity check");
        damaged = (byte[])simple.Clone(); damaged[14] ^= 1;
        check(Rejects(damaged), "A damaged checksum fails the integrity check");
        damaged = (byte[])simple.Clone(); damaged[8] = 2;
        check(Rejects(damaged), "A future save version is rejected explicitly");
        damaged = [.. simple, 0];
        check(Rejects(damaged), "Trailing file data is rejected");

        // The documented fixed tail for an empty map/defeat table is 37 bytes.
        var locationOffset = simple.Length - 37;
        foreach (var (offset, value, label) in new[]
        {
            (locationOffset, -1, "negative position"), (locationOffset + 4, 12, "off-grid position"),
            (locationOffset + 8, int.MaxValue, "invalid region"), (locationOffset + 16, 3, "invalid layer"),
            (locationOffset + 21, 18, "inconsistent map ID"),
            (locationOffset + 29, int.MaxValue, "unbounded map-change count"),
            (locationOffset + 33, -1, "negative defeated-placement count"),
        })
        {
            damaged = (byte[])simple.Clone(); BinaryPrimitives.WriteInt32LittleEndian(damaged.AsSpan(offset), value); Rehash(damaged);
            check(Rejects(damaged), "A checksummed payload rejects " + label);
        }
        damaged = (byte[])simple.Clone(); damaged[locationOffset + 20] = 2; Rehash(damaged);
        check(Rejects(damaged), "Noncanonical boolean bytes are rejected even with a valid checksum");
        damaged = (byte[])simple.Clone(); damaged[48] = 0xc0; Rehash(damaged);
        check(Rejects(damaged), "Invalid UTF-8 names are rejected even with a valid checksum");
        damaged = [.. simple, 0]; Rehash(damaged);
        check(Rejects(damaged), "Trailing payload data is rejected even with a valid length and checksum");
        damaged = [.. simple, 5, 0, 5, 0]; BinaryPrimitives.WriteInt32LittleEndian(damaged.AsSpan(locationOffset + 33), 2); Rehash(damaged);
        check(Rejects(damaged), "Duplicate defeated placements in a checksummed payload are rejected");
        damaged = [.. simple, 186, 0]; BinaryPrimitives.WriteInt32LittleEndian(damaged.AsSpan(locationOffset + 33), 1); Rehash(damaged);
        check(Rejects(damaged), "Unknown defeated placements are rejected");
        foreach (var (changes, label) in new (MapTileChange[], string)[]
        {
            ([new(17, new(5, 5), 8), new(17, new(5, 5), 2)], "duplicate map coordinates"),
            ([new(999, new(5, 5), 2)], "unavailable map edits"),
            ([new(17, new(13, 5), 2)], "off-grid map edits"),
        })
        {
            using var payload = new MemoryStream();
            payload.Write(simple.AsSpan(0, locationOffset + 29));
            using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(changes.Length);
                foreach (var change in changes)
                {
                    writer.Write(change.MapId); writer.Write(change.Position.X); writer.Write(change.Position.Y); writer.Write(change.Tile);
                }
                writer.Write(0);
            }
            var invalidChanges = payload.ToArray(); Rehash(invalidChanges);
            check(Rejects(invalidChanges), "A checksummed payload rejects " + label);
        }

        foreach (var (mutate, label) in new (Action<GameSession>, string)[]
        {
            (s => s.Player.OpenedChests[0] = 16, "invalid chest flags"),
            (s => s.Player.Specialty = 4, "invalid specialty"),
            (s => s.Player.SpecialAbility = (SpecialAbility)5, "invalid ability"),
            (s => s.Player.HellGatekeeperState = (HellGatekeeperState)2, "invalid gatekeeper state"),
            (s => s.Player.MiscellaneousShopStage = 5, "invalid shop stage"),
            (s => s.Player.PendingRelocation = 5, "unknown relocation"),
            (s => s.Player.PendingRelocation = 1, "missing relocation return"),
            (s => s.Player.SurfaceReturnMapId = 118, "unsupported return map"),
            (s => s.Player.Intelligence = 0, "unsafe stat divisor"),
            (s => s.Player.CurrentMapId = 18, "character/world mismatch"),
            (s => s.DefeatedMonsterPlacements.Add(186), "unknown placement ID"),
            (s => s.DefeatedMonsterPlacements.AddRange([5, 5]), "duplicate placement IDs"),
            (s => s.Player.Name = new string('x', 65536), "oversized name"),
            (s => s.Player.Name = "\ud800", "invalid name encoding"),
        })
        {
            var invalid = new GameSession(catalog); mutate(invalid);
            ExpectInvalid(() => GameSaveCodec.Encode(invalid, catalog), "Saving rejects " + label);
        }
        var generated = new GameSession(catalog);
        generated.Location.MapId = 118; generated.Location.RegionX = -3; generated.Location.RegionY = -1;
        generated.Location.AlternateMapSet = true; generated.Player.CurrentMapId = 118; generated.Player.AlternateMapSet = true;
        ExpectInvalid(() => GameSaveCodec.Encode(generated, catalog), "A mismatched generated location cannot be saved from a town session");

        var directory = Path.Combine(Path.GetTempPath(), "yipe-full-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "game.y3m");
            var store = new GameSaveStore(path, catalog);
            var live = Fixture(catalog);
            var snapshot = GameSaveCodec.Encode(live, catalog);
            var pending = store.SaveAsync(live);
            live.Player.Gold = 0; live.Player.Inventory[0] = 9; live.Random.Next(); live.DefeatedMonsterPlacements.Clear();
            await pending;
            check((await File.ReadAllBytesAsync(path)).SequenceEqual(snapshot), "Async saving captures all state before returning control to gameplay");
            var loaded = await store.LoadAsync();
            check(GameSaveCodec.Encode(loaded, catalog).SequenceEqual(snapshot), "The desktop store reloads the full isolated snapshot");
            await store.SaveAsync(live);
            check((await File.ReadAllBytesAsync(path)).SequenceEqual(GameSaveCodec.Encode(live, catalog)), "Atomic replacement saves the newest full state");
            var good = await File.ReadAllBytesAsync(path);
            live.Player.OpenedChests[0] = 16;
            await ExpectFailure(() => store.SaveAsync(live), "Invalid live state fails through the async save result");
            check((await File.ReadAllBytesAsync(path)).SequenceEqual(good), "Rejected state leaves the previous successful file intact");
            live.Player.OpenedChests[0] = 0;
            if (!OperatingSystem.IsWindows())
            {
                var permissions = File.GetUnixFileMode(directory);
                try
                {
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                    await ExpectFailure(() => store.SaveAsync(live), "A real write-permission failure is reported");
                    check((await File.ReadAllBytesAsync(path)).SequenceEqual(good), "Write failure leaves the last successful file intact");
                }
                finally { File.SetUnixFileMode(directory, permissions); }
            }
            var destinationDirectory = Path.Combine(directory, "directory.y3m");
            Directory.CreateDirectory(destinationDirectory);
            await ExpectFailure(() => new GameSaveStore(destinationDirectory, catalog).SaveAsync(live), "Failure at replacement is reported after writing the temporary file");
            check(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "Successful and failed replacements leave no temporary files");
            var parallel = Enumerable.Range(0, 8).Select(i =>
            {
                var session = new GameSession(catalog); session.Player.Gold = i;
                return store.SaveAsync(session);
            }).ToArray();
            await Task.WhenAll(parallel);
            check((await store.LoadAsync()).Player.Gold is >= 0 and < 8 && !Directory.EnumerateFiles(directory, "*.tmp").Any(),
                "Concurrent writers use distinct temporary files and leave one complete save");
            await File.WriteAllBytesAsync(path, damaged);
            await ExpectFailure(async () => { await store.LoadAsync(); }, "Disk load reports a structurally invalid payload");
            using (var file = File.Create(path)) file.SetLength(GameSaveCodec.MaximumFileSize + 1);
            await ExpectFailure(async () => { await store.LoadAsync(); }, "Disk load bounds file size before allocating");
            File.Delete(path);
            await ExpectFailure(async () => { await store.LoadAsync(); }, "A missing full save is reported");
            foreach (var extension in new[] { ".json", ".y3r", ".yp3" })
            {
                try { _ = new GameSaveStore(Path.Combine(directory, "legacy" + extension), catalog); check(false, "Reject legacy save destination"); }
                catch (ArgumentException) { check(true, "Full-game storage refuses a " + extension + " destination"); }
            }
            await RunChild("--write-save-fixture", path);
            await RunChild("--read-save-fixture", path);
            check(true, "Separate write/read processes preserve full state and subsequent item/RNG behavior");
        }
        finally { Directory.Delete(directory, recursive: true); }

        bool Rejects(byte[] data)
        {
            try { GameSaveCodec.Decode(data, catalog); return false; }
            catch (InvalidDataException) { return true; }
        }
        void ExpectInvalid(Action action, string label)
        {
            try { action(); check(false, label); }
            catch (InvalidDataException) { check(true, label); }
        }
        async Task ExpectFailure(Func<Task> action, string label)
        {
            try { await action(); check(false, label); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { check(true, label); }
        }
    }

    private static WorldLocation LocationFor(int id, MapPosition position)
    {
        foreach (var alternate in new[] { false, true }) for (var layer = 0; layer <= 2; layer++)
            for (var x = -4; x <= 1; x++) for (var y = -3; y <= 2; y++)
                if (x - y * 6 + 17 + layer * 40 + (alternate ? 98 : 0) == id)
                    return new() { Position = position, MapId = id, RegionX = x, RegionY = y, MapLayer = layer, AlternateMapSet = alternate };
        throw new InvalidOperationException("No coordinates for static map.");
    }

    private static void Rehash(byte[] bytes)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), bytes.Length - 46);
        SHA256.HashData(bytes.AsSpan(46)).CopyTo(bytes, 14);
    }

    internal static async Task RunChild(string operation, string path)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(info.FileName) == "dotnet") info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        info.ArgumentList.Add(operation); info.ArgumentList.Add(path);
        using var child = Process.Start(info) ?? throw new InvalidOperationException("Could not start restart check.");
        var output = child.StandardOutput.ReadToEndAsync(); var errors = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await child.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { child.Kill(entireProcessTree: true); throw new TimeoutException("Save restart check timed out."); }
        if (child.ExitCode != 0) throw new InvalidOperationException(await output + await errors);
        Console.Write(await output);
    }
}
