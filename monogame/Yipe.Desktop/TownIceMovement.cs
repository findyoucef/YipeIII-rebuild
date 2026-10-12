using Microsoft.Xna.Framework;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private void MovePlayer(Point requested, Point held)
    {
        var map = session.CurrentMap;
        var from = player.Position;
        var icy = WorldRenderer.IsIce(map) && map.Tile(from) is 0x53 or 0x49;
        var slippery = icy && IsOpenIce(map, from);
        var direction = requested;
        nextStride = PlayerStride.Walk;
        if (!slippery) iceDirection = Point.Zero;
        if (icy && iceDirection != Point.Zero && held != iceDirection)
        {
            // One decelerating tile on open ice. Traction around narrow passages
            // and exits lets the player line up, stop and turn without overshooting.
            direction = iceDirection;
            iceDirection = Point.Zero;
            var target = new MapPosition(from.X + direction.X, from.Y + direction.Y);
            if (IsOpenIce(map, target))
                nextStride = PlayerStride.IceBrake;
            else direction = held == Point.Zero ? Point.Zero : requested;
        }
        else if (icy && iceDirection != Point.Zero) direction = held;
        else if (icy && held == Point.Zero) direction = Point.Zero;

        if (direction == Point.Zero) return;
        var destination = new MapPosition(from.X + direction.X, from.Y + direction.Y);
        if (slippery && nextStride != PlayerStride.IceBrake && IsOpenIce(map, destination))
            nextStride = iceDirection == Point.Zero ? PlayerStride.IcePush : PlayerStride.IceGlide;
        player.Face(direction);
        try
        {
            StartCommand(adventure.Move(direction.X, direction.Y, DateTime.Now.Hour));
            walkingStep = player.IsMoving && !player.IsSliding;
            iceDirection = icy && player.IsMoving && command is null && ReferenceEquals(map, session.CurrentMap)
                && map.Tile(player.Position) is 0x53 or 0x49 && nextStride is PlayerStride.IcePush or PlayerStride.IceGlide
                ? direction : Point.Zero;
        }
        finally { nextStride = PlayerStride.Walk; }
    }

    private static bool IsOpenIce(Map map, MapPosition cell)
    {
        // A one-tile margin around visible obstacles and travel points has normal
        // footing, including opened doors whose terrain has become ordinary ice.
        if (cell.X <= 1 || cell.X >= Map.Width - 2 || cell.Y <= 1 || cell.Y >= Map.Height - 2) return false;
        for (var y = cell.Y - 1; y <= cell.Y + 1; y++)
            for (var x = cell.X - 1; x <= cell.X + 1; x++)
                if (map.Tile(new(x, y)) is not (0x53 or 0x49) || map.DepthActionAt(new(x, y)) != 0) return false;
        return true;
    }
}
