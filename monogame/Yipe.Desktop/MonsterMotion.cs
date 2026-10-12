using Microsoft.Xna.Framework;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Visual travel for one live roaming actor; the adventure owns its accepted tile.</summary>
internal sealed class MonsterMotion
{
    private object? actor;
    private Vector2 origin;
    private double elapsed = 1;
    private bool pursuitAnnounced;
    public double AlertRemaining { get; private set; }
    public MapPosition Position { get; private set; }
    public Vector2 VisualPosition { get; private set; }
    public bool FacingLeft { get; private set; }
    public bool AlternateStep { get; private set; }
    public bool IsMoving => actor is not null && elapsed < PlayerMotion.StepDuration;
    public float Progress => (float)Math.Clamp(elapsed / PlayerMotion.StepDuration, 0, 1);
    public double LandingAge => elapsed - PlayerMotion.StepDuration;
    public bool Tracks(object value) => ReferenceEquals(actor, value);
    internal static bool UsesHeavyStep(int type) => type is 4 or 8 or 9 or 11 or 19 or 20 or 33 or 36 or 37 or 39 or 40 or 41;

    public bool Synchronize(object? value, MapPosition position, bool animate, MapPosition player, bool visible)
    {
        if (value is null) { actor = null; elapsed = 1; AlternateStep = false; pursuitAnnounced = false; AlertRemaining = 0; return false; }
        var alerted = false;
        var sameActor = ReferenceEquals(actor, value);
        var direction = new Point(position.X - Position.X, position.Y - Position.Y);
        var adjacent = Math.Abs(direction.X) <= 1 && Math.Abs(direction.Y) <= 1;
        if (!sameActor || !animate || !visible || !adjacent) { pursuitAnnounced = false; AlertRemaining = 0; }
        if (sameActor && animate && position != Position && adjacent && visible && !pursuitAnnounced
            && Vector2.DistanceSquared(new(position.X, position.Y), new(player.X, player.Y))
                < Vector2.DistanceSquared(new(Position.X, Position.Y), new(player.X, player.Y)))
        {
            pursuitAnnounced = true;
            AlertRemaining = .7;
            alerted = true;
        }
        if (sameActor && animate && position == Position) return false;
        if (sameActor && animate && adjacent)
        {
            origin = VisualPosition;
            elapsed = 0;
            AlternateStep = !AlternateStep;
            if (direction.X != 0) FacingLeft = direction.X < 0;
        }
        else
        {
            VisualPosition = origin = new(position.X, position.Y);
            elapsed = 1;
            AlternateStep = false;
            if (!sameActor) FacingLeft = false;
        }
        actor = value;
        Position = position;
        return alerted;
    }

    public void Update(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        elapsed = Math.Min(1, elapsed + seconds);
        AlertRemaining = Math.Max(0, AlertRemaining - seconds);
        var progress = Progress;
        // Heavy creatures ease into their stride and settle within the same step time.
        var type = actor switch { RoamingMonster monster => monster.TypeId, PursuingMonster monster => monster.TypeId, _ => 0 };
        if (UsesHeavyStep(type))
            progress = progress * progress * (3 - 2 * progress);
        VisualPosition = Vector2.Lerp(origin, new(Position.X, Position.Y), progress);
    }
}
