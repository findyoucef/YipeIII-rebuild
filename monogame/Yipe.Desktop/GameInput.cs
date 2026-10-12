using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Yipe.Desktop;

public readonly record struct InputFrame(Point Direction, bool Confirm, bool Back, bool Pause, bool Map = false);

/// <summary>Converts keyboard/controller state to actions; held movement repeats on elapsed time.</summary>
public sealed class GameInput
{
    private static readonly ControlBindings Defaults = new();
    private KeyboardState previousKeyboard;
    private GamePadState previousPad;
    private Point previousDirection;
    private double repeatRemaining;
    public Point HeldDirection { get; private set; }

    public InputFrame Update(KeyboardState keyboard, GamePadState pad, double elapsed, bool active = true,
        double repeatInterval = PlayerMotion.StepDuration, bool textInput = false, ControlBindings? controls = null)
    {
        controls = textInput ? Defaults : controls ?? Defaults;
        if (!active || pad.IsConnected != previousPad.IsConnected)
        {
            previousKeyboard = keyboard;
            previousPad = pad;
            previousDirection = Direction(keyboard, pad, controls, textInput);
            repeatRemaining = 0.28;
            HeldDirection = Point.Zero;
            return default;
        }

        var direction = Direction(keyboard, pad, controls, textInput);
        var step = Point.Zero;
        if (direction != Point.Zero)
        {
            repeatRemaining -= elapsed;
            if (direction != previousDirection || repeatRemaining <= 0)
            {
                step = direction;
                // At most one move after a delayed frame; never replay a backlog of input.
                repeatRemaining = direction != previousDirection ? 0.28 : repeatInterval;
            }
        }
        // Retain held intent between repeat pulses, while respecting focus/connection suppression.
        if (direction == Point.Zero || step != Point.Zero || HeldDirection != Point.Zero) HeldDirection = direction;
        var result = new InputFrame(step,
            Pressed(controls.Keyboard[4]) || !textInput && Pressed(Keys.Space) || PressedButton(controls.Controller[0]),
            Pressed(controls.Keyboard[6]) || PressedButton(controls.Controller[1]),
            Pressed(controls.Keyboard[6]) || PressedButton(controls.Controller[3]),
            !textInput && (Pressed(controls.Keyboard[5]) || PressedButton(controls.Controller[2])));
        previousDirection = direction;
        previousKeyboard = keyboard;
        previousPad = pad;
        return result;

        bool Pressed(Keys key) => keyboard.IsKeyDown(key) && previousKeyboard.IsKeyUp(key);
        bool PressedButton(Buttons button) => pad.IsButtonDown(button) && previousPad.IsButtonUp(button);
    }

    private static Point Direction(KeyboardState keyboard, GamePadState pad, ControlBindings controls, bool textInput)
    {
        var x = (keyboard.IsKeyDown(controls.Keyboard[3]) || (!textInput && keyboard.IsKeyDown(Keys.D))
                 || pad.IsButtonDown(Buttons.DPadRight) ? 1 : 0)
              - (keyboard.IsKeyDown(controls.Keyboard[2]) || (!textInput && keyboard.IsKeyDown(Keys.A))
                 || pad.IsButtonDown(Buttons.DPadLeft) ? 1 : 0);
        var y = (keyboard.IsKeyDown(controls.Keyboard[1]) || (!textInput && keyboard.IsKeyDown(Keys.S))
                 || pad.IsButtonDown(Buttons.DPadDown) ? 1 : 0)
              - (keyboard.IsKeyDown(controls.Keyboard[0]) || (!textInput && keyboard.IsKeyDown(Keys.W))
                 || pad.IsButtonDown(Buttons.DPadUp) ? 1 : 0);
        if (x != 0 || y != 0) return new(x, y);
        var stick = pad.ThumbSticks.Left;
        if (Math.Max(Math.Abs(stick.X), Math.Abs(stick.Y)) < 0.45f) return Point.Zero;
        // Eight 45-degree sectors, with the existing dead zone. A small off-axis tilt stays cardinal.
        const float sectorBoundary = 0.41421356f; // tan(22.5 degrees)
        if (Math.Abs(stick.Y) < Math.Abs(stick.X) * sectorBoundary) return new(Math.Sign(stick.X), 0);
        if (Math.Abs(stick.X) < Math.Abs(stick.Y) * sectorBoundary) return new(0, -Math.Sign(stick.Y));
        return new(Math.Sign(stick.X), -Math.Sign(stick.Y));
    }
}
