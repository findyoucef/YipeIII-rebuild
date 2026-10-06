using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class ScoreChecks
{
    public static void Run(Action<bool, string> check, string? path = null)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        foreach (var difficulty in new[] { 0, 1, 2, 3, -1, 65538, 65539 })
        {
            var scores = HighScores.Create();
            for (var step = 0; step < 60; step++)
            {
                var p = new Player { Name = "Hero" + step % 7, Difficulty = difficulty, Level = step % 20 + 1,
                    Gold = step % 6 * 100, QuestProgressTotal = step % 13 * 7 };
                for (var i = 0; i < step % 21; i++) p.InventoryValues[i] = 1;
                var changed = HighScores.Update(scores, p, step % 9 == 0);
                writer.Write($"{difficulty},{step},{(changed ? 1 : 0)}");
                foreach (var entry in scores) writer.Write($"|{entry.Name}:{entry.Value}");
                writer.Write('\n');
            }
        }
        foreach (var value in new[] { int.MinValue, -1, 0, 1, 15, 16, 60, 61, 450, 451, int.MaxValue })
            foreach (ushort gate in new ushort[] { 0, 1, 3, 32768, 65535 }) foreach (var difficulty in new[] { 1, 2, 3, 65538 })
            {
                var p = new Player { Level = value, Gold = value, Difficulty = difficulty, GatekeeperState = gate, Value06a4 = (ushort)(gate == 3 ? 5 : 0) };
                Array.Fill(p.MonsterDefeats, value);
                writer.Write($"{value},{gate},{difficulty}|{HighScores.Code(p)}\n");
            }
        foreach (var (id, name) in AudioCatalog.Effects) writer.Write($"audio|{id}|{name}\n");
        for (var type = 1; type <= 41; type++) writer.Write($"monster|{type}|{AudioCatalog.MonsterFile(type)}\n");
        for (var index = 0; index < 12; index++) writer.Write($"spell|{index}|{AudioCatalog.Spell(index)}\n");
        var trace = writer.ToString();
        if (path is not null) File.WriteAllText(path, trace);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace))).ToLowerInvariant();
        check(digest == "b3b130d150f8b7f952f4b6afc162d7c589ba9a5f6b44575218dfab8a882495f9", "730 independent C++ records match high-score ranking/ties/finishers, signed/wrapped codes and sound aliases");
    }
}
