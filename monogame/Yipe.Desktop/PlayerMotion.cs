using Microsoft.Xna.Framework;
using Yipe.Core;

namespace Yipe.Desktop;

public enum PlayerStride { Walk, IcePush, IceGlide, IceBrake }

/// <summary>Animates an accepted grid step without changing the game's movement rules.</summary>
public sealed class PlayerMotion
{
    public const double StepDuration = 0.25;
    public const double WalkFramesPerSecond = 4;
    private Vector2 origin;
    private double stepElapsed = StepDuration;
    private double walkingTime;
    private PlayerStride stride;
    private double Duration => stride switch { PlayerStride.IcePush => .34, PlayerStride.IceGlide => .22, PlayerStride.IceBrake => .42, _ => StepDuration };

    public MapPosition Position { get; private set; }
    public Vector2 VisualPosition { get; private set; }
    public bool IsMoving => stepElapsed < Duration;
    public bool IsSliding => IsMoving && stride is PlayerStride.IceGlide or PlayerStride.IceBrake;
    public int FacingRow { get; private set; }
    public int AnimationFrame => IsSliding ? 0 : (int)(walkingTime * WalkFramesPerSecond) % 4;

    public PlayerMotion(MapPosition position) => SnapTo(position);

    public void Face(Point direction)
    {
        if (direction.X is < -1 or > 1 || direction.Y is < -1 or > 1 || direction == Point.Zero)
            throw new ArgumentException("Facing must be one of the eight adjacent directions.", nameof(direction));
        // Reuse the approved four-direction artwork; diagonals use their horizontal facing.
        FacingRow = direction.X < 0 ? 1 : direction.X > 0 ? 2 : direction.Y > 0 ? 0 : 3;
    }

    public void MoveTo(MapPosition destination, PlayerStride stride = PlayerStride.Walk)
    {
        if (IsMoving) throw new InvalidOperationException("Finish the current step before starting another.");
        var direction = new Point(destination.X - Position.X, destination.Y - Position.Y);
        Face(direction);
        origin = VisualPosition;
        Position = destination;
        this.stride = stride;
        stepElapsed = 0;
    }

    // A raft changes the accepted tile without starting a walk or changing facing.
    // Keep an unfinished boarding step so its boots settle onto the moving deck.
    public void CarryTo(MapPosition destination) => Position = destination;

    public void Update(double elapsed, Vector2? support = null)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!IsMoving)
        {
            if (support is { } deck) VisualPosition = deck;
            return;
        }
        if (!IsSliding) walkingTime += Math.Min(elapsed, Duration - stepElapsed);
        stepElapsed = Math.Min(Duration, stepElapsed + elapsed);
        var progress = (float)(stepElapsed / Duration);
        if (stride == PlayerStride.IcePush) progress = .5f * progress + .5f * progress * progress;
        else if (stride == PlayerStride.IceBrake) progress = 2 * progress - progress * progress;
        VisualPosition = Vector2.Lerp(origin, support ?? new(Position.X, Position.Y), progress);
    }

    public void SnapTo(MapPosition position)
    {
        Position = position;
        VisualPosition = origin = new(position.X, position.Y);
        stride = PlayerStride.Walk;
        stepElapsed = StepDuration;
        walkingTime = 0;
    }
}
