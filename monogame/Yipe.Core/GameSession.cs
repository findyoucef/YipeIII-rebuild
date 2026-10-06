namespace Yipe.Core;

public readonly record struct MapTileChange(int MapId, MapPosition Position, ushort Tile);
public enum MapTransition { None, Changed, Unavailable, Disabled }
public readonly record struct SessionMoveResult(
    int OriginalCode, MapPosition Position, bool PositionChanged, MapTransition Transition,
    bool Restored, int Damage);

/// <summary>Logical commands and map lifetime, independent of input, animation and storage.</summary>
public sealed class GameSession
{
    private readonly MapCatalog catalog;
    private readonly List<MapTileChange> tileChanges = new();
    public Player Player { get; }
    public WorldLocation Location { get; private set; }
    public MsvcRandom Random { get; }
    public Map CurrentMap { get; private set; } = null!;
    public IReadOnlyList<MapTileChange> TileChanges { get; }
    // Permanent placement IDs are distinct from per-monster defeat counters on Player.
    // Temporary defeats and active monsters will remain map-instance state (P2/P3).
    public List<ushort> DefeatedMonsterPlacements { get; } = new();

    // The same opening sequence serves new games and restored state (including D7).
    public GameSession(MapCatalog catalog, Player? player = null, WorldLocation? location = null,
        uint randomState = 1, IEnumerable<MapTileChange>? tileChanges = null,
        IEnumerable<ushort>? defeatedMonsterPlacements = null)
    {
        this.catalog = catalog;
        Player = player ?? new Player();
        Location = location?.Copy() ?? new();
        Random = new(randomState);
        TileChanges = this.tileChanges.AsReadOnly();
        if (defeatedMonsterPlacements is not null) DefeatedMonsterPlacements.AddRange(defeatedMonsterPlacements);
        if (!Map.Contains(Location.Position)) throw new ArgumentOutOfRangeException(nameof(location));
        if (tileChanges is not null)
            foreach (var change in tileChanges)
            {
                if (change.MapId < 1 || !Map.Contains(change.Position))
                    throw new ArgumentException("Invalid persistent map tile.", nameof(tileChanges));
                RememberTile(change);
            }
        // C++ loads (and generates) the saved map before applying a queued relocation.
        var map = catalog.LoadMap(Location.MapId, Location.Position, Random)
            ?? throw new InvalidOperationException($"Map {Location.MapId} is not available.");
        if (ApplyPendingRelocation() != PendingRelocation.None) return;
        SynchronizePlayerLocation();
        EnterMap(map);
        CurrentMap.RestoreOverlayForSavedPosition(Location.Position);
    }

    public SessionMoveResult Move(int dx, int dy, bool allowMapChanges = true)
    {
        var previous = Location;
        var from = previous.Position;
        var movement = CurrentMap.Move(from, dx, dy, new(Player.HasWaterproofShoeSpray,
            Player.HasRainbowKey, Player.HasUnderworldKey, Player.HasTreeClimbingShoes));
        var transition = MapTransition.None;
        var restored = false;
        var damage = 0;
        if (movement.Moved)
        {
            var next = previous.Copy();
            next.Position = movement.Position;
            var changedMap = movement.OriginalCode == 2 ? next.CrossMapEdge()
                : next.ChangeMapDepth(movement.OriginalCode);
            if (changedMap)
            {
                // Disabled and unavailable transitions leave the session and RNG untouched.
                if (!allowMapChanges) return Result(MapTransition.Disabled);
                var map = catalog.LoadMap(next.MapId, next.Position, Random);
                if (map is null) return Result(MapTransition.Unavailable);
                if (movement.OriginalCode == 0x0e)
                {
                    Player.SurfaceReturnMapId = unchecked((short)previous.MapId);
                    Player.SurfaceReturnRegionX = unchecked((short)previous.RegionX);
                    Player.SurfaceReturnRegionY = unchecked((short)previous.RegionY);
                }
                Location = next;
                EnterMap(map);
                transition = MapTransition.Changed;
            }
            else
            {
                Location = next;
                if (movement.OriginalCode == 0x12)
                {
                    CurrentMap.OpenDoor(from, Location.Position);
                    RememberCurrentTile(Location.Position);
                }
                else if (movement.OriginalCode == 0x1a && CurrentMap.ToggleLinkedTiles())
                {
                    RememberCurrentTile(CurrentMap.Events.Linked[0].Position);
                    if (CurrentMap.Events.Linked[1].HasPosition)
                        RememberCurrentTile(CurrentMap.Events.Linked[1].Position);
                }
            }
            // Arrival continues in the reference with hint, message, princess, encounter,
            // then chest. Combat replaces the chest action. Those systems belong to P2/P3;
            // callers retain the original action code and the new map's authored events.
        }
        else
        {
            switch (movement.OriginalCode)
            {
                case 0x0c:
                    if (Player.RestorationUses < 7 - Player.Difficulty)
                    {
                        Player.RestorationUses++;
                        Player.Health = Player.MaximumHealth;
                        Player.SpellPoints = Player.MaximumSpellPoints;
                        Player.AbilityPoints = Player.MaximumAbilityPoints;
                        restored = true;
                    }
                    break;
                case 0x16:
                    // Waiting for a gatekeeper choice is a later interaction, not a free exit.
                    if (Location.MapId == 0x75 && Player.HellGatekeeperState == HellGatekeeperState.Waiting) break;
                    if (!allowMapChanges) return Result(MapTransition.Disabled);
                    var surface = catalog.CreateMap(MapCatalog.StartingMapId);
                    if (surface is null) return Result(MapTransition.Unavailable);
                    if (Location.MapId == 0x75 && Player.HellGatekeeperState != HellGatekeeperState.Defeated)
                        Player.HellGatekeeperState = HellGatekeeperState.Absent;
                    Location = previous.Copy();
                    Location.ReturnToSurface();
                    EnterMap(surface);
                    transition = MapTransition.Changed;
                    break;
                case 0x17:
                    damage = WorldActions.ApplyFireWall(Player, Random);
                    break;
                case 0x1b:
                    if (CurrentMap.LinkedDestination is { } destination)
                        Location.Position = destination;
                    break;
            }
        }
        return Result(transition);

        SessionMoveResult Result(MapTransition status) => new(movement.OriginalCode, Location.Position,
            Location.Position != from || status == MapTransition.Changed, status, restored, damage);
    }

    public ItemUseResult UseInventoryItem(int slot, bool playSound = true)
    {
        var result = Player.UseInventoryItem(slot, Random, playSound, inCombat: false,
            currentMapId: Location.MapId, alternateMapSet: Location.AlternateMapSet);
        ApplyPendingRelocation();
        return result;
    }

    public PendingRelocation ApplyPendingRelocation()
    {
        var relocation = (PendingRelocation)Player.PendingRelocation;
        var id = relocation switch
        {
            PendingRelocation.None => 0,
            PendingRelocation.Surface => Player.SurfaceReturnMapId,
            PendingRelocation.Yipe => MapCatalog.StartingMapId,
            PendingRelocation.Hell => 0x75,
            PendingRelocation.StoneCircle => 0x15,
            _ => throw new InvalidOperationException("Player has an invalid pending relocation."),
        };
        if (relocation == PendingRelocation.None) return relocation;
        var map = catalog.CreateMap(id)
            ?? throw new InvalidOperationException($"Relocation destination {id} is not available.");
        Location.ApplyPendingRelocation(Player);
        // Find the entrance in the original template, before entry actions and persisted edits.
        if (relocation == PendingRelocation.Surface && map.SurfaceReturnPosition() is { } entry)
            Location.Position = entry;
        EnterMap(map);
        return relocation;
    }

    private void EnterMap(Map map)
    {
        CurrentMap = map;
        map.InitializeActions(Player, Location.Position);
        SynchronizePlayerLocation();
        foreach (var change in tileChanges)
            if (change.MapId == map.Id) map.RestoreTile(change.Position, change.Tile);
        // Visibility and transient encounter reset/initialization arrive with those systems.
    }

    // Combat retreat, death and timed falls use the same map-entry rules as movement.
    internal void LoadLocation(WorldLocation location)
    {
        var map = catalog.LoadMap(location.MapId, location.Position, Random)
            ?? throw new InvalidOperationException($"Map {location.MapId} is not available.");
        Location = location.Copy();
        EnterMap(map);
    }

    internal void RestoreLocation(WorldLocation location)
    {
        if (CurrentMap.Id != location.MapId) LoadLocation(location);
        else { Location = location.Copy(); SynchronizePlayerLocation(); }
    }

    private void SynchronizePlayerLocation()
    {
        Player.CurrentMapId = Location.MapId;
        Player.AlternateMapSet = Location.AlternateMapSet;
    }

    private void RememberCurrentTile(MapPosition position) =>
        RememberTile(new(CurrentMap.Id, position, CurrentMap.Tile(position)));

    private void RememberTile(MapTileChange change)
    {
        var index = tileChanges.FindIndex(c => c.MapId == change.MapId && c.Position == change.Position);
        if (index >= 0) tileChanges[index] = change;
        else tileChanges.Add(change);
    }
}
