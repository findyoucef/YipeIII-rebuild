namespace Yipe.Core;

public readonly record struct TimedHazard(int MapId, MapPosition First, MapPosition Second, ushort Floor, ushort Hazard);
public readonly record struct TimedHazardResult(bool Changed, bool Fell = false, int SoundId = 0, int Damage = 0, bool RemoveRoaming = false);

/// <summary>View-local 76-tick phase survives map changes, and resets on opening a game.</summary>
public sealed class ExplorationTimer
{
    private int tick;
    public int Advance(Player player, Map map, MapPosition position)
    {
        var current = ++tick;
        if (current is 38 or 76) player.SpellPoints = Math.Clamp(player.SpellPoints + 2, 0, player.MaximumSpellPoints);
        else if (current == 57) player.AbilityPoints = Math.Clamp(player.AbilityPoints + 1, 0, player.MaximumAbilityPoints);
        else if (map.Id == 5)
        {
            if (current == 47 && map.Tile(new(7, 8)) == 0x11) map.RestoreTile(new(7, 8), 2);
            else if ((current < 47 || current > 60) && map.Tile(new(7, 8)) == 2 && position != new MapPosition(7, 8))
                map.RestoreTile(new(7, 8), 0x11);
        }
        if (current == 76) tick = 0;
        return current;
    }
}

public static class Exploration
{
    private static readonly TimedHazard[] Hazards = [
        new(0x2a, new(4,7), new(4,9), 0x53, 0x3c), new(0x2f, new(8,4), new(8,5), 0x53, 0x3c),
        new(0x31, new(9,3), new(7,5), 0x29, 0x2a), new(0x36, new(1,4), new(1,5), 0x28, 0x3c),
        new(0x37, new(4,6), new(11,3), 0x28, 0x3c), new(0x43, new(10,9), new(11,7), 0x29, 0x2a),
        new(0x46, new(9,4), new(4,6), 0x27, 0x3c), new(0x49, new(5,6), new(2,3), 0x29, 0x2a),
        new(0x54, new(1,7), new(3,9), 0x28, 0x3c), new(0x5e, new(9,3), new(7,7), 0x53, 0x3c),
        new(0x62, new(8,2), new(3,9), 0x28, 0x3c), new(0x74, new(3,9), new(1,6), 0x27, 0x3c),
        new(0x79, new(8,5), new(3,3), 0x27, 0x24), new(0x85, new(5,8), new(8,5), 0x27, 0x24),
    ];
    public static TimedHazard? HazardForMap(int mapId)
    {
        foreach (var hazard in Hazards) if (hazard.MapId == mapId) return hazard;
        return null;
    }
    public static TimedHazardResult AdvanceHazard(Map map, MapVisibility visibility, Player player,
        MapPosition position, MapPosition? roaming, MsvcRandom random)
    {
        if (HazardForMap(map.Id) is not { } hazard || !visibility.Contains(hazard.First) || !visibility.Contains(hazard.Second)) return default;
        var oldTile = map.Tile(hazard.First);
        ushort next;
        var remove = false; var fell = false; var sound = 0; var damage = 0;
        switch (oldTile)
        {
            case 0x24: case 0x2a: case 0x3c:
                next = oldTile == 0x24 ? hazard.Floor : (ushort)(hazard.Hazard + 1);
                remove = true;
                if (Affected(position) && oldTile != 0x24)
                {
                    fell = oldTile == 0x2a; sound = fell ? 135 : 137;
                    damage = fell ? Math.Max(3, player.MaximumHealth / 15) : Math.Max(5, player.MaximumHealth / 9);
                    if (!player.AdjustHealth(-damage)) player.Health = 1;
                }
                break;
            case 0x27: case 0x28: case 0x29: case 0x53:
                next = hazard.Hazard; remove = next == 0x24;
                if (next == 0x24 && Affected(position)) { sound = 144; damage = WorldActions.ApplyFireWall(player, random); }
                break;
            case 0x2b: case 0x3d: next = hazard.Floor; break;
            default: return default;
        }
        map.RestoreTile(hazard.First, next); map.RestoreTile(hazard.Second, next);
        return new(true, fell, sound, damage, remove && roaming is { } cell && Affected(cell));
        bool Affected(MapPosition cell) => cell == hazard.First || cell == hazard.Second;
    }
    public static bool AdvanceRepellent(Player player)
    {
        if (unchecked((short)player.MonsterRepellentSteps) <= 0) return false;
        if (--player.MonsterRepellentSteps != 0) return false;
        player.RandomEncounterThreshold = player.SavedRandomEncounterThreshold;
        if (unchecked((short)player.SavedRandomEncounterThreshold) > 100)
            player.RandomEncounterThreshold = unchecked((ushort)(85 - player.Difficulty));
        return true;
    }
}
