using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;
using Yipe.Desktop;

internal static class PersistenceChecks
{
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Content", "Persistence");
    private static IEnumerable<string> Imports => Directory.EnumerateFiles(Fixtures)
        .Where(p => Path.GetExtension(p) is ".y3r" or ".yp3").Order();

    public static async Task Restart(string operation, string directory)
    {
        var catalog = GameSaveChecks.Catalog();
        foreach (var source in Imports)
        {
            var store = new GameSaveStore(Path.Combine(directory, Path.GetFileName(source) + ".y3m"), catalog);
            if (operation == "--write-import-fixtures") await store.ImportAsync(source);
            else
            {
                var output = new StringWriter(CultureInfo.InvariantCulture);
                for (var cycle = 0; cycle < 4; cycle++)
                {
                    var game = await store.LoadAsync();
                    Snapshot(output, game);
                    game.Player.Inventory[0] = 1;
                    game.UseInventoryItem(0);
                    Snapshot(output, game);
                    await store.SaveAsync(game);
                }
                if (output.ToString() != File.ReadAllText(source + ".txt").Replace("\r\n", "\n"))
                    throw new InvalidOperationException("Fresh-process C++ comparison failed: " + Path.GetFileName(source));
            }
        }
        Console.WriteLine(operation + " passed (24 imports, four reopen/item cycles each).");
    }

    public static async Task Run(Action<bool, string> check)
    {
        var catalog = GameSaveChecks.Catalog();
        var trace = new StringWriter(CultureInfo.InvariantCulture);
        uint[] seeds = [0, 1, 2, 0x7fffffff, 0x80000000, 0xffffffff, 0xdeadbeef, 2566201570];
        foreach (var seed in seeds) for (var id = 118; id <= 120; id++)
            for (var x = 1; x <= 11; x++) for (var y = 1; y <= 10; y++)
            {
                var random = new MsvcRandom(seed);
                var map = catalog.LoadMap(id, new(x, y), random)!;
                trace.Write(FormattableString.Invariant($"{seed},{id},{x},{y},{random.State}|"));
                MapState(trace, map);
            }
        check(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString()))).ToLowerInvariant()
            == "6e22522bc805d7dad55f4cefea0c99cbb50120c0a44e8a908da1581ce7995318", "All 2640 generated maps, chest tables and post-generation RNG states match C++");
        foreach (var bad in new MapPosition[] { new(0, 1), new(12, 1), new(1, 0), new(1, 11), new(-1, 5), new(int.MaxValue, 5) })
        {
            var random = new MsvcRandom(99);
            try { catalog.LoadMap(118, bad, random); check(false, "Invalid generator entry rejected"); }
            catch (InvalidDataException) { check(random.State == 99, "Invalid generator entry does not draw RNG"); }
        }
        var staticRandom = new MsvcRandom(99);
        check(catalog.LoadMap(17, new(5, 5), staticRandom) is not null && catalog.LoadMap(999, new(5, 5), staticRandom) is null
            && staticRandom.State == 99, "Static and unavailable map loads preserve RNG");

        foreach (var path in Imports)
        {
            var bytes = SaveImporter.Convert(File.ReadAllBytes(path), Path.GetExtension(path), catalog);
            var output = new StringWriter(CultureInfo.InvariantCulture);
            for (var cycle = 0; cycle < 4; cycle++)
            {
                var game = GameSaveCodec.Decode(bytes, catalog);
                Snapshot(output, game);
                game.Player.Inventory[0] = 1; game.UseInventoryItem(0);
                Snapshot(output, game);
                bytes = GameSaveCodec.Encode(game, catalog);
            }
            check(output.ToString() == File.ReadAllText(path + ".txt").Replace("\r\n", "\n"),
                Path.GetFileName(path) + ": full C++ state/map/RNG comparison through four reopen/item cycles");
        }
        var preview = "{\"Version\":1,\"MapId\":17,\"X\":6,\"Y\":5}"u8.ToArray();
        var migrated = GameSaveCodec.Decode(SaveImporter.Convert(preview, ".JSON", catalog), catalog);
        check(migrated.Location.Position == new MapPosition(6, 5) && migrated.Player.Gold == 0
            && migrated.Player.Specialty == 3 && migrated.Random.State == 1 && migrated.Player.Inventory[0] == 1,
            "Explicit preview migration restores only position and creates the documented default Warrior");
        var live = GameSaveChecks.Fixture(catalog);
        var liveBefore = GameSaveCodec.Encode(live, catalog);
        var native = File.ReadAllBytes(Path.Combine(Fixtures, "native-0.y3r"));
        var original = File.ReadAllBytes(Path.Combine(Fixtures, "original-0.yp3"));
        foreach (var (data, extension) in new[] { (preview, ".json"), (native, ".y3r"), (original, ".yp3") })
        {
            check(Enumerable.Range(0, data.Length).All(n => Rejects(data[..n], extension)), "Every truncated prefix rejected: " + extension);
            check(Rejects([.. data, 0], extension), "Trailing bytes rejected: " + extension);
            foreach (var other in new[] { ".json", ".y3r", ".yp3", ".y3m", ".dat" }.Where(e => e != extension))
                check(Rejects(data, other), extension + " cannot masquerade as " + other);
        }
        foreach (var json in new[]
        {
            "{}", "null", "[]", "{\"Version\":1,\"MapId\":17,\"X\":5,\"X\":6,\"Y\":5}",
            "{\"Version\":2,\"MapId\":17,\"X\":5,\"Y\":5}", "{\"Version\":1,\"MapId\":17,\"X\":6,\"Y\":6}",
            "{\"Version\":1,\"MapId\":17,\"X\":5,\"Y\":5,\"Gold\":100}",
        }) check(Rejects(Encoding.UTF8.GetBytes(json), ".json"), "Preview rejects incomplete, duplicate, extended or invalid state");
        foreach (var extension in new[] { ".json", ".y3r", ".yp3" })
            check(Rejects(new byte[GameSaveCodec.MaximumFileSize + 1], extension), "Oversize input rejected: " + extension);
        for (ushort version = 0; version <= 21; version++)
            if (version != 20)
            {
                var bad = (byte[])native.Clone(); BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(4), version);
                check(Rejects(bad, ".y3r"), "Explicit C++ version policy rejects version " + version);
            }
        var corrupt = (byte[])native.Clone(); corrupt[^1] ^= 1;
        check(Rejects(corrupt, ".y3r"), "C++ CRC detects damage");
        // Valid CRCs exercise structural checks independently of checksum failures.
        var nameEnd = 14 + 2 + "Reference-0".Length;
        foreach (var (offset, value, label) in new (int, int, string)[]
        {
            (nameEnd + 4, 0, "zero intelligence"),
            (nameEnd + 154, 99, "invalid position"),
            (nameEnd + 220, int.MaxValue, "unbounded map changes"),
            (nameEnd + 389, int.MaxValue, "unbounded defeated placements"),
            (native.Length - 137, 99, "invalid specialty"),
        })
        {
            var bad = (byte[])native.Clone(); BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(offset), value);
            Recrc(bad); check(Rejects(bad, ".y3r"), "C++ structural validation: " + label);
        }
        foreach (var offset in new[] { native.Length - 133, native.Length - 132, native.Length - 4 })
        {
            var bad = (byte[])native.Clone(); bad[offset] = 255; Recrc(bad);
            check(Rejects(bad, ".y3r"), "C++ structural validation rejects flags/chests/shop progression");
        }
        var badOriginal = (byte[])original.Clone(); badOriginal[1 + "Original-0".Length + 13 * 4] ^= 8;
        check(Rejects(badOriginal, ".yp3"), "Original validation mismatch is rejected");
        check(Rejects([255, 254, 255, .. original], ".yp3"), "Unsupported Unicode CString is rejected");
        var longOriginal = File.ReadAllBytes(Path.Combine(Fixtures, "original-1.yp3"));
        var framed = new byte[longOriginal.Length + 4];
        framed[0] = 255; framed[1] = framed[2] = 255;
        BinaryPrimitives.WriteUInt32LittleEndian(framed.AsSpan(3), 300);
        longOriginal.AsSpan(3).CopyTo(framed.AsSpan(7));
        check(SaveImporter.Convert(framed, ".yp3", catalog).SequenceEqual(SaveImporter.Convert(longOriginal, ".yp3", catalog)),
            "32-bit and 16-bit CString length framing agree");
        var ansi = (byte[])original.Clone(); ansi[1] = 0xe9;
        check(GameSaveCodec.Decode(SaveImporter.Convert(ansi, ".yp3", catalog), catalog).Player.Name.StartsWith('é'),
            "Original Windows-1252 names convert to Unicode consistently");
        foreach (var (offset, value) in new[]
        {
            (original.Length - 12, int.MaxValue),
            (original.Length - 522 + 20 * 10, 5),
            (original.Length - 522 + 26 * 10, 5),
        })
        {
            var bad = (byte[])original.Clone(); BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(offset), value);
            check(Rejects(bad, ".yp3"), "Original invalid gold/shop/relocation is rejected");
        }
        check(GameSaveCodec.Encode(live, catalog).SequenceEqual(liveBefore), "All rejected imports leave the independent live session unchanged");

        var directory = Path.Combine(Path.GetTempPath(), "yipe-p1-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "position.json"); await File.WriteAllBytesAsync(source, preview);
            var destination = Path.Combine(directory, "import.y3m"); var store = new GameSaveStore(destination, catalog);
            await store.ImportAsync(source);
            var saved = await File.ReadAllBytesAsync(destination);
            check((await store.LoadAsync()).Location.Position == new MapPosition(6, 5), "File import creates a usable full save");
            await Fails(() => store.ImportAsync(source), "Import refuses an existing destination");
            check(Enumerable.SequenceEqual(saved, await File.ReadAllBytesAsync(destination)) && Enumerable.SequenceEqual(preview, await File.ReadAllBytesAsync(source)),
                "Existing destination and source survive rejected replacement");
            await File.WriteAllTextAsync(source, "broken");
            await Fails(() => store.ImportAsync(source), "Malformed file import fails without replacement");
            check(Enumerable.SequenceEqual(saved, await File.ReadAllBytesAsync(destination)), "Malformed import preserves destination bytes");
            var absent = new GameSaveStore(Path.Combine(directory, "absent.y3m"), catalog);
            await Fails(() => absent.ImportAsync(source), "Malformed file does not create a destination");
            check(!File.Exists(Path.Combine(directory, "absent.y3m")), "Failed import leaves no output");
            await File.WriteAllBytesAsync(source, preview);
            // Repeated, synchronized contention exposes Unix check-then-rename races.
            // Separate stores and Unicode paths also exercise the native publish boundary.
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var racePath = Path.Combine(directory, $"race-é雪-{attempt}.y3m");
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var imports = Enumerable.Range(0, 16).Select(async _ =>
                {
                    await start.Task;
                    try { await new GameSaveStore(racePath, catalog).ImportAsync(source); return true; }
                    catch (IOException) { return false; }
                }).ToArray();
                start.SetResult();
                var outcomes = await Task.WhenAll(imports);
                check(outcomes.Count(ok => ok) == 1 && Enumerable.SequenceEqual(saved, await File.ReadAllBytesAsync(racePath))
                    && !Directory.EnumerateFiles(directory, "*.tmp").Any(),
                    $"Concurrent import round {attempt + 1}: exactly one intact destination, no temporary files ({outcomes.Count(ok => ok)} winners)");
            }
            var restart = Path.Combine(directory, "restart"); Directory.CreateDirectory(restart);
            await GameSaveChecks.RunChild("--write-import-fixtures", restart);
            await GameSaveChecks.RunChild("--read-import-fixtures", restart);
            check(true, "Fresh processes match C++ for static/generated imports and 96 save/reopen/item cycles");
            var cliDestination = Path.Combine(directory, "cli.y3m");
            check(await Cli(source, cliDestination) == 0 && File.Exists(cliDestination), "Desktop import CLI succeeds without a graphics window");
            var cliBefore = await File.ReadAllBytesAsync(cliDestination);
            check(await Cli(source, cliDestination) == 1 && Enumerable.SequenceEqual(cliBefore, await File.ReadAllBytesAsync(cliDestination)),
                "Desktop CLI reports refused replacement without changing the file");
            await File.WriteAllTextAsync(source, "broken");
            var badCliOutput = Path.Combine(directory, "bad-cli.y3m");
            check(await Cli(source, badCliOutput) == 1 && !File.Exists(badCliOutput),
                "Desktop CLI reports malformed imports cleanly without creating a save");
        }
        finally { Directory.Delete(directory, recursive: true); }

        bool Rejects(byte[] bytes, string extension)
        {
            try { SaveImporter.Convert(bytes, extension, catalog); return false; }
            catch (InvalidDataException) { return true; }
        }
        async Task Fails(Func<Task> action, string label)
        {
            try { await action(); check(false, label); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { check(true, label); }
        }
    }

    private static void Recrc(byte[] bytes)
    {
        uint crc = uint.MaxValue;
        foreach (var value in bytes.AsSpan(14))
        {
            crc ^= value;
            for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10), ~crc);
    }

    private static async Task<int> Cli(string source, string destination)
    {
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(typeof(GameSaveStore).Assembly.Location);
        info.ArgumentList.Add("--import-save"); info.ArgumentList.Add(source); info.ArgumentList.Add(destination);
        using var child = Process.Start(info)!;
        var output = child.StandardOutput.ReadToEndAsync(); var errors = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await child.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { child.Kill(entireProcessTree: true); throw; }
        Console.Write(await output); Console.Write(await errors);
        return child.ExitCode;
    }

    private static void Snapshot(TextWriter writer, GameSession game)
    {
        PlayerChecks.State(writer, game.Player);
        var l = game.Location;
        writer.Write(FormattableString.Invariant($"{l.Position.X},{l.Position.Y},{l.RegionX},{l.RegionY},{l.MapLayer},{(l.AlternateMapSet ? 1 : 0)},{l.MapId},{game.Random.State}\n"));
        writer.Write(game.TileChanges.Count);
        foreach (var c in game.TileChanges) writer.Write(FormattableString.Invariant($",{c.MapId},{c.Position.X},{c.Position.Y},{c.Tile}"));
        writer.Write('\n'); writer.Write(game.DefeatedMonsterPlacements.Count);
        foreach (var d in game.DefeatedMonsterPlacements) writer.Write(FormattableString.Invariant($",{d}"));
        writer.Write('\n'); MapState(writer, game.CurrentMap);
    }

    private static void MapState(TextWriter writer, Map map)
    {
        writer.Write(FormattableString.Invariant($"{map.Id},{map.Environment}"));
        for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) writer.Write(FormattableString.Invariant($",{map.Tile(new(x, y))}"));
        foreach (var p in map.Events.Chests) writer.Write(FormattableString.Invariant($",{p.X},{p.Y}"));
        writer.Write('\n');
    }
}
