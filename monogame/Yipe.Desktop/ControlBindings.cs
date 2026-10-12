using Microsoft.Xna.Framework.Input;

namespace Yipe.Desktop;

/// <summary>Gameplay bindings; menus always retain the standard navigation controls.</summary>
public sealed class ControlBindings
{
    public Keys[] Keyboard { get; set; } = [Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Enter, Keys.M, Keys.Escape];
    public Buttons[] Controller { get; set; } = [Buttons.A, Buttons.B, Buttons.Back, Buttons.Start];
    public static IReadOnlyList<string> KeyboardActions { get; } = Array.AsReadOnly(new[] { "Up", "Down", "Left", "Right", "Actions", "Map", "Pause" });
    public static IReadOnlyList<string> ControllerActions { get; } = Array.AsReadOnly(new[] { "Actions", "Back", "Map", "Pause" });
    public static IReadOnlyList<Buttons> BindableButtons { get; } = Array.AsReadOnly(new[]
    { Buttons.A, Buttons.B, Buttons.X, Buttons.Y, Buttons.Back, Buttons.Start, Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.LeftStick, Buttons.RightStick });

    public string ValidationError()
    {
        var error = ValidateKeyboard(Keyboard);
        if (error.Length != 0) return error;
        if (Controller is null || Controller.Length != 4 || Controller.Any(button => !BindableButtons.Contains(button)))
            return "Choose a face, shoulder, stick-click, View or Start button.";
        if (Controller.Distinct().Count() != 4) return "That button is already assigned to another action.";
        return "";
    }

    public string BindKey(int action, Keys key)
    {
        var candidate = (Keys[])Keyboard.Clone();
        candidate[action] = key;
        var error = ValidateKeyboard(candidate);
        if (error.Length == 0) Keyboard = candidate;
        return error;
    }

    public string BindButton(int action, Buttons button)
    {
        if (!BindableButtons.Contains(button)) return "Choose a face, shoulder, stick-click, View or Start button.";
        if (Controller.Where((_, index) => index != action).Contains(button)) return "That button is already assigned to another action.";
        Controller[action] = button;
        return "";
    }

    private static string ValidateKeyboard(Keys[]? keys)
    {
        if (keys is null || keys.Length != 7) return "Seven keyboard actions are required.";
        if (keys.Any(key => !Enum.IsDefined(key) || key is Keys.None or Keys.LeftShift or Keys.RightShift
            or Keys.LeftControl or Keys.RightControl or Keys.LeftAlt or Keys.RightAlt or Keys.LeftWindows or Keys.RightWindows))
            return "Choose a single key, without modifiers.";
        if (keys.Distinct().Count() != 7) return "That key is already assigned to another action.";
        // WASD and Space remain convenience aliases in gameplay. Do not let an action also move the hero.
        for (var index = 0; index < keys.Length; index++)
        {
            var alias = keys[index] switch { Keys.W => 0, Keys.S => 1, Keys.A => 2, Keys.D => 3, Keys.Space => 4, _ => -1 };
            if (alias >= 0 && alias != index) return "That key is reserved for movement or Actions.";
        }
        return "";
    }

    public static string ButtonName(Buttons button) => button switch
    {
        Buttons.A => "South", Buttons.B => "East", Buttons.X => "West", Buttons.Y => "North",
        Buttons.Back => "View/Back", Buttons.Start => "Start", Buttons.LeftShoulder => "Left shoulder",
        Buttons.RightShoulder => "Right shoulder", Buttons.LeftStick => "Left stick click", Buttons.RightStick => "Right stick click",
        _ => button.ToString(),
    };
}
