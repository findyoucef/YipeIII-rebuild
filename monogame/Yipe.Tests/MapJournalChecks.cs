using System.Text;
using Yipe.Core;
using Yipe.Desktop;

internal static class MapJournalChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var catalog = GameSaveChecks.Catalog();
        var session = new GameSession(catalog);
        var game = new Adventure(session);
        var journal = new MapJournal();
        var before = GameSaveCodec.Encode(session, catalog);
        journal.Observe(game); journal.Observe(game);
        check(journal.Regions.Count == 1 && journal.Find(17)!.Seen.All(s => s) && journal.Links.Count == 0,
            "Map journal starts with the visible village and does not chart unvisited regions");
        check(GameSaveCodec.Encode(session, catalog).SequenceEqual(before), "Observing the map changes neither gameplay nor RNG");
        check(journal.Find(17)!.Marks.Count(m => m == MapMark.Shop) > 4
            && journal.Find(17)!.Marks.Contains(MapMark.Entrance) && journal.Find(17)!.Marks.Contains(MapMark.Bridge),
            "Visible village services, cave entrance and bridges receive map markers");
        session.Player.PendingRelocation = (ushort)PendingRelocation.StoneCircle;
        session.ApplyPendingRelocation(); game = new Adventure(session); journal.Observe(game);
        session.Player.PendingRelocation = (ushort)PendingRelocation.Yipe;
        session.ApplyPendingRelocation(); game = new Adventure(session); journal.Observe(game);
        check(journal.Links.SequenceEqual(new[] { new MapLink(17, 21) }), "Actual travel records one undirected connection even after returning");
        check(catalog.Ids.Concat(new[] { 118, 119, 120 }).Select(id => new MappedRegion { Id = id })
            .All(r => r.Layer is >= 0 and <= 3 && r.Cell is >= 0 and < 36)
            && new MappedRegion { Id = 133 }.Coordinate == "E6" && new MappedRegion { Id = 57 }.Coordinate == "E3",
            "All 115 original areas fit the world grid, including alternate and generated maps");

        var cave = new Adventure(new GameSession(catalog, location: new() { MapId = 41, MapLayer = 1,
            RegionX = -4, RegionY = 2, Position = new(1, 2) }));
        journal.Observe(cave);
        var known = journal.Find(41)!;
        check(known.Seen.Any(s => s) && known.Seen.Any(s => !s)
            && Enumerable.Range(0, Map.TileCount).All(i => known.Seen[i] == cave.Visibility.Contains(new(i / Map.Height, i % Map.Height))
                && (known.Seen[i] || known.Tiles[i] == 0 && known.Marks[i] == MapMark.None)),
            "Cave map notes contain exactly the revealed cells and no unexplored contents");
        // Force one extra discovered cell to isolate persistence from room flooding rules.
        var hidden = Array.FindIndex(known.Seen, s => !s);
        known.Seen[hidden] = true; known.Tiles[hidden] = 0x22;
        journal.Observe(new Adventure(cave.Session));
        check(known.Seen[hidden], "Static cave discoveries survive a room visibility reset");
        var generated = new Adventure(new GameSession(catalog, location: new() { MapId = 118, AlternateMapSet = true,
            RegionX = -3, RegionY = -1, Position = new(1, 5) }));
        journal.Observe(generated);
        var generatedRegion = journal.Find(118)!;
        Array.Fill(generatedRegion.Seen, true); Array.Fill(generatedRegion.Tiles, (ushort)0x22);
        var replacement = new Adventure(new GameSession(catalog, location: generated.Session.Location, randomState: 712));
        var generatedState = replacement.Session.Random.State;
        journal.Observe(replacement);
        check(Enumerable.Range(0, Map.TileCount).All(i => generatedRegion.Seen[i] == replacement.Visibility.Contains(new(i / Map.Height, i % Map.Height))
            && (!generatedRegion.Seen[i] || generatedRegion.Tiles[i] == replacement.Map.Tile(new(i / Map.Height, i % Map.Height))))
            && replacement.Session.Random.State == generatedState, "Regenerated caves discard the previous shape without extra random draws");
        var secret = new Adventure(new GameSession(catalog, location: new() { MapId = 1, RegionX = -4, RegionY = 2 }));
        secret.Map.RestoreTile(new(5, 5), 0x56); secret.Map.RestoreTile(new(6, 5), 0x42);
        var secrets = new MapJournal(); secrets.Observe(secret);
        check(secrets.Find(1)!.Marks[5 * Map.Height + 5] == MapMark.None
            && secrets.Find(1)!.Marks[6 * Map.Height + 5] == MapMark.None, "Hidden trees and underworld tunnels receive no revealing map markers");

        var save = GameSaveCodec.Encode(session, catalog);
        var encoded = journal.Encode(save);
        var decoded = MapJournal.Decode(encoded, save, catalog);
        check(decoded.Encode(save).SequenceEqual(encoded), "Map journal JSON round trips with all discoveries and travel links");
        void Reject(byte[] data, byte[]? gameBytes, string label)
        {
            var rejected = false;
            try { MapJournal.Decode(data, gameBytes ?? save, catalog); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, label);
        }
        Reject(encoded, before, "Map notes from different game bytes cannot be attached to a save");
        Reject(Encoding.UTF8.GetBytes("{"), null, "Corrupt map JSON is rejected");
        Reject(new byte[512 * 1024 + 1], null, "Oversized map notes are rejected before parsing");
        decoded.Version = 2;
        Reject(decoded.Encode(save), null, "Newer map-note versions are rejected");
        decoded.Version = 1; decoded.Links.Add(new(17, 999));
        Reject(decoded.Encode(save), null, "Map links cannot refer to an unknown region");
        decoded.Links.RemoveAt(decoded.Links.Count - 1);
        decoded.Regions.Add(decoded.Regions[0]);
        Reject(decoded.Encode(save), null, "Duplicate regions are rejected");

        var directory = Path.Combine(Path.GetTempPath(), "yipe-map-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "game.y3m");
        var notesPath = Path.ChangeExtension(path, ".map.json");
        try
        {
            var store = new GameSaveStore(path, catalog);
            await store.SaveAsync(session, journal);
            var reopened = new GameSaveStore(path, catalog);
            var loaded = await reopened.LoadAsync();
            check(GameSaveCodec.Encode(loaded, catalog).SequenceEqual(save)
                && reopened.RestoredJournal.Encode(save).SequenceEqual(encoded) && reopened.JournalWarning.Length == 0,
                "A new save-store instance restores discoveries beside byte-identical version 1 game data");
            var validNotes = File.ReadAllBytes(notesPath);
            File.WriteAllBytes(path, before);
            await reopened.LoadAsync();
            check(reopened.RestoredJournal.Regions.Count == 0 && reopened.JournalWarning.Length > 0
                && File.ReadAllBytes(notesPath).SequenceEqual(validNotes), "Stale map notes are ignored with a warning while the valid adventure loads");
            File.WriteAllText(notesPath, "{");
            loaded = await reopened.LoadAsync();
            check(reopened.RestoredJournal.Regions.Count == 0 && reopened.JournalWarning.Length > 0
                && GameSaveCodec.Encode(loaded, catalog).SequenceEqual(before), "Corrupt companion notes cannot prevent loading a valid adventure");
            File.Delete(notesPath);
            await reopened.LoadAsync();
            check(reopened.RestoredJournal.Regions.Count == 0 && reopened.JournalWarning.Length == 0,
                "Older saves without a journal load normally and begin fresh discovery");
            Directory.CreateDirectory(notesPath);
            await store.SaveAsync(session, journal);
            check(store.JournalWarning.Length > 0 && File.ReadAllBytes(path).SequenceEqual(save),
                "A failed map-note write still saves the full adventure and reports the missing notes");
            Directory.Delete(notesPath);
            await store.SaveAsync(session, journal);
            File.Delete(path); Directory.CreateDirectory(path);
            var failed = false;
            try { await store.SaveAsync(new GameSession(catalog), new MapJournal()); }
            catch (IOException) { failed = true; }
            check(failed && File.ReadAllBytes(notesPath).SequenceEqual(validNotes)
                && !Directory.EnumerateFiles(directory, "*.tmp").Any(), "A failed game save never replaces its companion or leaves staging files");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
