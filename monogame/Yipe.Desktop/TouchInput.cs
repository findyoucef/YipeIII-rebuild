using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

namespace Yipe.Desktop;

public readonly record struct TouchRegion(Rectangle Bounds, InputFrame Action, int Row = -1, bool Slider = false);

/// <summary>One movement finger plus one UI finger; taps activate on release, never on a held press.</summary>
public sealed class TouchInput
{
    public List<TouchRegion> Regions { get; } = [];
    public Rectangle Pad { get; set; }
    public float Scale { get; set; } = 1;
    public bool CanSwipe { get; set; }
    public Point HeldDirection { get; private set; }
    public int SelectedRow { get; private set; } = -1;
    public int SliderRow { get; private set; } = -1;
    public float SliderValue { get; private set; }
    public bool SliderFinished { get; private set; }
    private int? padFinger, uiFinger;
    private Vector2 origin, swipeOrigin;
    private TouchRegion? pressed;
    private bool dragged, blocked;

    public void Reset()
    {
        padFinger = uiFinger = null; pressed = null;
        HeldDirection = Point.Zero; blocked = true;
    }

    public InputFrame Update(TouchCollection touches, bool active = true)
    {
        HeldDirection = Point.Zero; SelectedRow = SliderRow = -1; SliderFinished = false;
        if (!active) { Reset(); return default; }
        if (blocked)
        {
            if (!touches.Any(t => t.State is TouchLocationState.Pressed or TouchLocationState.Moved)) blocked = false;
            return default;
        }
        var action = default(InputFrame);
        foreach (var touch in touches)
        {
            var point = touch.Position.ToPoint();
            if (touch.State == TouchLocationState.Pressed)
            {
                if (padFinger is null && Pad.Contains(point)) padFinger = touch.Id;
                else if (uiFinger is null)
                {
                    uiFinger = touch.Id; origin = swipeOrigin = touch.Position; dragged = false; pressed = null;
                    for (var i = Regions.Count - 1; i >= 0; i--)
                        if (Regions[i].Bounds.Contains(point)) { pressed = Regions[i]; break; }
                }
            }
            if (padFinger == touch.Id)
            {
                if (touch.State is TouchLocationState.Released or TouchLocationState.Invalid) padFinger = null;
                else
                {
                    var offset = touch.Position - Pad.Center.ToVector2();
                    if (offset.Length() >= Pad.Width * .12f)
                    {
                        const float sector = .41421356f;
                        HeldDirection = Math.Abs(offset.Y) < Math.Abs(offset.X) * sector ? new(Math.Sign(offset.X), 0)
                            : Math.Abs(offset.X) < Math.Abs(offset.Y) * sector ? new(0, Math.Sign(offset.Y))
                            : new(Math.Sign(offset.X), Math.Sign(offset.Y));
                    }
                }
                continue;
            }
            if (uiFinger != touch.Id) continue;
            if (touch.State == TouchLocationState.Invalid) { uiFinger = null; continue; }
            if (pressed is { Slider: true } slider)
            {
                SliderRow = slider.Row;
                SliderValue = Math.Clamp((touch.Position.X - slider.Bounds.Left) / slider.Bounds.Width, 0, 1);
                SliderFinished = touch.State == TouchLocationState.Released;
            }
            else
            {
                if (Vector2.Distance(origin, touch.Position) > 8 * Scale) dragged = true;
                var delta = touch.Position - swipeOrigin;
                if (CanSwipe && dragged && Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y)) >= 16 * Scale)
                {
                    action = new(Math.Abs(delta.Y) >= Math.Abs(delta.X) ? new(0, -Math.Sign(delta.Y))
                        : new(-Math.Sign(delta.X), 0), false, false, false);
                    swipeOrigin = touch.Position;
                }
                if (touch.State == TouchLocationState.Released && !dragged && pressed is { } target && target.Bounds.Contains(point))
                { action = target.Action; SelectedRow = target.Row; }
            }
            if (touch.State == TouchLocationState.Released) { uiFinger = null; pressed = null; }
        }
        if (padFinger.HasValue && !touches.Any(t => t.Id == padFinger)) padFinger = null;
        if (uiFinger.HasValue && !touches.Any(t => t.Id == uiFinger)) { uiFinger = null; pressed = null; }
        return action;
    }
}
