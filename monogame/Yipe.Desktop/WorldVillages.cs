using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    // Presentation paths connect the existing approaches; collision and encounters
    // continue to use the original map. Quizilog's palms keep their blocked cells.
    private static readonly Point[] QuizilogLanes =
    [
        new(3, 6), new(4, 6), new(5, 6), new(6, 6), new(7, 6), new(8, 6),
        new(9, 6), new(10, 6), new(11, 6), new(9, 4), new(8, 4), new(9, 5),
        new(9, 7), new(7, 7), new(7, 8), new(6, 8),
    ];
    private static readonly Point[] ErvynLanes =
    [
        new(3, 5), new(4, 5), new(5, 5), new(6, 5), new(7, 5), new(9, 5), new(10, 5),
        new(3, 6), new(9, 6), new(3, 7), new(4, 7), new(5, 7), new(6, 7), new(7, 7),
        new(8, 7), new(9, 7),
    ];
    private static readonly (int Sprite, int X, int Y)[] QuizilogDetails =
    [
        (2, 172, 178), (3, 148, 182), (3, 227, 182), (2, 346, 218),
        (6, 196, 289), (7, 197, 267), (15, 279, 294), (14, 259, 157),
        (15, 217, 156), (6, 324, 290), (15, 294, 180),
        (5, 267, 105), (3, 204, 116), (2, 366, 151), (3, 377, 258),
        (3, 256, 327), (2, 288, 343), (5, 193, 333), (3, 151, 325),
        (15, 297, 260), (2, 139, 247), (3, 359, 322),
    ];
    private static readonly (int Sprite, int X, int Y)[] ErvynDetails =
    [
        (0, 118, 110), (1, 141, 107), (5, 98, 125), (3, 157, 119),
        (1, 265, 107), (0, 284, 110), (5, 306, 127), (3, 247, 120),
        (0, 188, 214), (1, 212, 211), (0, 231, 216), (5, 199, 202),
        (15, 121, 153), (7, 166, 156), (14, 294, 157),
        (6, 120, 218), (15, 247, 217), (15, 295, 216),
        (0, 119, 275), (1, 145, 278), (5, 97, 287), (3, 162, 288),
        (1, 268, 280), (0, 286, 273), (5, 309, 289),
        (2, 70, 203), (3, 337, 220), (0, 337, 154), (1, 349, 144),
        (5, 86, 79), (5, 327, 82), (0, 111, 83), (1, 304, 85),
        (3, 206, 303), (2, 227, 299),
    ];

    private static bool IsVillage(Map map) => map.Id is 8 or 27;

    internal static int VillagePathConnections(Map map, MapPosition cell)
    {
        var original = RetroArt.PathConnections(map.Tile(cell));
        if (!IsVillage(map)) return original;
        var lanes = map.Id == 8 ? QuizilogLanes : ErvynLanes;
        if (!OnPath(cell.X, cell.Y)) return 0;
        var connections = (OnPath(cell.X, cell.Y - 1) ? 1 : 0) | (OnPath(cell.X + 1, cell.Y) ? 2 : 0)
            | (OnPath(cell.X, cell.Y + 1) ? 4 : 0) | (OnPath(cell.X - 1, cell.Y) ? 8 : 0);
        // Preserve authored road exits into neighboring regions.
        if (cell.X == 0) connections |= original & 8;
        if (cell.X == Map.Width - 1) connections |= original & 2;
        if (cell.Y == 0) connections |= original & 1;
        if (cell.Y == Map.Height - 1) connections |= original & 4;
        return connections;

        bool OnPath(int x, int y)
        {
            if (!Map.Contains(new(x, y))) return false;
            var tile = map.Tile(new(x, y));
            return RetroArt.PathConnections(tile) != 0
                || (tile is 2 or 0x2d or >= 0x1b and <= 0x1e) && lanes.Contains(new(x, y));
        }
    }

    private void DrawVillageDetails(SpriteBatch batch, Map map, MapPosition cell, TownRenderer town, double time)
    {
        if (!IsVillage(map)) return;
        foreach (var detail in map.Id == 8 ? QuizilogDetails : ErvynDetails)
            if (detail.X / 32 == cell.X && detail.Y / 32 == cell.Y)
                town.DrawProp(batch, detail.Sprite, new(detail.X, detail.Y), time);
        town.DrawBuildingShadow(batch, map.Tile(cell), cell);
    }

    private void DrawVillageActivity(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsVillage(game.Map)) return;
        var clock = Math.Floor(time * 8) / 8;
        for (var i = 0; i < 4; i++)
        {
            var x = (int)(game.Map.Id == 27 ? 115 + i * 53 + Math.Sin(clock * .6 + i) * 12
                : 167 + i * 47 + Math.Sin(clock * .45 + i) * 18);
            var y = (int)(game.Map.Id == 27 ? (i % 2 == 0 ? 110 : 276) + Math.Cos(clock * .8 + i) * 6
                : 170 + i * 38 + Math.Cos(clock * .55 + i) * 8);
            if (!game.Visibility.Contains(new(x / 32, y / 32))) continue;
            if (game.Map.Id == 27)
            {
                // Tiny butterflies over the flower beds; no simulation or encounter state.
                var wing = (int)(clock * 8 + i) % 3 == 0 ? 1 : 2;
                batch.Draw(pixel, new Rectangle(x - wing, y, wing * 2 + 1, 1), RetroArt.Palette[i % 2 == 0 ? 6 : 33]);
                batch.Draw(pixel, new Rectangle(x, y + 1, 1, 1), RetroArt.Palette[15]);
            }
            else
                batch.Draw(pixel, new Rectangle(x, y, 2, 1), RetroArt.Palette[18] * .6f);
        }
    }
}
