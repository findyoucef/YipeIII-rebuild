namespace Yipe.Core;

public sealed class RoamingMonster(int typeId, MapPosition position)
{
    public int TypeId { get; } = typeId;
    public MapPosition Position { get; set; } = position;
}
public sealed class PursuingMonster(int typeId, MapPosition position, MapPosition origin)
{
    public int TypeId { get; } = typeId;
    public MapPosition Position { get; set; } = position;
    public MapPosition Origin { get; } = origin;
}
public enum RoamingMonsterMove { Stayed, Moved, EncounteredPlayer }
public enum RoamingMonsterUpdate { None, Spawned, Moved, EncounteredPlayer }

public static class Encounters
{
    private static readonly ushort[] AllowedTiles = [
        0x2d, 0x2f, 0x08, 0x29, 0x2a, 0x33, 0x02, 0x28, 0x37, 0x32, 0x34,
        0x35, 0x38, 0x53, 0x3c, 0x2c, 0x0b, 0x16, 0x17, 0x18, 0x19, 0x1a,
        0x26, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x20, 0x27, 0x46, 0x47, 0x48,
        0x49, 0x5d, 0x5e, 0x5f, 0x52, 0x65, 0x66, 0x67, 0x68, 0x69,
    ];
    private static readonly int[] SurfaceAllowed = [
        0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0,
        0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];
    private static readonly int[] RequiredEnvironment = [
        0, 1, 1, 1, -1, 1, -1, 1, 2, 0, 2, 2, 2, 3, 3, 0, 3, -1, 3, 4, 3,
        4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, -1, -1, -1, -1, -1, -1, -1,
    ];
    private static int Roll(MsvcRandom random, int bound) => random.Next() % Math.Max(bound, 1);

    private static bool MapEventAt(MapEvents events, MapPosition position)
    {
        if (events.Chests.Contains(position)) return true;
        if (events.Message.HasPosition && events.Message.Position == position
            || events.Overlay.HasPosition && events.Overlay.Position == position
            || events.Princess.HasPosition && events.Princess.Position == position) return true;
        foreach (var entry in events.Depth) if (entry.HasPosition && entry.Position == position) return true;
        foreach (var entry in events.Linked) if (entry.HasPosition && entry.Position == position) return true;
        return false;
    }
    private static bool TileAllowed(ushort tile, int type)
    {
        if (AllowedTiles.Contains(tile)) return true;
        if (type is >= 0x19 and <= 0x1d && (tile is >= 0x0d and <= 0x15 or >= 0x4a and <= 0x51)) return true;
        return type == 6 && (tile is 0x21 or 0x2e or 0x31 or >= 0x0e and <= 0x15);
    }
    private static bool PositionAvailable(Map map, int type, MapPosition position,
        IReadOnlyList<MonsterPlacement> placements, MapPosition? blocked)
    {
        if (!Map.Contains(position) || position.Y >= Map.Height - 1 || !TileAllowed(map.Tile(position), type)
            || MapEventAt(map.Events, position) || blocked == position) return false;
        foreach (var placement in placements) if (placement.Contains(position)) return false;
        return true;
    }
    public static bool Allowed(int environment, int type, bool alternateMapSet)
    {
        if (type < 1 || type >= SurfaceAllowed.Length) return false;
        var allowed = environment == 0 ? SurfaceAllowed[type] != 0
            : RequiredEnvironment[type] == 0 || RequiredEnvironment[type] == environment;
        if (environment == 7 && RequiredEnvironment[type] >= 0) allowed = true;
        if (alternateMapSet && (type < 3 || type is 0x11 or 0x13)) allowed = false;
        return allowed;
    }
    private static int Candidate(Map map, Player p, int defaultEnvironmentMonsterLimit, MsvcRandom random)
    {
        var type = map.Environment switch
        {
            0 => Roll(random, p.Level + 2) + 1,
            1 => Roll(random, 7) + 1,
            2 => Roll(random, map.Id < 0x51 && !p.EncounteredMonsterTypes[36] ? 9 : 13) + 2,
            3 => Roll(random, map.Id > 0x50 || p.EncounteredMonsterTypes[37] ? 12 : 8) + 9,
            4 => Roll(random, map.Id > 0x50 || p.EncounteredMonsterTypes[38] ? 16 : 14) + 9,
            5 => Roll(random, map.Id > 0x50 || p.EncounteredMonsterTypes[39] ? 16 : 13) + 0x0f,
            6 => Roll(random, map.Id < 0x51 && !p.EncounteredMonsterTypes[40] ? 3 : 5) + 0x1e,
            _ => Roll(random, defaultEnvironmentMonsterLimit >= 33 ? 33 : defaultEnvironmentMonsterLimit >= 24 ? 24 : 20) + 1,
        };
        if (p.Level < 4) type = Roll(random, p.Level + 1) + 1;
        if (type > 40) type = 0x23 - Roll(random, 0x0b);
        return type;
    }
    private static MapPosition StepToward(MapPosition from, MapPosition target) =>
        new(from.X + Math.Sign(target.X - from.X), from.Y + Math.Sign(target.Y - from.Y));
    private static MapPosition RandomAdjacentStep(MapPosition from, MsvcRandom random)
    {
        var delta = Roll(random, 8) switch
        {
            0 => new MapPosition(-1, -1),
            1 => new(1, 0),
            2 => new(-1, 0),
            3 => new(0, 1),
            4 => new(0, -1),
            5 => new(1, -1),
            6 => new(-1, 1),
            _ => new(1, 1),
        };
        var x = Math.Max(from.X + delta.X, 0); var y = Math.Max(from.Y + delta.Y, 0);
        return new(x > 12 ? 11 : x, Math.Min(y, 11));
    }
    public static bool ShouldSpawn(Player player, WorldLocation location, ushort destinationTile, MsvcRandom random)
    {
        int threshold = player.RandomEncounterThreshold;
        if (destinationTile is 8 or >= 0x16 and <= 0x1a) threshold += 11;
        else if (destinationTile is 0x2f or 0x32) threshold -= 10;
        if (location.MapId > 0x28) threshold -= 5;
        if (location.MapId > 0x50) threshold -= 5;
        if (player.Health > 0 && player.MaximumHealth / player.Health > 7) threshold += 7 - player.Difficulty;
        return threshold < Roll(random, 100);
    }
    public static RoamingMonster? Spawn(Map map, Player player, MapPosition playerPosition,
        IReadOnlyList<MonsterPlacement> placements, MapVisibility visibility, int defaultEnvironmentMonsterLimit,
        MsvcRandom random, MapPosition? pursuer = null)
    {
        if (map.Id is 0x11 or 8 or 0x1b or 0x75) return null;
        var type = 0;
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var candidate = Candidate(map, player, defaultEnvironmentMonsterLimit, random);
            if (Allowed(map.Environment, candidate, player.AlternateMapSet)) { type = candidate; break; }
        }
        if (type == 0) return null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = new MapPosition(Roll(random, 12), Roll(random, 9));
            if (candidate != playerPosition && visibility.Contains(candidate) && PositionAvailable(map, type, candidate, placements, pursuer))
                return new(type, candidate);
        }
        return null;
    }
    public static RoamingMonsterMove Move(RoamingMonster monster, Map map, MapPosition playerPosition,
        IReadOnlyList<MonsterPlacement> placements, MsvcRandom random, MapPosition? pursuer = null)
    {
        var candidate = StepToward(monster.Position, playerPosition);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (Roll(random, 100) < 9) { candidate = RandomAdjacentStep(monster.Position, random); continue; }
            if (candidate == playerPosition) return RoamingMonsterMove.EncounteredPlayer;
            if (PositionAvailable(map, monster.TypeId, candidate, placements, pursuer))
            {
                monster.Position = candidate;
                return RoamingMonsterMove.Moved;
            }
            candidate = RandomAdjacentStep(monster.Position, random);
        }
        return RoamingMonsterMove.Stayed;
    }
    public static RoamingMonsterUpdate Update(ref RoamingMonster? monster, Map map, Player player, WorldLocation location,
        ushort destinationTile, IReadOnlyList<MonsterPlacement> placements, MapVisibility visibility,
        int defaultEnvironmentMonsterLimit, MsvcRandom random, MapPosition? pursuer = null)
    {
        // The spawn roll is consumed even while a roaming monster already exists.
        var spawnRequested = ShouldSpawn(player, location, destinationTile, random);
        if (monster is null)
        {
            if (!spawnRequested) return RoamingMonsterUpdate.None;
            monster = Spawn(map, player, location.Position, placements, visibility, defaultEnvironmentMonsterLimit, random, pursuer);
            return monster is null ? RoamingMonsterUpdate.None : RoamingMonsterUpdate.Spawned;
        }
        return Move(monster, map, location.Position, placements, random, pursuer) switch
        {
            RoamingMonsterMove.Stayed => RoamingMonsterUpdate.None,
            RoamingMonsterMove.Moved => RoamingMonsterUpdate.Moved,
            _ => RoamingMonsterUpdate.EncounteredPlayer,
        };
    }
    public static PursuingMonster? PursuerForMap(Map map, int level)
    {
        var entry = map.Events.Pursuer;
        return entry.HasPosition ? new(entry.TypeForLevel(level), entry.Position, entry.Position) : null;
    }
    public static RoamingMonsterMove MovePursuer(PursuingMonster monster, Map map, MapPosition playerPosition,
        IReadOnlyList<MonsterPlacement> placements, RoamingMonster? roaming, MapVisibility visibility, MsvcRandom random)
    {
        if (!visibility.Contains(monster.Position)) return RoamingMonsterMove.Stayed;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = Roll(random, 100) < 55 ? RandomAdjacentStep(monster.Position, random) : StepToward(monster.Position, playerPosition);
            if (candidate == playerPosition) return RoamingMonsterMove.EncounteredPlayer;
            if (Math.Abs(candidate.X - monster.Origin.X) > 2 || Math.Abs(candidate.Y - monster.Origin.Y) > 2) continue;
            if (PositionAvailable(map, monster.TypeId, candidate, placements, roaming?.Position))
            {
                monster.Position = candidate;
                return RoamingMonsterMove.Moved;
            }
        }
        return RoamingMonsterMove.Stayed;
    }
}
