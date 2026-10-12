using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const double RaftTravelSeconds = .9;
    private Map? raftMap;
    private MapPosition raftTile;
    private Vector2 raftOrigin;
    private double raftElapsed = RaftTravelSeconds;

    // Core still moves the raft every third exploration tick. Interpolate only
    // that accepted move, never predict a route or consume gameplay randomness.
    internal bool SynchronizeRaft(Adventure game, MapPosition rider, bool animate)
    {
        var map = game.Map;
        var raft = map.Events.Overlay;
        if (!IsWaterCave(map) || raft.Value <= 0) { raftMap = null; return false; }
        var same = ReferenceEquals(raftMap, map);
        var adjacent = Math.Abs(raft.Position.X - raftTile.X) <= 1 && Math.Abs(raft.Position.Y - raftTile.Y) <= 1;
        var carried = same && animate && adjacent && raft.Position != raftTile
            && rider == raftTile && game.Location.Position == raft.Position;
        if (!same || !animate || !adjacent)
        {
            raftOrigin = new(raft.Position.X, raft.Position.Y);
            raftElapsed = RaftTravelSeconds;
        }
        else if (raft.Position != raftTile)
        {
            raftOrigin = RaftPosition(map, 0); // No bob at time zero.
            raftElapsed = 0;
        }
        raftMap = map; raftTile = raft.Position;
        return carried;
    }

    internal void UpdateRaft(double elapsed) => raftElapsed = Math.Min(RaftTravelSeconds, raftElapsed + elapsed);

    private Vector2 RaftPosition(Map map, double time)
    {
        var tile = map.Events.Overlay.Position;
        var position = new Vector2(tile.X, tile.Y);
        if (ReferenceEquals(raftMap, map))
        {
            var progress = (float)(raftElapsed / RaftTravelSeconds);
            position = Vector2.Lerp(raftOrigin, new(raftTile.X, raftTile.Y), MathHelper.SmoothStep(0, 1, progress));
        }
        // Both boots and deck use this single one-pixel buoyancy offset.
        position.Y += (int)(Math.Sin(Math.Floor(time * 8) / 8 * 1.8) * 1.2) / 32f;
        return position;
    }

    internal Vector2? RaftSupport(Adventure game, double time) => IsWaterCave(game.Map)
        && game.Map.Events.Overlay is { Value: > 0 } raft && raft.Position == game.Location.Position
        ? RaftPosition(game.Map, time) : null;

    private void DrawRaft(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsWaterCave(game.Map) || game.Map.Events.Overlay is not { Value: > 0 } raft
            || !game.Visibility.Contains(raft.Position)) return;
        var position = RaftPosition(game.Map, time);
        var feet = new Point((int)MathF.Round(position.X * 32) + 16, (int)MathF.Round(position.Y * 32) + 25);
        // Small broken ripples make its buoyancy readable without a smooth halo.
        for (var side = -1; side <= 1; side += 2)
            for (var step = 0; step < 7; step++)
            {
                var phase = (int)(time * 6) % 3;
                var x = feet.X + side * (13 + phase) + step / 3;
                var y = feet.Y - 3 + step;
                if (x < 0 || y < 0 || x >= WaterWidth || y >= WaterHeight
                    || !game.Visibility.Contains(new(x / 32, y / 32)) || !WaterAt(x, y) || (step + phase) % 3 == 0) continue;
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), WaterCavePalette[21] * .4f);
            }
        // The visible log deck is centered at (24,23) in its padded 48px cell.
        // Generic prop bottom-alignment placed the rider 19 pixels below it.
        batch.Draw(props, new Rectangle(feet.X - 24, feet.Y - 23, 48, 48), Cell(12, 4, 48), Color.White);
    }
}
