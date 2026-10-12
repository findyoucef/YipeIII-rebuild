using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Native pixel art and discrete ambient effects, independent of collision tiles.</summary>
internal sealed partial class TownRenderer : IDisposable
{
    private static readonly Color[] Palette = RetroArt.Palette;
    private readonly TownMap map;
    private readonly Texture2D terrain;
    private readonly Texture2D town;
    private readonly Texture2D hero;
    private readonly Texture2D pixel;
    private readonly MountainTerrain mountains;
    // Shared alternate sheets belong to WorldRenderer; the approved default stays local.
    public (Texture2D Texture, int Row)? AlternateHero { get; set; }
    public SunShafts? Sunlight { get; set; }
    private Texture2D HeroTexture => AlternateHero?.Texture ?? hero;

    public TownRenderer(GraphicsDevice device, TownMap map, string content, MapCatalog catalog)
    {
        this.map = map;
        terrain = Load("terrain.png", 2, 2, 32);
        using (var sand = Load("World/desert.png", 2, 2, 32))
            mountains = new MountainTerrain(device, terrain, catalog, sand);
        town = Load("town.png", 3, 3, 48);
        // Replace only the five shop cells; keep the castle, cave, bridge and stones intact.
        using (var shops = Load("shops.png", 3, 3, 48))
        {
            var frame = new Color[48 * 48];
            for (var sprite = 0; sprite < 5; sprite++)
            {
                var cell = Cell(sprite, 3, 48);
                shops.GetData(0, cell, frame, 0, frame.Length);
                town.SetData(0, cell, frame, 0, frame.Length);
            }
        }
        hero = Load("hero.png", 6, 4, 32);
        meadow = Load("meadow.png", 2, 2, 32);
        details = Load("details.png", 4, 4, 32);
        river = CreateRiver(device);
        paths = RetroArt.CreatePaths(device, terrain);
        (castShadows, buildingShade, windowGlow) = CreateBuildingLighting(device);
        pixel = new Texture2D(device, 1, 1);
        pixel.SetData([Color.White]);
        for (var y = 0; y < TownMap.Height; y++)
            for (var x = 0; x < TownMap.Width; x++)
                if (map.Tile(new(x, y)) is not (1 or 2 or 3 or 5 or 6 or 7 or 8 or 10 or 11
                    or 14 or 15 or 16 or 22 or 25 or 26 or 48 or 50 or 57 or 89))
                    throw new InvalidDataException("No artwork for this town tile.");

        Texture2D Load(string name, int columns, int rows, int cellSize) =>
            RetroArt.Load(device, content, name, columns, rows, cellSize);
    }

    public void Draw(SpriteBatch batch, PlayerMotion player, double time, bool miniature, float rainStrength = 0)
    {
        mountains.Prepare(TownMap.Id, map.Tile);
        for (var y = 0; y < TownMap.Height; y++)
            for (var x = 0; x < TownMap.Width; x++)
            {
                var effects = (x % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally)
                    | (y % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
                var variant = (x * 3 + y * 7) % 11 switch { < 4 => 0, < 7 => 1, < 9 => 2, _ => 3 };
                batch.Draw(meadow, new Rectangle(x * 32, y * 32, 32, 32), Cell(variant, 2, 32),
                    Color.White, 0, Vector2.Zero, effects, 0);
            }
        DrawRiver(batch, time);
        DrawHeroReflection(batch, player, time);
        for (var y = 0; y < TownMap.Height; y++)
            for (var x = 0; x < TownMap.Width; x++)
            {
                var tile = map.Tile(new(x, y));
                if (MountainTerrain.IsMountain(tile))
                {
                    var bounds = new Rectangle(x * 32, y * 32, 32, 32);
                    batch.Draw(mountains.Texture, bounds, bounds, Color.White);
                }
                var path = RetroArt.PathConnections(tile);
                if (path != 0) DrawPath(batch, x, y, path);
                if (tile == 11)
                {
                    batch.Draw(pixel, new Rectangle(x * 32 + 1, y * 32 + 19, 30, 6), Palette[0] * 0.25f);
                    batch.Draw(town, new Vector2(x * 32 - 8, y * 32 - 8), Cell(7, 3, 48), Color.White);
                }
                if (tile == 50) batch.Draw(town, new Vector2(x * 32 - 8, y * 32 - 16), Cell(8, 3, 48), Color.White);
            }
        Rain.DrawSplashes(batch, pixel, time, rainStrength, map.Tile, (x, y) => IsWater(new(x, y, 1, 1)));
        DrawGroundDetails(batch, player, time);
        DrawLeaves(batch, time);
        // Cast every shadow onto the ground before drawing any character or building.
        for (var y = 0; y < TownMap.Height; y++)
            for (var x = 0; x < TownMap.Width; x++)
            {
                DrawBuildingShadow(batch, map.Tile(new(x, y)), new(x, y));
            }
        var heroDrawn = false;
        for (var y = 0; y < TownMap.Height; y++)
        {
            if (!heroDrawn && player.VisualPosition.Y * 32 + 25 < y * 32 + 28)
            {
                DrawHero(batch, player, time, miniature);
                heroDrawn = true;
            }
            for (var x = 0; x < TownMap.Width; x++)
            {
                var tile = map.Tile(new(x, y));
                DrawBuilding(batch, tile, new(x, y), time, miniature);
            }
        }
        if (!heroDrawn) DrawHero(batch, player, time, miniature);
    }

    internal void DrawBuildingShadow(SpriteBatch batch, ushort tile, MapPosition cell)
    {
        var sprite = BuildingSprite(tile);
        if (sprite is < 0 or 6) return;
        batch.Draw(castShadows, new Vector2(cell.X * 32 - 8, cell.Y * 32 + 28),
            new Rectangle(sprite * 64, 0, 64, 16), Color.White);
    }

    internal void DrawBuilding(SpriteBatch batch, ushort tile, MapPosition cell, double time, bool miniature)
    {
        var sprite = BuildingSprite(tile);
        if (sprite < 0) return;
        var x = cell.X; var y = cell.Y;
        // Share Maynard's signs, facade shading, window light and smoke with other towns.
        if (sprite < 5) DrawShopDetails(batch, x, y, sprite, time);
        if (sprite != 6)
        {
            batch.Draw(pixel, new Rectangle(x * 32 + 2, y * 32 + 27, 29, 3), Palette[8] * 0.7f);
            if (miniature)
                batch.Draw(pixel, new Rectangle(x * 32 + 3, y * 32 + 28, 28, 2), Palette[0] * 0.6f);
        }
        var position = new Vector2(x * 32 - 8, y * 32 - (sprite == 6 ? 12 : 16));
        batch.Draw(town, position, Cell(sprite, 3, 48), Color.White);
        batch.Draw(buildingShade, position, Cell(sprite, 3, 48), Color.White);
        var glow = 0.8f + 0.2f * MathF.Sin((float)time * 1.2f + x);
        batch.Draw(windowGlow, position, Cell(sprite, 3, 48), Color.White * glow);
        if (tile == 6) DrawSmoke(batch, x * 32 + 29, y * 32 - 10, time);
        if (tile == 10 && (int)(time * 6) % 4 == 0)
            batch.Draw(pixel, new Rectangle(x * 32 + 25, y * 32 + 14, 2, 2), Palette[7]);
    }

    private void DrawPath(SpriteBatch batch, int x, int y, int connections)
    {
        var variant = x % 2 + y % 2 * 2;
        batch.Draw(paths, new Vector2(x * 32, y * 32), Cell(connections + variant * 16, 4, 32), Color.White);
    }

    internal void DrawHero(SpriteBatch batch, PlayerMotion player, double time, bool miniature, bool footsteps = true)
    {
        var feet = new Vector2(MathF.Round(player.VisualPosition.X * 32) + 16, MathF.Round(player.VisualPosition.Y * 32) + 25);
        if (footsteps) DrawFootsteps(batch, player, feet);
        Sunlight?.DrawDust(batch, pixel, time);
        batch.Draw(pixel, new Rectangle((int)feet.X - 5, (int)feet.Y - 1, 10, 2), Palette[8]);
        if (miniature)
            batch.Draw(pixel, new Rectangle((int)feet.X - 4, (int)feet.Y, 8, 1), Palette[0] * 0.6f);
        var source = HeroFrame(player, time);
        var position = feet - new Vector2(16, 28);
        var remaining = (new Vector2(player.Position.X, player.Position.Y) - player.VisualPosition) * 32;
        var distance = remaining.Length();
        if (player.IsMoving && distance > 0)
        {
            // Sample only the current accepted step: never trail through a turn or a teleport.
            var length = Math.Clamp(32 - distance, 0, 3);
            var fade = MathHelper.SmoothStep(0, 1, Math.Clamp(distance / 8, 0, 1));
            for (var sample = 3; sample >= 1; sample--)
            {
                var trail = position - remaining / distance * (length * sample / 3);
                trail = new Vector2(MathF.Round(trail.X), MathF.Round(trail.Y));
                if (trail == position) continue;
                batch.Draw(HeroTexture, trail, source, Color.White * (0.035f * (4 - sample) * fade));
            }
        }
        // The current pose stays fully opaque and crisp over the short exposure trail.
        batch.Draw(HeroTexture, position, source, Color.White);
        Sunlight?.DrawHero(batch, HeroTexture, source, position, pixel, time);
    }

    internal FootstepSurface FootstepAt(MapPosition position)
    {
        var tile = map.Tile(position);
        if (tile == 0x0b) return FootstepSurface.Wood;
        if (IsWater(new(position.X * 32 + 16, position.Y * 32 + 25, 1, 1))) return FootstepSurface.Water;
        return RetroArt.PathConnections(tile) != 0 ? FootstepSurface.Stone : FootstepSurface.Grass;
    }

    private Rectangle HeroFrame(PlayerMotion player, double time)
    {
        if (AlternateHero is { } alternate)
            return new(player.FacingRow * 32, (alternate.Row + (player.IsMoving ? player.AnimationFrame % 2 : 0)) * 32, 32, 32);
        var frame = player.IsMoving ? player.AnimationFrame : (int)(time * 8) % 32 == 31 ? 5 : 4;
        return Cell(player.FacingRow * 6 + frame, 6, 32);
    }

    public void DrawCombatHero(SpriteBatch batch, Rectangle bounds, Color tint)
    {
        var source = AlternateHero is { } alternate ? new Rectangle(0, alternate.Row * 32, 32, 32) : Cell(4, 6, 32);
        batch.Draw(HeroTexture, bounds, source, tint);
    }

    private void DrawSmoke(SpriteBatch batch, int chimneyX, int chimneyY, double time)
    {
        var tick = (int)(time * 6);
        for (var i = 0; i < 3; i++)
        {
            var age = (tick + i * 4) % 12;
            var size = 2 + age / 5;
            var x = chimneyX - age / 4 + (age / 3 % 2);
            var y = chimneyY - age;
            var color = Palette[age < 4 ? 4 : age < 8 ? 3 : 2];
            batch.Draw(pixel, new Rectangle(x, y, size, size), color);
            batch.Draw(pixel, new Rectangle(x - 1, y + 1, size + 2, Math.Max(1, size - 2)), color);
        }
    }

    private static Rectangle Cell(int index, int columns, int size) => new(index % columns * size, index / columns * size, size, size);

    public void Dispose()
    {
        terrain.Dispose();
        mountains.Dispose();
        town.Dispose();
        hero.Dispose();
        meadow.Dispose();
        details.Dispose();
        river.Dispose();
        paths.Dispose();
        castShadows.Dispose();
        buildingShade.Dispose();
        windowGlow.Dispose();
        pixel.Dispose();
    }
}
