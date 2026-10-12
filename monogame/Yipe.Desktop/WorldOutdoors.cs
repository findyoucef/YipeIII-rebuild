using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private readonly Texture2D outdoorGround;
    private readonly Color[] meadowPattern = new Color[64 * 64];
    private readonly Color[] outdoorPathPattern = new Color[128 * 512];
    private readonly ushort[] outdoorTiles = new ushort[Map.TileCount];
    private readonly Dictionary<int, Map> surfaceNeighbors = new();
    private readonly List<(int Sprite, Point Feet)> outdoorPlants = new();
    private readonly List<Point> outdoorLeaves = new();
    private Map? outdoorMap;
    internal float OutdoorDaylight { get; set; } = 1;

    // Cosmetic aliases must have the same plants, soil and sound as ordinary terrain.
    private static ushort OutdoorTile(ushort tile) => tile switch
    {
        0x46 => 2, 0x42 => 0x2d, 0x56 => 0x31, 0x57 => 0x2e,
        0x58 => 0x11, 0x59 => 0x0e, 0x69 => 0x1f, 0x45 => 3, _ => tile,
    };

    private ushort SurfaceTile(Map map, int x, int y)
    {
        if (Map.Contains(new(x, y))) return OutdoorTile(map.Tile(new(x, y)));
        // Rooms overlap by two tiles. Wrap on the original six-by-six world.
        var gx = Mod((map.Id - 1) % 6 * 11 + x, 66);
        var gy = Mod((map.Id - 1) / 6 * 10 + y, 60);
        var id = gy / 10 * 6 + gx / 11 + 1;
        if (!surfaceNeighbors.TryGetValue(id, out var neighbor))
            surfaceNeighbors[id] = neighbor = catalog.CreateMap(id)!;
        return OutdoorTile(neighbor.Tile(new(gx % 11, gy % 10)));
    }

    private static int Mod(int value, int size) => (value % size + size) % size;
    private static uint OutdoorHash(int x, int y)
    {
        var value = (uint)(Mod(x, 2112) * 374761393 + Mod(y, 1920) * 668265263);
        value = (value ^ (value >> 13)) * 1274126177;
        return value ^ (value >> 16);
    }

    private static float MeadowField(int x, int y)
    {
        // A periodic field shares identical pixels through overlapping rooms and world edges.
        var gx = Mod(x, 2112); var gy = Mod(y, 1920);
        var fx = MathHelper.SmoothStep(0, 1, gx % 96 / 96f);
        var fy = MathHelper.SmoothStep(0, 1, gy % 96 / 96f);
        float Corner(int dx, int dy) => (OutdoorHash(gx / 96 * 96 + dx, gy / 96 * 96 + dy) & 65535) / 65535f;
        return MathHelper.Lerp(MathHelper.Lerp(Corner(0, 0), Corner(96, 0), fx),
            MathHelper.Lerp(Corner(0, 96), Corner(96, 96), fx), fy);
    }

    private void PrepareOutdoors(Map map)
    {
        if (map.Id is < 1 or > 36 || map.Id == 17) return;
        var changed = !ReferenceEquals(outdoorMap, map);
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var tile = OutdoorTile(map.Tile(new(x, y)));
                var index = y * Map.Width + x;
                changed |= outdoorTiles[index] != tile;
                outdoorTiles[index] = tile;
            }
        if (!changed) return;
        outdoorMap = map;
        outdoorPlants.Clear(); outdoorLeaves.Clear();
        var pixels = new Color[WaterWidth * WaterHeight];
        var ox = (map.Id - 1) % 6 * 352; var oy = (map.Id - 1) / 6 * 320;
        for (var cy = 0; cy < Map.Height; cy++)
            for (var cx = 0; cx < Map.Width; cx++)
            {
                var tile = OutdoorTile(map.Tile(new(cx, cy)));
                var path = VillagePathConnections(map, new(cx, cy));
                var pathCell = Cell(path, 4, 32);
                var trees = new List<Point>();
                var rock = false; var flowers = false;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var near = SurfaceTile(map, cx + dx, cy + dy);
                        if (near is 0x31 or 0x2e) trees.Add(new((cx + dx) * 32 + 16, (cy + dy) * 32 + 23));
                        rock |= MountainTerrain.IsMountain(near);
                        flowers |= near is 0x32 or 0x33;
                    }
                if (tile == 0x31) outdoorLeaves.Add(new(cx * 32 + 16, cy * 32 + 4));
                for (var py = 0; py < 32; py++)
                    for (var px = 0; px < 32; px++)
                    {
                        var x = cx * 32 + px; var y = cy * 32 + py; var i = y * WaterWidth + x;
                        var gx = ox + x; var gy = oy + y;
                        var grain = (OutdoorHash(gx / 3 * 3, gy / 2 * 2) % 100) / 100f;
                        var field = MeadowField(gx / 2 * 2, gy / 2 * 2) + (grain - .5f) * .08f;
                        var shade = 0f;
                        foreach (var tree in trees) shade = Math.Max(shade, Math.Max(0, 1 - Vector2.Distance(new(x, y), tree.ToVector2()) / 44));
                        var wet = Math.Clamp((23 - shoreDistance[i]) / 23, 0, 1);
                        var baseColor = field < .35f ? new Color(102, 131, 70)
                            : field > .66f ? new Color(112, 139, 75) : RetroArt.Palette[11];
                        if (wet > .55f + grain * .15f || shade > .55f + grain * .12f) baseColor = new Color(94, 122, 67);
                        else if (wet > .2f + grain * .1f || shade > .23f + grain * .15f) baseColor = new Color(102, 129, 68);
                        var source = meadowPattern[Mod(gy, 64) * 64 + Mod(gx, 64)];
                        // Preserve the original grass's fine native-pixel marks inside broad quiet patches.
                        pixels[i] = source == RetroArt.Palette[11] ? baseColor : Color.Lerp(source, baseColor, .25f);
                        if (shade > .45f && OutdoorHash(gx / 3 * 3, gy / 2 * 2) % 29 == 0)
                            pixels[i] = RetroArt.Palette[(gx / 3 + gy / 2) % 3 == 0 ? 17 : 10];
                        if (desertActive && sandPixels[i].A > 0)
                        {
                            pixels[i] = shade > .35f + grain * .1f
                                ? Color.Lerp(sandPixels[i], RetroArt.Palette[16], .12f) : sandPixels[i];
                            if (shade > .55f && OutdoorHash(gx / 4 * 4, gy / 2 * 2) % 31 == 0)
                                pixels[i] = RetroArt.Palette[17];
                        }
                        if (path != 0)
                        {
                            bool Paved(int dx, int dy) => outdoorPathPattern[(pathCell.Y + Math.Clamp(py + dy, 0, 31)) * 128
                                + pathCell.X + Math.Clamp(px + dx, 0, 31)].A > 0;
                            var reach = grain > .6f ? 2 : 3;
                            if (!Paved(0, 0) && (Paved(reach, 0) || Paved(-reach, 0) || Paved(0, reach) || Paved(0, -reach)))
                                pixels[i] = Color.Lerp(pixels[i], RetroArt.Palette[17], .27f);
                        }
                    }

                // A jittered world-space candidate per cell; clusters follow terrain, never simulation RNG.
                var hash = OutdoorHash(ox + cx * 32, oy + cy * 32);
                var feet = new Point(cx * 32 + 5 + (int)(hash % 23), cy * 32 + 9 + (int)(hash / 23 % 23));
                if (!OutdoorPlantFits(map, feet)) continue;
                var ground = feet.Y * WaterWidth + feet.X;
                var waterNear = shoreDistance[ground] is > 4 and < 23;
                var dry = sandField[ground] > .65f && desertActive;
                var fieldHere = MeadowField(ox + feet.X, oy + feet.Y);
                var roadNear = false;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                        roadNear |= RetroArt.PathConnections(SurfaceTile(map, cx + dx, cy + dy)) != 0;
                var chance = waterNear ? 85 : trees.Count > 0 ? 65 : flowers ? 65 : rock ? 48 : roadNear ? 35
                    : fieldHere > .62f ? 45 : fieldHere < .33f ? 25 : 5;
                if (IsVillage(map)) chance = map.Id == 27 ? 30 : 22;
                if (hash / 31 % 100 >= chance) continue;
                var sprite = waterNear ? 4 : dry ? (hash % 3 == 0 ? 2 : 3)
                    : rock && hash % 3 == 0 ? 2 : flowers || fieldHere > .58f ? (int)(hash % 2)
                    : trees.Count > 0 && hash % 2 == 0 ? 5 : 3;
                outdoorPlants.Add((sprite, feet));
                if (hash % 3 == 0)
                {
                    var companion = new Point(feet.X + 13, feet.Y + 7);
                    if (OutdoorPlantFits(map, companion)) outdoorPlants.Add((sprite is 0 or 1 ? 3 : sprite, companion));
                }
            }
        // Authored garden borders extend Ervyn's existing beds, leaving its roads and door approaches clear.
        if (map.Id == 27)
            foreach (var feet in new Point[] { new(78, 116), new(180, 93), new(234, 90), new(331, 119),
                new(73, 266), new(184, 301), new(245, 305), new(343, 270), new(60, 166), new(364, 180) })
                if (OutdoorPlantFits(map, feet))
                {
                    outdoorPlants.Add((5, feet));
                    outdoorLeaves.Add(new(feet.X, feet.Y - 18));
                }
        outdoorPlants.Sort((a, b) => a.Feet.Y.CompareTo(b.Feet.Y));
        if (map.Id == 27)
        {
            // Two tended flower borders with broken, native-pixel soil edges.
            foreach (var bed in new Rectangle[] { new(102, 76, 60, 12), new(252, 77, 67, 12),
                new(106, 265, 55, 16), new(256, 267, 58, 16) })
                for (var y = bed.Top; y < bed.Bottom; y++)
                    for (var x = bed.Left; x < bed.Right; x++)
                    {
                        var rim = Math.Min(Math.Min(x - bed.Left, bed.Right - 1 - x), Math.Min(y - bed.Top, bed.Bottom - 1 - y));
                        if (rim < OutdoorHash(x / 2, y) % 3 || map.Tile(new(x / 32, y / 32)) != 2) continue;
                        pixels[y * WaterWidth + x] = (x / 3 + y / 2) % 5 == 0 ? new Color(117, 103, 63) : new Color(100, 94, 57);
                    }
        }
        outdoorGround.SetData(pixels);
    }

    private bool OutdoorPlantFits(Map map, Point feet)
    {
        // Low plant art has no collision. Reserve the complete footprint around paths, props and water.
        for (var y = feet.Y - 10; y <= feet.Y + 2; y += 2)
            for (var x = feet.X - 9; x <= feet.X + 9; x += 3)
            {
                if (x < 0 || y < 0 || x >= WaterWidth || y >= WaterHeight) return false;
                var cell = new MapPosition(x / 32, y / 32);
                var tile = OutdoorTile(map.Tile(cell));
                if (tile is not (2 or 0x2d or >= 0x1b and <= 0x1e) || VillagePathConnections(map, cell) != 0
                    || WaterAt(x, y) || SnowColdAt(x, y)) return false;
                if (map.Events.Chests.Contains(cell) || map.Events.Depth.Any(e => e.HasPosition && e.Position == cell)
                    || map.Events.Message is { HasPosition: true } sign && sign.Position == cell) return false;
            }
        return true;
    }

    private void DrawOutdoorPlants(SpriteBatch batch, Map map, MapPosition cell, TownRenderer town, double time)
    {
        if (map.Id is < 1 or > 36 || map.Id == 17) return;
        foreach (var plant in outdoorPlants)
            if (plant.Feet.X / 32 == cell.X && plant.Feet.Y / 32 == cell.Y)
                town.DrawProp(batch, plant.Sprite, plant.Feet.ToVector2(), time);
    }

    private void DrawOutdoorActivity(SpriteBatch batch, Adventure game, double time, float rain, float daylight)
    {
        if (game.Map.Id is < 1 or > 36 || game.Map.Id == 17) return;
        var clock = Math.Floor(time * 10) / 10;
        foreach (var source in outdoorLeaves)
            for (var leaf = 0; leaf < 3; leaf++)
            {
                var phase = (clock + source.X * .13 + source.Y * .07 + leaf * 5.7) % 19;
                if (phase > 12) continue;
                var fall = Math.Min(1, phase / 7);
                var x = source.X - 12 + leaf * 8 + (int)(fall * 25 + Math.Sin(phase * 2 + leaf) * 4);
                var y = source.Y + (int)(fall * 42);
                if (!Visible(x, y)) continue;
                var color = RetroArt.Palette[leaf == 0 ? 17 : 12] * (float)Math.Min(1, (12 - phase) / 2);
                batch.Draw(pixel, new Rectangle(x, y, (int)(phase * 4) % 3 == 0 ? 1 : 3, 1), color);
                batch.Draw(pixel, new Rectangle(x + 1, y + 1, 1, 1), RetroArt.Palette[10]);
            }
        if (rain > .15f || daylight < .35f) return;
        foreach (var plant in outdoorPlants)
        {
            if (plant.Sprite is not (0 or 1 or 3 or 4) || (plant.Feet.X + plant.Feet.Y) % 3 != 0) continue;
            var phase = clock * .8 + plant.Feet.X;
            var x = plant.Feet.X + (int)(Math.Sin(phase) * 12);
            var y = plant.Feet.Y - 13 + (int)(Math.Cos(phase * .7) * 6);
            if (!Visible(x, y)) continue;
            if (plant.Sprite == 3)
            {
                if (desertActive && sandField[plant.Feet.Y * WaterWidth + plant.Feet.X] > .3f) continue;
                x += (int)(clock % 9 * 2); y -= (int)(clock % 9);
                if (Visible(x, y)) batch.Draw(pixel, new Rectangle(x, y, 2, 1), RetroArt.Palette[13] * .5f);
            }
            else if (plant.Sprite == 4)
            {
                batch.Draw(pixel, new Rectangle(x, y, 3, 1), RetroArt.Palette[29]);
                batch.Draw(pixel, new Rectangle(x + 1, y - 1, 1, 3), RetroArt.Palette[6] * .65f);
            }
            else
            {
                var wings = (int)(clock * 10 + plant.Feet.Y) % 3 == 0 ? 1 : 2;
                batch.Draw(pixel, new Rectangle(x - wings, y, wings * 2 + 1, 1), RetroArt.Palette[plant.Sprite == 0 ? 33 : 6]);
                batch.Draw(pixel, new Rectangle(x, y + 1, 1, 1), RetroArt.Palette[15]);
            }
        }
        bool Visible(int x, int y) => x >= 1 && y >= 1 && x < WaterWidth - 4 && y < WaterHeight - 3
            && game.Visibility.Contains(new(x / 32, y / 32)) && !SnowColdAt(x, y)
            && !MountainTerrain.IsMountain(game.Map.Tile(new(x / 32, y / 32)));
    }

    internal (float Dry, float Cold, float Wood) OutdoorHabitatAt(Map map, Vector2 position)
    {
        PrepareWater(map); PrepareDesert(map); PrepareSnow(map);
        var feet = position * 32 + new Vector2(16, 25);
        float dry = 0, cold = 0, wood = 0, weight = 0;
        // Blend over the same nearby material footprint while walking between habitats.
        for (var dy = -64; dy <= 64; dy += 16)
            for (var dx = -64; dx <= 64; dx += 16)
            {
                var x = Math.Clamp((int)feet.X + dx, 0, WaterWidth - 1);
                var y = Math.Clamp((int)feet.Y + dy, 0, WaterHeight - 1);
                var w = Math.Max(0, 1 - MathF.Sqrt(dx * dx + dy * dy) / 80);
                weight += w;
                dry += (desertActive ? sandField[y * WaterWidth + x] : 0) * w;
                cold += (SnowColdAt(x, y) ? 1 : 0) * w;
                wood += (OutdoorTile(map.Tile(new(x / 32, y / 32))) == 0x31 ? 1 : 0) * w;
            }
        return (dry / weight, cold / weight, Math.Min(1, wood / weight * 4));
    }

    internal (float Gain, float Pan) OutdoorWaterSoundAt(Map map, Vector2 position)
    {
        PrepareWater(map);
        var feet = position * 32 + new Vector2(16, 25);
        const int reach = 104;
        var amount = 0f; var waterX = feet.X;
        for (var y = Math.Max(0, (int)(feet.Y - reach) / 4 * 4); y < Math.Min(WaterHeight, feet.Y + reach); y += 4)
            for (var x = Math.Max(0, (int)(feet.X - reach) / 4 * 4); x < Math.Min(WaterWidth, feet.X + reach); x += 4)
            {
                if (!WaterAt(x, y)) continue;
                var tile = WaterTile(map.Tile(new(x / 32, y / 32)));
                var gain = tile is 0x0c or 0x0d or >= 0x4a and <= 0x51 ? .35f : 1;
                var distance = Math.Clamp((Vector2.Distance(feet, new(x, y)) - 12) / (reach - 12), 0, 1);
                var strength = gain * (1 - MathHelper.SmoothStep(0, 1, distance));
                if (strength <= amount) continue;
                amount = strength; waterX = x;
            }
        return (amount, Math.Clamp((waterX - feet.X) / 80, -.65f, .65f));
    }
}
