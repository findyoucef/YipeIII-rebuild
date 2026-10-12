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
        var direction = requested;
        nextStride = PlayerStride.Walk;
        if (!icy) iceDirection = Point.Zero;
        else if (iceDirection != Point.Zero && held != iceDirection)
        {
            // One decelerating tile of momentum on release or a turn. Never drift through
            // a doorway, depth exit, room boundary or onto a different floor material.
            direction = iceDirection;
            iceDirection = Point.Zero;
            var target = new MapPosition(from.X + direction.X, from.Y + direction.Y);
            if (target.X is > 0 and < Map.Width - 1 && target.Y is > 0 and < Map.Height - 1
                && map.Tile(target) is 0x53 or 0x49 && map.DepthActionAt(target) == 0)
                nextStride = PlayerStride.IceBrake;
            else direction = held == Point.Zero ? Point.Zero : requested;
        }
        else if (iceDirection != Point.Zero) direction = held;
        else if (held == Point.Zero) direction = Point.Zero;

        if (direction == Point.Zero) return;
        var destination = new MapPosition(from.X + direction.X, from.Y + direction.Y);
        if (icy && nextStride != PlayerStride.IceBrake && Map.Contains(destination)
            && map.Tile(destination) is 0x53 or 0x49 && map.DepthActionAt(destination) == 0)
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
}
