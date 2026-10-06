namespace Yipe.Core;

public enum PendingRelocation : ushort { None, Surface, Yipe, Hell, StoneCircle }

/// <summary>Original region, layer and alternate-map coordinates. Transition rules do not load or render maps.</summary>
public sealed class WorldLocation
{
    public MapPosition Position { get; set; } = new(5, 5);
    public int RegionX { get; set; }
    public int RegionY { get; set; }
    public int MapLayer { get; set; }
    public bool AlternateMapSet { get; set; }
    public int MapId { get; set; } = MapCatalog.StartingMapId;

    internal WorldLocation Copy() => (WorldLocation)MemberwiseClone();

    private int ComputedMapId => RegionX - RegionY * 6 + 0x11 + MapLayer * 0x28 + (AlternateMapSet ? 0x62 : 0);

    public bool CrossMapEdge()
    {
        var crossed = false;
        if (Position.X == 0)
        {
            RegionX--;
            Position = Position with { X = Map.Width - 2 };
            crossed = true;
        }
        else if (Position.X == Map.Width - 1)
        {
            RegionX++;
            Position = Position with { X = 1 };
            crossed = true;
        }
        if (Position.Y == 0)
        {
            RegionY++;
            Position = Position with { Y = Map.Height - 2 };
            crossed = true;
        }
        else if (Position.Y == Map.Height - 1)
        {
            RegionY--;
            Position = Position with { Y = 1 };
            crossed = true;
        }
        if (!crossed) return false;
        if (RegionX == 2) RegionX = -4;
        else if (RegionX == -5) RegionX = 1;
        if (RegionY == 3) RegionY = -3;
        else if (RegionY == -4) RegionY = 2;
        MapId = ComputedMapId;
        return true;
    }

    public bool ChangeMapDepth(int originalCode)
    {
        var change = originalCode switch { 0x0f => -1, 0x0e or 0x10 => 1, _ => 0 };
        if (change == 0) return false;
        CrossMapEdge();
        MapLayer += change;
        if (MapLayer is < 0 or > 2) ReturnToSurface();
        else MapId = ComputedMapId;
        return true;
    }

    public void AscendToSurface()
    {
        MapLayer = 0;
        MapId = ComputedMapId;
    }

    public void ReturnToSurface()
    {
        Position = new(4, 5);
        RegionX = RegionY = MapLayer = 0;
        AlternateMapSet = false;
        MapId = MapCatalog.StartingMapId;
    }

    public PendingRelocation ApplyPendingRelocation(Player player)
    {
        var relocation = (PendingRelocation)player.PendingRelocation;
        switch (relocation)
        {
            case PendingRelocation.None: return relocation;
            case PendingRelocation.Surface:
                RegionX = player.SurfaceReturnRegionX;
                RegionY = player.SurfaceReturnRegionY;
                MapLayer = 0;
                AlternateMapSet = false;
                MapId = player.SurfaceReturnMapId;
                break;
            case PendingRelocation.Yipe:
                ReturnToSurface();
                break;
            case PendingRelocation.Hell:
                EnterHell(player, true);
                break;
            case PendingRelocation.StoneCircle:
                Position = new(6, 8);
                RegionX = -2;
                RegionY = -1;
                MapLayer = 0;
                AlternateMapSet = false;
                MapId = 0x15;
                break;
            default: throw new InvalidOperationException("Player has an invalid pending relocation.");
        }
        player.PendingRelocation = (ushort)PendingRelocation.None;
        player.CurrentMapId = MapId;
        player.AlternateMapSet = AlternateMapSet;
        return relocation;
    }

    public void EnterHell(Player player, bool throughSecretPassage)
    {
        player.EnteredHellThroughSecretPassage = throughSecretPassage;
        if (player.HellGatekeeperState == HellGatekeeperState.Absent)
            player.HellGatekeeperState = HellGatekeeperState.Waiting;
        Position = new(6, 10);
        RegionX = -4;
        RegionY = -1;
        MapLayer = 0;
        AlternateMapSet = true;
        MapId = 0x75;
        player.CurrentMapId = MapId;
        player.AlternateMapSet = true;
    }
}
