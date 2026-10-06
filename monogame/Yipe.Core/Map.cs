namespace Yipe.Core;

public readonly record struct MapPosition(int X, int Y);
public readonly record struct MovementResult(int OriginalCode, MapPosition Position, bool Moved);
public readonly record struct MovementState(
    bool HasWaterproofShoeSpray, bool HasRainbowKey, bool HasUnderworldKey, bool HasTreeClimbingShoes);

public readonly record struct MapEvent(MapPosition Position, int Value)
{
    public static MapEvent None => new(new(-1, -1), -1);
    public bool HasPosition => Position.X >= 0 && Position.Y >= 0;
}

public readonly record struct PrincessEvent(MapPosition Position, int PortraitVariant, int RescueIndex)
{
    public static PrincessEvent None => new(new(-1, -1), 0, 0);
    public bool HasPosition => Position.X >= 0 && Position.Y >= 0;
}

public readonly record struct PursuingMonsterEvent(MapPosition Position, int TypeId, int UpgradedTypeId, int UpgradeLevel)
{
    public static PursuingMonsterEvent None => new(new(-1, -1), 0, 0, 0);
    public bool HasPosition => Position.X >= 0 && Position.Y >= 0;
    public int TypeForLevel(int level) => UpgradedTypeId > 0 && level >= UpgradeLevel ? UpgradedTypeId : TypeId;
}

public sealed class MapEvents
{
    public MapEvent Message { get; set; } = MapEvent.None;
    public MapEvent Overlay { get; set; } = MapEvent.None;
    public MapEvent[] Depth { get; } = [MapEvent.None, MapEvent.None];
    public MapEvent[] Linked { get; } = [MapEvent.None, MapEvent.None];
    public PrincessEvent Princess { get; set; } = PrincessEvent.None;
    public PursuingMonsterEvent Pursuer { get; set; } = PursuingMonsterEvent.None;
    public MapPosition[] Chests { get; } = [new(-1, -1), new(-1, -1), new(-1, -1), new(-1, -1)];

    internal MapEvents Copy()
    {
        var copy = new MapEvents { Message = Message, Overlay = Overlay, Princess = Princess, Pursuer = Pursuer };
        Depth.CopyTo(copy.Depth, 0);
        Linked.CopyTo(copy.Linked, 0);
        Chests.CopyTo(copy.Chests, 0);
        return copy;
    }
}

/// <summary>A mutable map instance. Reloading from the catalog restores its original tiles and events.</summary>
public sealed class Map
{
    public const int Width = 13;
    public const int Height = 12;
    public const int TileCount = Width * Height;
    private readonly ushort[] tiles;
    public int Id { get; }
    public int Environment { get; }
    public MapEvents Events { get; }

    public Map(int id, int environment, ReadOnlySpan<ushort> tiles, MapEvents? events = null)
    {
        if (tiles.Length != TileCount) throw new ArgumentException("A map must contain 156 tiles.", nameof(tiles));
        Id = id;
        Environment = environment;
        this.tiles = tiles.ToArray();
        Events = events?.Copy() ?? new MapEvents();
    }

    internal Map Copy() => new(Id, Environment, tiles, Events);
    public static bool Contains(MapPosition position) =>
        position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

    public ushort Tile(MapPosition position)
    {
        if (!Contains(position)) throw new ArgumentOutOfRangeException(nameof(position));
        return tiles[position.X * Height + position.Y];
    }

    public void RestoreTile(MapPosition position, ushort tile)
    {
        if (!Contains(position)) throw new ArgumentOutOfRangeException(nameof(position));
        tiles[position.X * Height + position.Y] = tile;
    }

    public int DepthActionAt(MapPosition position)
    {
        foreach (var entry in Events.Depth)
            if (entry.HasPosition && entry.Position == position) return entry.Value == 0x3a ? 0x0f : 0x10;
        return 0;
    }

    public void OpenDoor(MapPosition previous, MapPosition current) => RestoreTile(current, Tile(previous));

    // Original initialize_map_actions. Call before reapplying persistent tile changes.
    public void InitializeActions(Player player, MapPosition entry)
    {
        if (!Contains(entry)) throw new ArgumentOutOfRangeException(nameof(entry));
        for (var slot = 0; slot < Events.Chests.Length; slot++)
        {
            var position = Events.Chests[slot];
            if (Contains(position) && (Tile(position) == 0x24
                || (Id is >= 1 and <= 128 && (player.OpenedChests[Id - 1] & (1 << slot)) != 0)))
                RemoveChest(slot);
        }
        if (Id == 0x2d && player.Strength >= 40) RestoreTile(new(11, 9), 0x5f);
        else if (Id == 0x75 && unchecked((short)player.GatekeeperState) >= 1) RestoreTile(new(12, 1), 0x27);
        else if (Id == 3 && unchecked((short)player.StampsFound) >= 5) RestoreTile(new(4, 7), 0x46);
        if (Tile(entry) == 0x25) RestoreTile(entry, 0x53);
        if (Id != 0) player.HealingWaterAvailable = true;
    }

    public void RemoveChest(int slot)
    {
        if (slot < 0 || slot >= Events.Chests.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        Events.Chests[slot] = new(-1, -1);
    }

    public void RelocateOverlayAfterCombat(MapPosition position)
    {
        if (Environment == 5 && Events.Overlay.Value > 0 && Contains(position) && Tile(position) is 0x0c or 0x0d)
            Events.Overlay = Events.Overlay with { Position = position };
    }

    // Inherited D7: saved riders retain platform support without persisting the platform's phase.
    public void RestoreOverlayForSavedPosition(MapPosition position)
    {
        if (Events.Overlay.Value > 0 && Contains(position) && Tile(position) is 0x0c or 0x0d)
            Events.Overlay = Events.Overlay with { Position = position };
    }

    public bool AdvanceOverlay(MapVisibility visibility, ref MapPosition playerPosition, MsvcRandom random,
        MapPosition? roamingPosition = null)
    {
        var overlay = Events.Overlay;
        if (overlay.Value < 1 || !visibility.Contains(overlay.Position)) return false;
        var delta = overlay.Value switch
        {
            1 => new MapPosition(0, -1), 2 => new(1, 0), 3 => new(0, 1), 4 => new(-1, 0),
            5 => new(random.Next() % 3, random.Next() % 3), _ => new(0, 0),
        };
        if (overlay.Value > 5) return false;
        if (delta.X == 2) delta = delta with { X = -1 };
        if (delta.Y == 2) delta = delta with { Y = -1 };
        var target = new MapPosition(overlay.Position.X + delta.X, overlay.Position.Y + delta.Y);
        if (Contains(target) && Tile(target) is 0x0c or 0x0d && roamingPosition != target)
        {
            var carrying = playerPosition == overlay.Position;
            Events.Overlay = overlay with { Position = target };
            if (carrying) playerPosition = target;
            return carrying;
        }
        if (overlay.Value <= 4) Events.Overlay = overlay with { Value = (overlay.Value + 1) % 4 + 1 };
        return false;
    }

    public bool ToggleLinkedTiles()
    {
        var first = Events.Linked[0];
        var second = Events.Linked[1];
        if (!first.HasPosition || !Contains(first.Position) || first.Value < 0 || second.Value < 0
            || (second.HasPosition && !Contains(second.Position))) return false;
        var toggled = Tile(first.Position) == second.Value;
        RestoreTile(first.Position, unchecked((ushort)(toggled ? first.Value : second.Value)));
        if (second.HasPosition) RestoreTile(second.Position, unchecked((ushort)(toggled ? second.Value : first.Value)));
        return true;
    }

    public MapPosition? LinkedDestination => Events.Linked[0].HasPosition && Contains(Events.Linked[0].Position)
        ? Events.Linked[0].Position : null;

    public MapPosition? SurfaceReturnPosition()
    {
        MapPosition? result = null;
        for (var x = 1; x < Width - 1; x++)
            for (var y = 1; y < Height - 1; y++)
                if (Tile(new(x, y)) is 0x36 or 0x39) result = new(x, y + 1);
        return result;
    }

    // The original checks the destination only, including diagonal movement past blocked neighbors.
    public MovementResult Move(MapPosition from, int dx, int dy, MovementState state = default)
    {
        if (!Contains(from)) throw new ArgumentOutOfRangeException(nameof(from));
        if (dx is < -1 or > 1 || dy is < -1 or > 1 || (dx == 0 && dy == 0))
            throw new ArgumentException("Movement must be one of the eight adjacent steps.");
        var target = new MapPosition(from.X + dx, from.Y + dy);
        if (!Contains(target)) return new(0, from, false);
        var tile = Tile(target);
        if (Events.Overlay.HasPosition && Events.Overlay.Position == target && Events.Overlay.Value > 0) tile = 0x71;
        switch (tile)
        {
            case 1: return new(8, from, false);
            case 2 or 8 or 0x0b or 0x16 or 0x17 or 0x18 or 0x19 or 0x1a or 0x1b or 0x1c
                or 0x1d or 0x1e or 0x1f or 0x20 or 0x27 or 0x28 or 0x29 or 0x2a or 0x2c
                or 0x2d or 0x2f or 0x32 or 0x33 or 0x34 or 0x35 or 0x3c or 0x43 or 0x44
                or 0x45 or 0x53 or 0x65 or 0x66 or 0x67 or 0x68 or 0x69:
                return UpdatePosition(1, 2);
            case 4: return new(0x0d, from, false);
            case 5: return new(7, from, false);
            case 6: return new(5, from, false);
            case 7: return new(6, from, false);
            case 10: return new(9, from, false);
            case 0x0c: return new(0x0c, from, false);
            case 0x24: return new(0x17, from, false);
            case 0x30: return new(0x15, from, false);
            case 0x36 or 0x39: return UpdatePosition(0x0e, 0x0e);
            case 0x3e or 0x3f or 0x40:
                if (tile == 0x3f && !state.HasRainbowKey) return new(0x13, from, false);
                if (tile == 0x40 && !state.HasUnderworldKey) return new(0x14, from, false);
                return UpdatePosition(0x12, 0x12);
            case 0x41: return new(0x11, from, false);
            case 0x52: return new(0x1b, from, false);
            case 0x55: return new(0x16, from, false);
            case 0x56 or 0x57:
                return state.HasTreeClimbingShoes ? UpdatePosition(3, 2) : new(0, from, false);
            case 0x26 or 0x42 or 0x46 or 0x47 or 0x48 or 0x49: return UpdatePosition(3, 2);
            case 0x58 or 0x59:
                return state.HasWaterproofShoeSpray ? UpdatePosition(1, 1) : new(0, from, false);
            case 0x5a: return UpdatePosition(0x19, 2);
            case 0x5d or 0x5e or 0x5f: return UpdatePosition(0x1a, 2);
            case 0x71: return UpdatePosition(0x18, 0x18);
            default: return new(0, from, false);
        }

        MovementResult UpdatePosition(int interiorCode, int edgeCode)
        {
            var depth = DepthActionAt(target);
            var interior = target.X != 0 && target.X != Width - 1 && target.Y != 0 && target.Y != Height - 1;
            return new(depth != 0 ? depth : interior ? interiorCode : edgeCode, target, true);
        }
    }
}
