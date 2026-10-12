using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Atomic desktop storage for .y3m saves. Existing preview and C++ files are separate.</summary>
public sealed class GameSaveStore
{
    private readonly string path;
    private readonly MapCatalog catalog;
    public string FilePath => path;
    public MapJournal RestoredJournal { get; private set; } = new();
    public string JournalWarning { get; private set; } = "";

    public GameSaveStore(string path, MapCatalog catalog)
    {
        if (!Path.GetExtension(path).Equals(".y3m", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Full game saves require a separate .y3m file.", nameof(path));
        this.path = Path.GetFullPath(path);
        this.catalog = catalog;
    }

    public async Task SaveAsync(GameSession session, MapJournal? journal = null)
    {
        // Snapshot before starting asynchronous I/O; later gameplay cannot alter these bytes.
        var bytes = GameSaveCodec.Encode(session, catalog);
        var notes = journal?.Encode(bytes);
        JournalWarning = "";
        await WriteAsync(bytes).ConfigureAwait(false);
        if (notes is null) return;
        try { await WriteAsync(notes, destination: Path.ChangeExtension(path, ".map.json")).ConfigureAwait(false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { JournalWarning = "Game saved, but map notes could not be saved."; Console.Error.WriteLine(error.Message); }
    }

    private async Task WriteAsync(byte[] bytes, bool overwrite = true, string? destination = null)
    {
        destination ??= path;
        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
            {
                await file.WriteAsync(bytes).ConfigureAwait(false);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // Imports create a new destination only. Both source and any existing destination survive failures.
    public async Task ImportAsync(string sourcePath)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous);
        if (source.Length > GameSaveCodec.MaximumFileSize) throw new InvalidDataException("The import is too large.");
        var bytes = new byte[(int)source.Length];
        await source.ReadExactlyAsync(bytes).ConfigureAwait(false);
        if (source.ReadByte() != -1) throw new InvalidDataException("The import changed while reading.");
        var converted = SaveImporter.Convert(bytes, Path.GetExtension(sourcePath), catalog);
        await WriteAsync(converted, overwrite: false).ConfigureAwait(false);
    }

    public async Task<GameSession> LoadAsync()
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous);
        if (file.Length > GameSaveCodec.MaximumFileSize) throw new InvalidDataException("The game save is too large.");
        var bytes = new byte[(int)file.Length];
        await file.ReadExactlyAsync(bytes).ConfigureAwait(false);
        if (file.ReadByte() != -1) throw new InvalidDataException("The game save changed while reading.");
        var session = GameSaveCodec.Decode(bytes, catalog);
        var restored = new MapJournal();
        JournalWarning = "";
        var journalPath = Path.ChangeExtension(path, ".map.json");
        try
        {
            if (File.Exists(journalPath))
            {
                await using var notes = File.OpenRead(journalPath);
                if (notes.Length > 512 * 1024) throw new InvalidDataException("The map journal is too large.");
                var data = new byte[(int)notes.Length];
                await notes.ReadExactlyAsync(data).ConfigureAwait(false);
                if (notes.ReadByte() != -1) throw new InvalidDataException("The map journal changed while reading.");
                restored = MapJournal.Decode(data, bytes, catalog);
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        { JournalWarning = "Game loaded; previous map notes are unavailable."; Console.Error.WriteLine(error.Message); }
        RestoredJournal = restored;
        return session;
    }
}
