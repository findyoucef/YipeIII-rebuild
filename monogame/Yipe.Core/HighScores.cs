using System.Globalization;

namespace Yipe.Core;

public sealed record HighScoreEntry(string Name = "----", int Value = 0);

/// <summary>Three difficulty groups: four top-three lists and the first three finishers.</summary>
public static class HighScores
{
    public static HighScoreEntry[] Create() => Enumerable.Range(0, 45).Select(_ => new HighScoreEntry()).ToArray();
    public static int GroupOffset(int difficulty) => unchecked((short)difficulty) switch { 2 => 15, 3 => 30, _ => 0 };
    public static bool Update(HighScoreEntry[] scores, Player player, bool completed = false)
    {
        if (scores.Length != 45) throw new ArgumentException("High scores require 45 entries.", nameof(scores));
        var previous = (HighScoreEntry[])scores.Clone();
        var group = GroupOffset(player.Difficulty);
        if (completed && !scores.Skip(group + 12).Take(3).Any(entry => entry.Name == player.Name))
            for (var i = group + 12; i < group + 15; i++)
                if (scores[i].Name == "----") { scores[i] = scores[i] with { Name = player.Name }; break; }
        int[] values = [player.QuestProgressTotal, player.RescuedPrincessCount, player.Gold, player.Level];
        for (var category = 0; category < values.Length; category++)
        {
            var first = group + category * 3;
            var rank = 2;
            for (var i = 0; i < 3; i++) if (scores[first + i].Name == player.Name) rank = i;
            var value = values[category];
            if (value < scores[first + rank].Value) continue;
            while (rank > 0 && scores[first + rank - 1].Value <= value)
            {
                scores[first + rank] = scores[first + rank - 1]; rank--;
            }
            scores[first + rank] = new(player.Name, value);
        }
        return !scores.SequenceEqual(previous);
    }

    public static string Code(Player player)
    {
        var defeats = 0;
        foreach (var count in player.MonsterDefeats) defeats = unchecked(defeats + count);
        int[] values = [player.Level, player.Gold, defeats];
        int[] divisors = [15, 450, 60];
        var quotientSum = 0;
        var code = "";
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i] > 0 ? values[i] - 1 : 0;
            var quotient = value / divisors[i];
            var remainder = value % divisors[i];
            quotientSum += quotient;
            code += string.Create(CultureInfo.InvariantCulture, $"{quotient}L{(i == 1 ? 1000 - remainder : remainder)}W");
        }
        code += string.Create(CultureInfo.InvariantCulture, $"{unchecked((short)player.GatekeeperState) * 3}W{quotientSum}W{unchecked((short)player.Difficulty)}");
        return code + (player.Value06a4 == 5 ? "W" : "");
    }
}
