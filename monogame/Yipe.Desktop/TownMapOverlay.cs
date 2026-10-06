using Microsoft.Xna.Framework;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private void OpenMap()
    {
        if (command is not null || textReceiver is not null) return;
        journal.Observe(adventure);
        mapLayer = journal.Find(session.CurrentMap.Id)!.Layer;
        worldMapOpen = false; mapOpen = true; pendingDirection = Point.Zero;
        tickRemaining = .512;
    }

    private void UpdateMap(InputFrame actions)
    {
        if (actions.Map || actions.Back)
        { mapOpen = false; pendingDirection = Point.Zero; tickRemaining = .512; return; }
        if (actions.Pause) { paused = true; selected = 0; selectionY = 0; return; }
        if (actions.Confirm) { worldMapOpen = !worldMapOpen; return; }
        if (worldMapOpen && actions.Direction.X != 0)
        {
            var layers = journal.Regions.Select(r => r.Layer).Distinct().Order().ToArray();
            var selectedLayer = Array.IndexOf(layers, mapLayer);
            mapLayer = layers[(selectedLayer + actions.Direction.X + layers.Length) % layers.Length];
        }
    }

    private void RenderAreaMap()
    {
        GraphicsDevice.SetRenderTarget(mapView);
        GraphicsDevice.Clear(Background);
        batch.Begin(samplerState: Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp);
        if (session.CurrentMap.Id == TownMap.Id) town.Draw(batch, player, sceneTime, miniature: false);
        else world.DrawMap(batch, adventure.Map, journal.Find(adventure.Map.Id)!.Seen, sceneTime);
        batch.End();
    }

    private void DrawMapOverlay()
    {
        Fill(new(0, 0, CanvasWidth, CanvasHeight), Background);
        DrawPanel(new(4, 4, 312, 172));
        var current = journal.Find(adventure.Map.Id)!;
        font.Draw(batch, worldMapOpen ? "WORLD MAP" : "AREA MAP", 12, 11, Accent);
        font.Draw(batch, worldMapOpen ? LayerName(mapLayer) : current.Name, 112, 11, Ink);
        if (worldMapOpen) DrawWorldMap(current);
        else DrawAreaMap(current);
        Fill(new(12, 162, 296, 1), Muted * .35f);
        font.Draw(batch, controllerConnected
            ? $"SOUTH: {(worldMapOpen ? "AREA" : "WORLD")}   VIEW/EAST: CLOSE"
            : $"ENTER: {(worldMapOpen ? "AREA" : "WORLD")}   M/ESC: CLOSE", 12, 166, Muted);
    }

    private void DrawAreaMap(MappedRegion current)
    {
        var bounds = new Rectangle(12, 24, 143, 132); // Eleven native UI pixels per original tile.
        Fill(new(bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height + 2), Muted);
        batch.Draw(mapView, bounds, Color.White);
        for (var x = 0; x < Map.Width; x++)
            for (var y = 0; y < Map.Height; y++)
            {
                var i = x * Map.Height + y;
                if (!current.Seen[i])
                {
                    Fill(new(bounds.X + x * 11, bounds.Y + y * 11, 11, 11), Background);
                    Fill(new(bounds.X + x * 11 + 5, bounds.Y + y * 11 + 5, 1, 1), Muted * .25f);
                    continue;
                }
                if (current.Marks[i] == MapMark.None) continue;
                MapIcon(bounds.X + x * 11 + 5, bounds.Y + y * 11 + 5, current.Marks[i]);
            }
        var px = bounds.X + (int)MathF.Round((player.VisualPosition.X + .5f) * 11);
        var py = bounds.Y + (int)MathF.Round((player.VisualPosition.Y + .5f) * 11);
        PlayerMapMarker(px, py, player.FacingRow);
        font.Draw(batch, "YOU ARE HERE", 170, 28, Accent);
        font.Draw(batch, $"{current.Coordinate} / {session.Location.Position.X},{session.Location.Position.Y}", 170, 40, Ink);
        font.Draw(batch, "KNOWN PLACES", 170, 60, Muted);
        MapLegend(170, 75, MapMark.Shop, "Shop / service");
        MapLegend(170, 89, MapMark.Entrance, "Cave entrance");
        MapLegend(170, 103, MapMark.Stairs, "Up / down");
        MapLegend(170, 117, MapMark.Bridge, "Bridge");
        font.Draw(batch, $"Seen {current.Seen.Count(s => s)}/{Map.TileCount}", 170, 137, Muted);
        font.Draw(batch, "Dark = unexplored", 170, 149, Muted);
    }

    private void DrawWorldMap(MappedRegion current)
    {
        const int left = 18, top = 35, stepX = 27, stepY = 20;
        Point Center(MappedRegion r) => new(left + r.Cell % 6 * stepX + 11, top + r.Cell / 6 * stepY + 8);
        for (var i = 0; i < 6; i++)
        {
            font.Draw(batch, ((char)('A' + i)).ToString(), left + i * stepX + 9, 25, Muted);
            font.Draw(batch, (i + 1).ToString(), 9, top + i * stepY + 5, Muted);
        }
        // Only crossings actually made by this adventure become links. Wraps get edge ticks.
        foreach (var link in journal.Links)
        {
            var from = journal.Find(link.From)!; var to = journal.Find(link.To)!;
            if (from.Layer != mapLayer || to.Layer != mapLayer) continue;
            var a = Center(from); var b = Center(to);
            var dx = Math.Abs(from.Cell % 6 - to.Cell % 6); var dy = Math.Abs(from.Cell / 6 - to.Cell / 6);
            if (dx + dy == 1) MapLine(a, b, Accent * .75f);
            else if (dx == 5 && dy == 0)
            { MapLine(new(left - 3, a.Y), new(left + 2, a.Y), Accent); MapLine(new(left + 6 * stepX - 5, a.Y), new(left + 6 * stepX, a.Y), Accent); }
            else if (dy == 5 && dx == 0)
            { MapLine(new(a.X, top - 3), new(a.X, top + 2), Accent); MapLine(new(a.X, top + 6 * stepY - 5), new(a.X, top + 6 * stepY), Accent); }
        }
        for (var cell = 0; cell < 36; cell++)
        {
            var id = cell + 1 + (mapLayer == 3 ? 98 : mapLayer * 40);
            if (mapLayer == 3 && id < 117 || !catalog.Contains(id)) continue;
            var bounds = new Rectangle(left + cell % 6 * stepX, top + cell / 6 * stepY, 23, 16);
            var region = journal.Regions.Find(r => r.Layer == mapLayer && r.Cell == cell);
            Fill(bounds, region?.Id == current.Id ? Accent : Panel);
            bounds.Inflate(-1, -1); Fill(bounds, Background);
            if (region is null) { font.Draw(batch, "?", bounds.X + 8, bounds.Y + 3, Muted * .4f); continue; }
            for (var x = 0; x < Map.Width; x++)
                for (var y = 0; y < Map.Height; y++)
                {
                    var index = x * Map.Height + y;
                    if (region.Seen[index]) Fill(new(bounds.X + 4 + x, bounds.Y + 1 + y, 1, 1), MapTileColor(region.Tiles[index], region.Layer));
                }
            if (region.Id == current.Id) PlayerMapMarker(bounds.Center.X, bounds.Center.Y, player.FacingRow);
            else if (region.Id is 8 or 17 or 27) MapIcon(bounds.Center.X, bounds.Center.Y, MapMark.Shop);
        }
        font.Draw(batch, "YOU ARE HERE", 192, 29, Accent);
        font.Draw(batch, current.Name, 192, 41, Ink);
        font.Draw(batch, LayerName(current.Layer), 192, 53, Muted);
        font.Draw(batch, $"{journal.Regions.Count} areas visited", 192, 70, Ink);
        font.Draw(batch, "KNOWN LINKS", 192, 88, Muted);
        var neighbors = journal.Links.Where(l => l.From == current.Id || l.To == current.Id)
            .Select(l => journal.Find(l.From == current.Id ? l.To : l.From)!).ToArray();
        for (var i = 0; i < Math.Min(4, neighbors.Length); i++)
            font.Draw(batch, neighbors[i].Name, 192, 100 + i * 10, Ink);
        if (neighbors.Length == 0) font.Draw(batch, "Explore to chart", 192, 100, Muted);
        if (neighbors.Length > 4) font.Draw(batch, $"+{neighbors.Length - 4} more routes", 192, 140, Muted);
        font.Draw(batch, "LEFT/RIGHT: LAYER", 192, 152, Muted);
    }

    private static string LayerName(int layer) => layer switch { 0 => "Surface", 1 => "Upper caves", 2 => "Deep caves", _ => "Yipe Hell" };

    private static Color MapTileColor(ushort tile, int layer) => RetroArt.Palette[tile switch
    {
        11 or >= 0x0c and <= 0x15 or >= 0x4a and <= 0x51 or 0x58 or 0x59 => 28,
        3 or 0x39 or 0x45 or 9 or 0x22 or 0x23 or 0x25 or 0x43 or 0x44 or 0x5a => 3,
        8 or >= 0x16 and <= 0x1a => 18,
        >= 0x2c and <= 0x2f or 0x36 or 0x42 or 0x57 => 17,
        _ => layer == 0 ? 10 : 15,
    }];

    private void MapLegend(int x, int y, MapMark mark, string label)
    { MapIcon(x + 3, y + 3, mark); font.Draw(batch, label, x + 12, y, Ink); }

    private void MapIcon(int x, int y, MapMark mark)
    {
        Fill(new(x - 4, y - 4, 9, 9), Background);
        var glyph = mark switch { MapMark.Shop => "S", MapMark.Entrance => "C", MapMark.Stairs => ">", _ => "=" };
        font.Draw(batch, glyph, x - 2, y - 3, new Color(145, 199, 188));
    }

    private void PlayerMapMarker(int x, int y, int facing)
    {
        Fill(new(x - 4, y - 4, 9, 9), Background);
        Fill(new(x - 2, y - 2, 5, 5), Accent);
        var direction = facing switch { 0 => new Point(0, 1), 1 => new(-1, 0), 2 => new(1, 0), _ => new(0, -1) };
        Fill(new(x + direction.X * 4, y + direction.Y * 4, 1, 1), Ink);
    }

    private void MapLine(Point from, Point to, Color color)
    {
        Fill(new(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Abs(to.X - from.X) + 1, Math.Abs(to.Y - from.Y) + 1), color);
    }
}
