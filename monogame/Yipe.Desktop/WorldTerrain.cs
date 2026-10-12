using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    // Tile identities follow app/main.cpp::tile_graphic. Secret variants deliberately
    // share their ordinary terrain: scenery must not reveal hidden actions or passages.
    private static bool HasArtwork(ushort tile) => tile is >= 1 and <= 0x36
        or 0x39 or >= 0x3c and <= 0x5a or >= 0x5d and <= 0x5f or 0x61 or 0x62
        or >= 0x65 and <= 0x69 or 0x71;

    private void DrawGround(SpriteBatch batch, Map map, MapPosition cell, double time)
    {
        var tile = map.Tile(cell);
        if (!HasArtwork(tile)) throw new InvalidDataException($"No world artwork for tile {tile:x} in map {map.Id}.");
        var bounds = new Rectangle(cell.X * 32, cell.Y * 32, 32, 32);
        if (IsHell(map)) { DrawHellGround(batch, bounds, time); return; }
        if (IsFire(map)) { DrawFireGround(batch, bounds, time); return; }
        if (IsBreeding(map)) { batch.Draw(breedingGround, bounds, bounds, Color.White); return; }
        if (IsIce(map)) { batch.Draw(iceGround, bounds, bounds, Color.White); return; }
        if (IsWaterCave(map))
        {
            batch.Draw(waterCaveGround, bounds, bounds, Color.White);
            DrawWater(batch, bounds, time);
            return;
        }
        if (IsFinal(map))
        {
            batch.Draw(finalGround, bounds, bounds, Color.White);
            DrawWater(batch, bounds, time);
            // Authored instability remains legible above decorative floor wear.
            if (tile is 0x29 or 0x2a)
                for (var y = 4; y < 29; y++)
                {
                    var x = bounds.X + 15 + y / 4 % 3 - 1;
                    batch.Draw(pixel, new Rectangle(x, bounds.Y + y, 2, 1), FinalPalette[0]);
                    batch.Draw(pixel, new Rectangle(x + 2, bounds.Y + y, 1, 1), FinalPalette[8]);
                }
            if (tile == 0x2b)
            {
                batch.Draw(pixel, new Rectangle(bounds.X + 6, bounds.Y + 8, 22, 20), FinalPalette[6]);
                batch.Draw(pixel, new Rectangle(bounds.X + 8, bounds.Y + 9, 19, 17), FinalPalette[0]);
            }
            if (tile == 0x71) DrawProp(batch, 12, cell);
            return;
        }
        if (IsStone(map))
        {
            batch.Draw(stoneGround, bounds, bounds, Color.White);
            DrawWater(batch, bounds, time);
            return;
        }
        if (IsEarth(map))
        {
            batch.Draw(earthGround, bounds, bounds, Color.White);
            DrawWater(batch, bounds, time);
            return;
        }
        var surface = map.Id < 41;
        var floor = (Math.Clamp(map.Environment, 1, 7) - 1) * 2;
        if (surface && map.Id <= 36 && map.Id != 17) batch.Draw(outdoorGround, bounds, bounds, Color.White);
        else batch.Draw(surface ? meadow : floors, bounds,
            surface ? Cell((cell.X * 3 + cell.Y * 7) % 4, 2, 32) : Cell(floor, 4, 32), Color.White);

        if (desertActive && !(map.Id <= 36 && map.Id != 17)) batch.Draw(sandLayer, bounds, bounds, Color.White);
        // Gravel sits below the curved snowfield; cliff faces stay above it.
        if (MountainTerrain.IsMountain(tile)) batch.Draw(mountains.Scree, bounds, bounds, Color.White);
        if (snowActive) batch.Draw(snowGround, bounds, bounds, Color.White);
        if (MountainTerrain.IsMountain(tile)) batch.Draw(mountains.Texture, bounds, bounds, Color.White);
        if (tile == 0x36) batch.Draw(desert, bounds, Cell(2, 2, 32), Color.White);
        if (tile is 0x22 or 0x23 or 0x25 or 0x43 or 0x44 or 0x5a)
        {
            batch.Draw(floors, bounds, Cell(floor + 1, 4, 32), Color.White);
            if (cell.Y < Map.Height - 1 && map.Tile(new(cell.X, cell.Y + 1)) is not (0x22 or 0x23 or 0x25 or 0x43 or 0x44 or 0x5a))
                batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y + 29, 32, 3), RetroArt.Palette[0] * .45f);
        }
        if (!snowActive && tile is (0x1f or 0x20 or 0x69) || surface && tile is (>= 0x27 and <= 0x2b or 0x47 or 0x48 or 0x52 or 0x54 or 0x55 or 0x5d or 0x5e or 0x61))
            batch.Draw(floors, bounds, Cell(14, 4, 32), Color.White);
        if (tile is 0x29 or 0x2a) DrawCrack(batch, bounds);
        if (tile == 0x2b)
        {
            batch.Draw(pixel, new Rectangle(bounds.X + 6, bounds.Y + 8, 22, 20), RetroArt.Palette[2]);
            batch.Draw(pixel, new Rectangle(bounds.X + 8, bounds.Y + 9, 19, 17), RetroArt.Palette[0]);
        }
        DrawWater(batch, bounds, time);
        var connections = VillagePathConnections(map, cell);
        if (connections != 0)
        {
            var variant = cell.X % 2 + cell.Y % 2 * 2;
            batch.Draw(paths, bounds, Cell(connections + variant * 16, 4, 32),
                map.Id == 8 ? new Color(245, 224, 188) : Color.White);
        }
        if (tile is 0x3c or 0x3d) DrawSpikes(batch, bounds, tile == 0x3d);
        if (tile is >= 0x65 and <= 0x68) DrawMountainFoot(batch, map, bounds, tile);
        if (tile == 0x71) DrawProp(batch, 12, cell);
        var floraIndex = tile switch
        {
            0x2c => 8, 0x2f => 2, 0x32 => 3, 0x33 => 9, 0x34 => 7, 0x35 => 6,
            _ => -1,
        };
        if (floraIndex >= 0) DrawFlora(batch, floraIndex, cell, time, surface);
    }

    private void DrawFlora(SpriteBatch batch, int index, MapPosition cell, double time, bool surface)
    {
        var source = Cell(index, 4, 48);
        var position = new Vector2(cell.X * 32 - 8, cell.Y * 32 - 15);
        if (index is not (0 or 2) && !(surface && index is 5 or 10 or 11)) { batch.Draw(flora, position, source, Color.White); return; }
        var breeze = MathF.Sin((float)(Math.Floor(time * 6) / 6) * 1.8f + cell.X * .7f + cell.Y);
        // Crown/branch movement is at most one pixel; trunks and roots remain anchored.
        for (var row = 0; row < 48; row += 4)
        {
            var sway = (int)MathF.Round(breeze * Math.Max(0, 1 - row / 32f));
            batch.Draw(flora, position + new Vector2(sway, row), new Rectangle(source.X, source.Y + row, 48, 4), Color.White);
        }
    }
    private void DrawCrack(SpriteBatch batch, Rectangle bounds)
    {
        for (var y = 4; y < 29; y++)
            batch.Draw(pixel, new Rectangle(bounds.X + 15 + (y / 4 % 3) - 1, bounds.Y + y, 1, 1), RetroArt.Palette[2]);
    }
    private void DrawMountainFoot(SpriteBatch batch, Map map, Rectangle bounds, ushort tile)
    {
        // Original bitmap 460: rock on the right, bottom, left, top respectively.
        // These are walkable mountain skirts, not directional markers. Extend the
        // neighboring mountain texture into sand, keeping most of the tile open.
        var vertical = tile is 0x65 or 0x67;
        var farEdge = tile is 0x65 or 0x66;
        var neighbor = new MapPosition(bounds.X / 32 + (vertical ? farEdge ? 1 : -1 : 0),
            bounds.Y / 32 + (vertical ? 0 : farEdge ? 1 : -1));
        var connected = Map.Contains(neighbor) && MountainTerrain.IsMountain(map.Tile(neighbor));
        for (var along = 0; along < 32; along++)
        {
            var phase = along * MathF.Tau / 32;
            var depth = 5 + (int)MathF.Round(2 * MathF.Sin(phase) + MathF.Sin(phase * 3));
            var offset = farEdge ? 32 - depth : 0;
            var toe = farEdge ? offset - 2 : depth;
            batch.Draw(pixel, vertical
                ? new Rectangle(bounds.X + toe, bounds.Y + along, 2, 1)
                : new Rectangle(bounds.X + along, bounds.Y + toe, 1, 2), RetroArt.Palette[16] * .3f);
            var destination = vertical
                ? new Rectangle(bounds.X + offset, bounds.Y + along, depth, 1)
                : new Rectangle(bounds.X + along, bounds.Y + offset, 1, depth);
            var source = vertical
                ? new Rectangle(farEdge ? 32 : 64 - depth, 32 + along, depth, 1)
                : new Rectangle(32 + along, farEdge ? 32 : 64 - depth, 1, depth);
            // Carry the actual neighboring cliff colors into its authored skirt.
            // Isolated skirt tiles retain the original small-stone texture.
            if (connected) source = vertical
                ? new Rectangle(neighbor.X * 32 + (farEdge ? 0 : 32 - depth), neighbor.Y * 32 + along, depth, 1)
                : new Rectangle(neighbor.X * 32 + along, neighbor.Y * 32 + (farEdge ? 0 : 32 - depth), 1, depth);
            if (connected) batch.Draw(mountains.Scree, destination, source, Color.White, 0, Vector2.Zero,
                vertical ? SpriteEffects.FlipHorizontally : SpriteEffects.FlipVertically, 0);
            batch.Draw(connected ? mountains.Texture : outdoors, destination, source, Color.White, 0, Vector2.Zero,
                vertical ? SpriteEffects.FlipHorizontally : SpriteEffects.FlipVertically, 0);
        }
        // A few loose stones soften the transition without suggesting a blocked cell.
        var variation = bounds.X / 32 + bounds.Y / 32 * 3;
        for (var stone = 0; stone < 3; stone++)
        {
            var along = 5 + stone * 9 + variation % 3;
            var distance = 11 + (stone * 5 + variation) % 8;
            var across = farEdge ? 31 - distance : distance;
            var x = bounds.X + (vertical ? across : along);
            var y = bounds.Y + (vertical ? along : across);
            batch.Draw(pixel, new Rectangle(x - 1, y + 2, 4, 1), RetroArt.Palette[16] * .4f);
            batch.Draw(pixel, new Rectangle(x, y, 3, 2), RetroArt.Palette[2]);
            batch.Draw(pixel, new Rectangle(x, y, 2, 1), RetroArt.Palette[4]);
        }
    }
    private void DrawCasinoSign(SpriteBatch batch, MapPosition cell)
    {
        batch.Draw(pixel, new Rectangle(cell.X * 32 + 18, cell.Y * 32 + 13, 9, 9), RetroArt.Palette[6]);
        foreach (var p in new[] { new Point(20, 15), new(24, 15), new(22, 17), new(20, 19), new(24, 19) })
            batch.Draw(pixel, new Rectangle(cell.X * 32 + p.X, cell.Y * 32 + p.Y, 1, 1), RetroArt.Palette[14]);
    }
}
