using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Yipe.Desktop;

internal static class FootstepAssetChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Content", "Audio", "Footsteps");
        using var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "sources.json")));
        var credits = sources.RootElement.GetProperty("sounds").EnumerateArray().ToDictionary(
            sound => sound.GetProperty("file").GetString()!);
        var hashes = new List<string>();
        var surfaces = typeof(GameInput).Assembly.GetType("Yipe.Desktop.FootstepSurface")!;
        foreach (var surface in Enum.GetNames(surfaces))
        {
            var files = Enumerable.Range(0, 3).Select(i => $"{surface.ToLowerInvariant()}-{i}.wav").ToArray();
            var variants = files.Select(file => File.ReadAllBytes(Path.Combine(directory, file))).ToArray();
            check(variants.All(wave => wave.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && wave.AsSpan(8, 8).SequenceEqual("WAVEfmt "u8)
                && BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(16, 4)) == 16
                && BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(20, 2)) == 1
                && BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(22, 2)) == 1
                && BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(24, 4)) == 22050
                && BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(34, 2)) == 16
                && wave.AsSpan(36, 4).SequenceEqual("data"u8)
                && BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(40, 4)) == wave.Length - 44
                && wave.Length is > 4000 and <= 10628),
                $"{surface} ships three MonoGame-compatible 16-bit mono recordings shorter than one walking step");
            foreach (var wave in variants)
            {
                var samples = Enumerable.Range(0, (wave.Length - 44) / 2).Select(i =>
                    (double)BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(44 + i * 2, 2)) / short.MaxValue).ToArray();
                check(samples[0] == 0 && samples[^1] == 0 && samples.Min() < -.02 && samples.Max() > .02
                    && samples.All(sample => Math.Abs(sample) <= .751) && Math.Abs(samples.Average()) < .025
                    && Math.Sqrt(samples.Average(sample => sample * sample)) is > .025 and < .2,
                    $"{surface} recording has a clean boundary, audible energy and mixing headroom");
                hashes.Add(Convert.ToHexString(SHA256.HashData(wave)));
            }
            check(files.All(file => credits.TryGetValue(file, out var credit)
                && credit.GetProperty("license").GetString() == "CC0-1.0"
                && credit.GetProperty("author").GetString()!.Length > 0
                && credit.GetProperty("source_page").GetString()!.StartsWith("https://")
                && credit.GetProperty("sha256").GetString() == Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, file)))).ToLowerInvariant())
                && File.Exists(Path.Combine(directory, "CC0-1.0.txt")),
                $"{surface} recordings ship with verified source credits, CC0 license and file hashes");
        }
        check(hashes.Distinct().Count() == Enum.GetNames(surfaces).Length * 3, "All six surfaces use three distinct recorded footsteps");
    }
}
