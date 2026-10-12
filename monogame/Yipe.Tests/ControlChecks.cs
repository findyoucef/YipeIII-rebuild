using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Yipe.Desktop;

internal static class ControlChecks
{
    public static void Run(Action<bool, string> check)
    {
        var controls = new ControlBindings();
        check(controls.ValidationError() == "", "Default gameplay controls are valid");
        var original = (Keys[])controls.Keyboard.Clone();
        foreach (var key in new[] { Keys.M, Keys.W, Keys.None, Keys.LeftShift, (Keys)999 })
            check(controls.BindKey(4, key).Length != 0 && controls.Keyboard.SequenceEqual(original),
                $"Rejected Actions key {key} preserves the existing bindings");
        check(controls.BindKey(0, Keys.Space).Length != 0 && controls.Keyboard.SequenceEqual(original),
            "Space cannot be rebound to movement while it remains an Actions alias");
        check(controls.BindKey(0, Keys.I) == "" && controls.BindKey(3, Keys.L) == ""
            && controls.BindKey(4, Keys.E) == "", "Movement and Actions can be rebound independently");
        var input = new GameInput();
        check(input.Update(new(Keys.I, Keys.L), default, .016, controls: controls).Direction == new Point(1, -1),
            "Remapped keyboard movement supports diagonals");
        input.Update(default, default, .016, controls: controls);
        var frame = input.Update(new(Keys.E), default, .016, controls: controls);
        check(frame.Confirm && frame.Direction == Point.Zero && !frame.Map && !frame.Pause,
            "Remapped Actions produces one action without movement");
        check(!input.Update(new(Keys.E), default, 1, controls: controls).Confirm, "Holding a remapped action does not reopen it");
        input.Update(new(Keys.E), default, 1, active: false, controls: controls);
        check(!input.Update(new(Keys.E), default, .016, controls: controls).Confirm, "Focus return suppresses held remapped actions");
        input.Update(default, default, .016);
        frame = input.Update(new(Keys.Up, Keys.Enter), default, .016);
        check(frame.Direction == new Point(0, -1) && frame.Confirm,
            "Standard menu navigation remains available independently of gameplay remaps");
        input.Update(default, default, .016);
        frame = input.Update(new(Keys.E, Keys.I), default, .016, textInput: true, controls: controls);
        check(frame.Direction == Point.Zero && !frame.Confirm, "Gameplay letter remaps do not interfere with name/code entry");
        check(controls.BindButton(0, Buttons.B).Length != 0 && controls.Controller[0] == Buttons.A
            && controls.BindButton(0, Buttons.DPadUp).Length != 0, "Controller conflicts and movement buttons are rejected");
        check(controls.BindButton(0, Buttons.X) == "" && controls.BindButton(1, Buttons.Y) == ""
            && controls.BindButton(2, Buttons.LeftShoulder) == "" && controls.BindButton(3, Buttons.RightShoulder) == "",
            "All four controller gameplay shortcuts can be rebound");
        var padInput = new GameInput();
        GamePadState Pad(params Buttons[] buttons) => new(Vector2.Zero, Vector2.Zero, 0, 0, buttons);
        padInput.Update(default, Pad(), 0, controls: controls);
        foreach (var (button, action) in controls.Controller.Select((button, index) => (button, index)))
        {
            padInput.Update(default, Pad(), .016, controls: controls);
            frame = padInput.Update(default, Pad(button), .016, controls: controls);
            check(frame.Confirm == (action == 0) && frame.Back == (action == 1)
                && frame.Map == (action == 2) && frame.Pause == (action == 3), $"Rebound {button} dispatches only its assigned action");
        }
        padInput.Update(default, default, .016, controls: controls);
        check(!padInput.Update(default, Pad(Buttons.X), .016, controls: controls).Confirm,
            "Controller reconnect suppresses a held rebound button");
        controls.Keyboard = [Keys.Up];
        check(controls.ValidationError().Length != 0, "Malformed binding arrays are rejected before use");
        controls = new() { Controller = [(Buttons)12345, Buttons.B, Buttons.Back, Buttons.Start] };
        check(controls.ValidationError().Length != 0, "Unknown or combined controller buttons are rejected");
    }
}
