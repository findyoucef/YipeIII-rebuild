using System.Text.Json;
using System.Text.Json.Serialization;
using Yipe.Core;

namespace Yipe.Desktop;

public sealed record TownSave(int Version, int MapId, int X, int Y);

[JsonSerializable(typeof(TownSave))]
internal partial class SaveJsonContext : JsonSerializerContext;

/// <summary>Desktop-only storage for the town preview. Never reads or overwrites C++ saves.</summary>
public sealed class SaveStore(string path, TownMap map)
{
    public async Task SaveAsync(MapPosition position)
    {
        if (!map.CanResume(position)) throw new InvalidDataException("Cannot save at this town position.");
        var save = new TownSave(1, TownMap.Id, position.X, position.Y);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(save, SaveJsonContext.Default.TownSave);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            // Cleanup must not hide the original write error or a successful replacement.
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public async Task<MapPosition> LoadAsync()
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous);
        if (file.Length > 4096) throw new InvalidDataException("This is not a town preview save.");
        var save = await JsonSerializer.DeserializeAsync(file, SaveJsonContext.Default.TownSave)
            .ConfigureAwait(false);
        if (save is null || save.Version != 1 || save.MapId != TownMap.Id
            || !map.CanResume(new(save.X, save.Y)))
            throw new InvalidDataException("This town preview save is invalid or from another version.");
        return new(save.X, save.Y);
    }
}
