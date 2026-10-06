using Microsoft.Xna.Framework;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Animates an accepted grid step without changing the game's movement rules.</summary>
public sealed class PlayerMotion
{
    public const double StepDuration = 0.25;
    public const double WalkFramesPerSecond = 4;
    private Vector2 origin;
    private double stepElapsed = StepDuration;
    private double walkingTime;

    public MapPosition Position { get; private set; }
    public Vector2 VisualPosition { get; private set; }
    public bool IsMoving => stepElapsed < StepDuration;
    public int FacingRow { get; private set; }
    public int AnimationFrame => (int)(walkingTime * WalkFramesPerSecond) % 4;

    public PlayerMotion(MapPosition position) => SnapTo(position);

    public void Face(Point direction)
    {
        if (direction.X is < -1 or > 1 || direction.Y is < -1 or > 1 || direction == Point.Zero)
            throw new ArgumentException("Facing must be one of the eight adjacent directions.", nameof(direction));
        // Reuse the approved four-direction artwork; diagonals use their horizontal facing.
        FacingRow = direction.X < 0 ? 1 : direction.X > 0 ? 2 : direction.Y > 0 ? 0 : 3;
    }

    public void MoveTo(MapPosition destination)
    {
        if (IsMoving) throw new InvalidOperationException("Finish the current step before starting another.");
        var direction = new Point(destination.X - Position.X, destination.Y - Position.Y);
        Face(direction);
        origin = VisualPosition;
        Position = destination;
        stepElapsed = 0;
    }

    public void Update(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!IsMoving) return;
        walkingTime += Math.Min(elapsed, StepDuration - stepElapsed);
        stepElapsed = Math.Min(StepDuration, stepElapsed + elapsed);
        VisualPosition = Vector2.Lerp(origin, new(Position.X, Position.Y), (float)(stepElapsed / StepDuration));
    }

    public void SnapTo(MapPosition position)
    {
        Position = position;
        VisualPosition = origin = new(position.X, position.Y);
        stepElapsed = StepDuration;
        walkingTime = 0;
    }
}
