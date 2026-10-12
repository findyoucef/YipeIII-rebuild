using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Shared map artwork. Event positions and visibility come from the live adventure.</summary>
internal sealed partial class WorldRenderer : IDisposable
{
    private readonly Texture2D floors, outdoors, meadow, props, buildings, shops, pixel, desert, flora, water, quests, paths, sandLayer, snowGround, snowProps;
    private readonly Texture2D[] monsters;
    private readonly int[] monsterBottoms = new int[44];
    private readonly int[] monsterTops = Enumerable.Repeat(48, 44).ToArray();
    private readonly Texture2D[] heroes;
    private readonly MountainTerrain mountains;
    private readonly MapCatalog catalog;

    public WorldRenderer(GraphicsDevice device, string content, MapCatalog catalog)
    {
        this.catalog = catalog;
        desert = RetroArt.Load(device, content, "World/desert.png", 2, 2, 32);
        sandLayer = CreateSand(device);
        flora = RetroArt.Load(device, content, "World/flora.png", 4, 3, 48);
        snowProps = RetroArt.Load(device, content, "World/snow-props.png", 2, 2, 48);
        snowGround = new Texture2D(device, SnowWidth, SnowHeight);
        quests = RetroArt.Load(device, content, "World/quests.png", 2, 2, 48);
        heroes = [RetroArt.Load(device, content, "World/heroes-01.png", 4, 8, 32),
            RetroArt.Load(device, content, "World/heroes-02.png", 4, 4, 32),
            RetroArt.Load(device, content, "World/heroes-03.png", 4, 4, 32),
            RetroArt.Load(device, content, "World/heroes-04.png", 4, 8, 32)];
        floors = RetroArt.Load(device, content, "World/terrain.png", 4, 4, 32);
        props = RetroArt.Load(device, content, "World/props.png", 4, 4, 48);
        outdoors = RetroArt.Load(device, content, "terrain.png", 2, 2, 32);
        mountains = new MountainTerrain(device, outdoors, catalog, desert);
        water = CreateWater(device);
        paths = RetroArt.CreatePaths(device, outdoors);
        paths.GetData(outdoorPathPattern);
        meadow = RetroArt.Load(device, content, "meadow.png", 2, 2, 32);
        meadow.GetData(meadowPattern);
        outdoorGround = new Texture2D(device, WaterWidth, WaterHeight);
        buildings = RetroArt.Load(device, content, "town.png", 3, 3, 48);
        shops = RetroArt.Load(device, content, "shops.png", 3, 3, 48);
        monsters = [RetroArt.Load(device, content, "World/monsters-01.png", 4, 4, 48),
            RetroArt.Load(device, content, "World/monsters-02.png", 4, 4, 48),
            RetroArt.Load(device, content, "World/monsters-03.png", 4, 3, 48)];
        // World feet follow visible artwork, not the transparent atlas padding.
        // Keep the atlas itself unchanged for combat portraits.
        for (var sheet = 0; sheet < monsters.Length; sheet++)
        {
            var atlas = monsters[sheet];
            var data = new Color[atlas.Width * atlas.Height]; atlas.GetData(data);
            for (var y = 0; y < atlas.Height; y++)
                for (var x = 0; x < atlas.Width; x++)
                    if (data[y * atlas.Width + x].A != 0)
                    {
                        var index = sheet * 16 + y / 48 * 4 + x / 48;
                        monsterBottoms[index] = Math.Max(monsterBottoms[index], y % 48 + 1);
                        monsterTops[index] = Math.Min(monsterTops[index], y % 48);
                    }
        }
        pixel = new Texture2D(device, 1, 1);
        pixel.SetData([Color.White]);
        LoadMonsterAnimations(device, content);
        hellProps = LoadHell(device, content);
        hellGround = new Texture2D(device, HellWidth, HellHeight);
        hellLava = new Texture2D(device, HellWidth * 4, HellHeight * 2);
        hellGlow = new Texture2D(device, HellWidth, HellHeight);
        earthGround = new Texture2D(device, EarthWidth, EarthHeight);
        stoneGround = new Texture2D(device, StoneWidth, StoneHeight);
        finalGround = new Texture2D(device, FinalWidth, FinalHeight);
        iceGround = new Texture2D(device, IceWidth, IceHeight);
        waterCaveGround = new Texture2D(device, WaterWidth, WaterHeight);
        breedingGround = new Texture2D(device, Map.Width * 32, Map.Height * 32);
        fireGround = new Texture2D(device, FireWidth, FireHeight);
        fireLava = new Texture2D(device, FireWidth * 4, FireHeight * 2);
    }

    private static Rectangle Cell(int index, int columns, int size) => new(index % columns * size, index / columns * size, size, size);
    public (Texture2D Texture, int Row) Character(int portrait)
    {
        var index = Math.Clamp(portrait, 1, 12) - 1;
        var atlas = index < 4 ? 0 : index < 6 ? 1 : index < 8 ? 2 : 3;
        var row = index - (atlas switch { 0 => 0, 1 => 4, 2 => 6, _ => 8 });
        return (heroes[atlas], row * 2);
    }
    public void DrawPortrait(SpriteBatch batch, int portrait, Rectangle destination)
    {
        var (texture, row) = Character(portrait);
        batch.Draw(texture, destination, new Rectangle(0, row * 32, 32, 32), Color.White);
    }

    internal FootstepSurface FootstepAt(Map map, MapPosition position)
    {
        var tile = map.Tile(position);
        if (IsWaterCave(map) && map.Events.Overlay is { Value: > 0 } raft && raft.Position == position)
            return FootstepSurface.Wood;
        if (tile == 0x0b) return FootstepSurface.Wood;
        PrepareWater(map);
        var x = position.X * 32 + 16; var y = position.Y * 32 + 25;
        if (WaterAt(x, y)) return FootstepSurface.Water;
        PrepareSnow(map);
        if (SnowAt(x, y)) return FootstepSurface.Snow;
        if (map.Id < 41 && SnowTile(tile)) return FootstepSurface.Grass;
        if (IsHell(map)) return FootstepSurface.Stone;
        if (IsFire(map)) return FireFootstep(map, position);
        var stone = VillagePathConnections(map, position) != 0 || MountainTerrain.IsMountain(tile)
            || tile is 9 or 0x1f or 0x20 or 0x22 or 0x23 or 0x25 or 0x43 or 0x44 or 0x5a or >= 0x65 and <= 0x69
            || map.Id < 41 && tile is (>= 0x27 and <= 0x2b or 0x47 or 0x48 or 0x52 or 0x54 or 0x55 or 0x5d or 0x5e or 0x61);
        if (stone) return FootstepSurface.Stone;
        if (IsEarth(map)) return FootstepSurface.Sand;
        if (IsBreeding(map)) return FootstepSurface.Sand;
        if (map.Id >= 41) return FootstepSurface.Stone;
        PrepareDesert(map);
        return desertActive && sandPixels[y * SandWidth + x].A > 0 ? FootstepSurface.Sand : FootstepSurface.Grass;
    }
    public void DrawMonster(SpriteBatch batch, int type, Rectangle destination, Color? tint = null)
    {
        if (type is < 1 or > 44) throw new ArgumentOutOfRangeException(nameof(type));
        var index = type - 1;
        batch.Draw(monsters[index / 16], destination, Cell(index % 16, 4, 48), tint ?? Color.White);
    }

    private void DrawWorldMonster(SpriteBatch batch, int type, MapPosition cell, int scale = 1)
    {
        var feet = new Point(cell.X * 32 + 16 * scale, (cell.Y + scale) * 32);
        // A small stepped oval stays inside the occupied tile(s), under the feet.
        for (var row = 0; row < 4; row++)
        {
            var radius = row is 1 or 2 ? 11 : 8;
            batch.Draw(pixel, new Rectangle(feet.X - radius * scale, feet.Y - (4 - row) * scale,
                (radius * 2 + 1) * scale, scale), RetroArt.Palette[0] * (row is 1 or 2 ? .22f : .12f));
        }
        DrawMonster(batch, type, new Rectangle(feet.X - 24 * scale,
            feet.Y - monsterBottoms[type - 1] * scale, 48 * scale, 48 * scale));
    }

    public void Draw(SpriteBatch batch, Adventure game, PlayerMotion hero, TownRenderer town, double time, bool miniature, float rainStrength = 0)
    {
        var map = game.Map;
        PrepareWater(map);
        PrepareDesert(map);
        PrepareHell(map);
        PrepareEarth(map);
        PrepareStone(map);
        PrepareFinal(map);
        PrepareIce(map);
        PrepareWaterCave(map);
        PrepareBreeding(map);
        PrepareFire(map);
        PrepareSnow(map);
        mountains.Prepare(map.Id, map.Tile);
        PrepareOutdoors(map);
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var cell = new MapPosition(x, y);
                if (!game.Visibility.Contains(cell)) continue;
                DrawGround(batch, map, cell, time);
                if (!IsWaterCave(map) && map.Events.Overlay is { Value: > 0 } overlay && overlay.Position == cell) DrawProp(batch, 12, cell);
            }

        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (game.Visibility.Contains(new(x, y)))
                {
                    DrawOutdoorPlants(batch, map, new(x, y), town, time);
                    if (IsVillage(map)) DrawVillageDetails(batch, map, new(x, y), town, time);
                }
        DrawOutdoorActivity(batch, game, time, rainStrength, OutdoorDaylight);
        if (rainStrength < .15f && OutdoorDaylight > .35f) DrawVillageActivity(batch, game, time);
        if (rainStrength < .15f) DrawDesertWind(batch, game, time);
        DrawHellAtmosphere(batch, game, time);
        DrawEarthAtmosphere(batch, game, time);
        DrawStoneAtmosphere(batch, game, time);
        DrawFinalAtmosphere(batch, game, time);
        DrawIceAtmosphere(batch, game, time);
        DrawWaterCaveAtmosphere(batch, game, time);
        DrawBreedingAtmosphere(batch, game, time);
        DrawFireAtmosphere(batch, game, time);
        DrawSnowWind(batch, game, time);
        DrawSnowTracks(batch, game, time);
        town.DrawWorldReflection(batch, hero, time, game, this);
        DrawIceMonsterReflections(batch, game, hero, time);
        if (map.Id is >= 1 and <= 40)
            Rain.DrawSplashes(batch, pixel, time, rainStrength, map.Tile, WaterAt, SnowColdAt);
        // Water and reflections precede every deck, so they cannot cover its planks.
        DrawRaft(batch, game, time);
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (game.Visibility.Contains(new(x, y)))
                {
                    if (map.Tile(new(x, y)) == 11)
                        batch.Draw(buildings, new Vector2(x * 32 - 8, y * 32 - 8), Cell(7, 3, 48), Color.White);
                }

        var placements = game.ActivePlacements;
        var heroDrawn = false;
        for (var y = 0; y < Map.Height; y++)
        {
            if (!heroDrawn && hero.VisualPosition.Y * 32 + 25 < y * 32 - 4)
            {
                town.DrawHero(batch, hero, time, miniature, footsteps: false);
                heroDrawn = true;
            }
            for (var x = 0; x < Map.Width; x++)
            {
                var cell = new MapPosition(x, y);
                if (!game.Visibility.Contains(cell)) continue;
                DrawScenery(batch, map, cell, time, town, miniature);
                foreach (var depth in map.Events.Depth)
                    if (depth.HasPosition && depth.Position == cell) DrawProp(batch, depth.Value == 0x3a ? 3 : 4, cell);
                if (map.Events.Message is { HasPosition: true } sign && sign.Position == cell) DrawProp(batch, 2, cell);
                if (map.Events.Chests.Contains(cell)) DrawProp(batch, 0, cell);
                if (map.Events.Princess is { HasPosition: true } princess && princess.Position == cell
                    && game.Player.InventoryValues[princess.RescueIndex] == 0)
                    batch.Draw(quests, new Vector2(x * 32 - 8, y * 32 - 16), Cell(princess.PortraitVariant - 1, 2, 48), Color.White);
                foreach (var placement in placements)
                    if (placement.Position == cell)
                    {
                        var scale = placement.OccupiesTwoByTwoTiles ? 2 : 1;
                        if (HasMonsterAnimation(placement.TypeId))
                            DrawMonsterActor(batch, placement.TypeId, new(cell.X, cell.Y), scale, time, hero.VisualPosition.X < cell.X);
                        else DrawWorldMonster(batch, placement.TypeId, cell, scale);
                    }
            }
            // Draw the waiting guard after the arch and neighboring scenery, preserving his face and key.
            if (y == 1 && map.Id == 117 && game.Visibility.Contains(new(6, 1)) && game.Visibility.Contains(new(6, 0))
                && game.Player.HellGatekeeperState == HellGatekeeperState.Waiting)
                DrawAnimatedMonster(batch, 41, new(6 * 32 - 8, 0, 48, 48), MonsterIdleFrame(41, time));
            DrawRoamingActor(batch, game, y, time);
        }
        if (!heroDrawn) town.DrawHero(batch, hero, time, miniature, footsteps: false);
        // Hide unknown terrain and atmosphere, but keep a known door's crown intact.
        if (IsHell(map) || map.Id <= 36)
            for (var y = 0; y < Map.Height; y++)
                for (var x = 0; x < Map.Width; x++)
                    if (!game.Visibility.Contains(new(x, y)))
                    {
                        batch.Draw(pixel, new Rectangle(x * 32, y * 32, 32, 32), RetroArt.Palette[0]);
                        if (IsHell(map) && game.Visibility.Contains(new(x, y + 1)))
                            DrawHellDoorCrown(batch, map, new(x, y + 1), time);
                    }
    }

    internal void DrawMap(SpriteBatch batch, Map map, bool[] seen, double time, TownRenderer town)
    {
        PrepareWater(map);
        PrepareDesert(map);
        PrepareHell(map);
        PrepareEarth(map);
        PrepareStone(map);
        PrepareFinal(map);
        PrepareIce(map);
        PrepareWaterCave(map);
        PrepareBreeding(map);
        PrepareFire(map);
        PrepareSnow(map);
        mountains.Prepare(map.Id, map.Tile);
        PrepareOutdoors(map);
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (seen[x * Map.Height + y]) DrawGround(batch, map, new(x, y), time);
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (seen[x * Map.Height + y])
                {
                    DrawOutdoorPlants(batch, map, new(x, y), town, time);
                    if (IsVillage(map)) DrawVillageDetails(batch, map, new(x, y), town, time);
                }
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                if (!seen[x * Map.Height + y]) continue;
                var cell = new MapPosition(x, y);
                if (map.Tile(cell) == 11) batch.Draw(buildings, new Vector2(x * 32 - 8, y * 32 - 8), Cell(7, 3, 48), Color.White);
                DrawScenery(batch, map, cell, time, town);
                foreach (var depth in map.Events.Depth)
                    if (depth.HasPosition && depth.Position == cell) DrawProp(batch, depth.Value == 0x3a ? 3 : 4, cell);
            }
        // Tall scenery from a known cell must not expose a neighboring unexplored cell.
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (!seen[x * Map.Height + y])
                {
                    batch.Draw(pixel, new Rectangle(x * 32, y * 32, 32, 32), RetroArt.Palette[0]);
                    if (IsHell(map) && y + 1 < Map.Height && seen[x * Map.Height + y + 1])
                        DrawHellDoorCrown(batch, map, new(x, y + 1), time);
                }
    }

    private void DrawScenery(SpriteBatch batch, Map map, MapPosition cell, double time, TownRenderer town, bool miniature = false)
    {
        var x = cell.X; var y = cell.Y;
        var tile = map.Tile(cell);
        if (IsVillage(map) && tile is 1 or 5 or 6 or 7)
        { town.DrawBuilding(batch, tile, cell, time, miniature); return; }
        if (IsHell(map)) { DrawHellScenery(batch, map, cell, time); return; }
        var building = tile switch { 1 or 4 => 3, 5 => 2, 6 => 0, 7 => 1, 10 => 4, 0x30 => 5, 0x36 or 0x39 => 6, _ => -1 };
        if (snowActive && (tile == 0x21 || tile == 0x39 && SnowColdAt(x * 32 + 16, y * 32 + 16)))
        { DrawSnowScenery(batch, map, cell, time); return; }
        if (snowActive) DrawSnowScenery(batch, map, cell, time);
        if (tile == 0x36)
            batch.Draw(quests, new Vector2(x * 32 - 8, y * 32 - 12), Cell(3, 2, 48), Color.White);
        else if (building >= 0)
            batch.Draw(building < 5 ? shops : buildings, new Vector2(x * 32 - 8, y * 32 - (building == 6 ? 12 : 16)), Cell(building, 3, 48), Color.White);
        var prop = tile switch
        {
            0x24 => 9, 0x26 => 8,
            0x3e => 5, 0x3f => 6, 0x40 => 7, 0x41 => 15,
            _ => -1,
        };
        if (prop >= 0) DrawProp(batch, prop, cell);
        var plant = tile switch { 0x12 => 10, 0x13 => 11, 0x21 => 4, 0x2e or 0x57 => 0, 0x31 or 0x56 => 5, _ => -1 };
        if (plant >= 0) DrawFlora(batch, plant, cell, time, map.Id <= 36);
        if (tile == 4) DrawCasinoSign(batch, cell);
    }

    private void DrawProp(SpriteBatch batch, int index, MapPosition cell) =>
        batch.Draw(props, new Rectangle(cell.X * 32 - 8, cell.Y * 32 - 17, 48, 48), Cell(index, 4, 48), Color.White);

    private void DrawSpikes(SpriteBatch batch, Rectangle tile, bool raised)
    {
        for (var i = 0; i < 3; i++)
        {
            var x = tile.X + 7 + i * 8;
            batch.Draw(pixel, new Rectangle(x - 2, tile.Y + 23, 5, 2), RetroArt.Palette[0]);
            if (!raised) continue;
            for (var row = 0; row < 7; row++)
                batch.Draw(pixel, new Rectangle(x - row / 3, tile.Y + 16 + row, row / 3 * 2 + 1, 1), RetroArt.Palette[4]);
        }
    }

    public void Dispose()
    {
        desert.Dispose(); sandLayer.Dispose(); flora.Dispose(); water.Dispose(); quests.Dispose(); paths.Dispose();
        floors.Dispose(); outdoors.Dispose(); meadow.Dispose(); props.Dispose(); buildings.Dispose(); shops.Dispose(); pixel.Dispose();
        foreach (var atlas in monsters) atlas.Dispose();
        foreach (var atlas in monsterAnimations.Values) atlas.Dispose();
        empoweredGatekeeperAnimation?.Dispose();
        foreach (var atlas in heroes) atlas.Dispose();
        hellProps.Dispose(); hellGround.Dispose(); hellLava.Dispose(); hellGlow.Dispose();
        earthGround.Dispose();
        stoneGround.Dispose();
        finalGround.Dispose();
        iceGround.Dispose();
        waterCaveGround.Dispose();
        breedingGround.Dispose(); fireGround.Dispose(); fireLava.Dispose();
        mountains.Dispose();
        snowGround.Dispose(); snowProps.Dispose();
        outdoorGround.Dispose();
    }
}
