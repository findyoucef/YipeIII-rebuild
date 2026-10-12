using System.Text.Json;
using Microsoft.Xna.Framework.Input;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private int bindingAction = -1;


    private void ShowHelp() => Choices("Help & credits", "Controls, the gameplay guide and the creators.", [
        new("Current controls", ShowCurrentControls), new("Gameplay guide", () =>
            Notice("Gameplay guide", "Gameplay notes from the original manual. Use Current controls for this port's keyboard and controller mappings.", ShowManualContents)),
        new("Stats and saves", () => Notice("Stats and saves",
            $"New adventurers use a stat limit of 50. Loaded games retain their saved limits.\nYour current stat limit: {Hero.MaximumStat}.\nPreferences and scores are stored separately from your adventure.", ShowHelp)),
        new("About the game", ShowAbout),
        new("Credits", ShowCredits),
        new("Back", ReturnToPause)], ReturnToPause);

    private void ShowCurrentControls()
    {
        var controls = profile.Data.Controls;
        var keyboard = string.Join("\n", ControlBindings.KeyboardActions.Select((action, index) => $"{action}: {controls.Keyboard[index]}"));
        var controller = string.Join("\n", ControlBindings.ControllerActions.Select((action, index) => $"{action}: {ControlBindings.ButtonName(controls.Controller[index])}"));
        Notice("Current controls", $"GAMEPLAY KEYBOARD\n{keyboard}\nWASD also moves; Space opens Actions. Combine directions for diagonals.\nGAMEPLAY CONTROLLER\nD-pad / left stick: eight-way movement\n{controller}\nActions includes items, spells, Character, Read sign and Other.\nICE FLOORS\nOpen ice takes longer to gain traction. Hold a direction to glide; release or turn early to allow one tile of braking. Near walls, doors, stairs and room edges, normal footing lets you stop and turn precisely.\nMENUS AND MAP\nAlways use arrows/WASD or D-pad/stick to select. Enter/Space or South confirms. Escape or East goes back; Start pauses. M or View closes the map; confirm switches area/world. Left/Right changes pages or map layers.\nBATTLE\nEscape or East attempts to flee from the main battle menu. In spell, inventory and enemy-info screens, it goes back. Left/Right selects living enemies in the battle menu and attack-spell power choices. High power highlights all affected enemies. HP/SP/AP remain visible while choosing actions. Enemy info shows current ratings, spell resistance and recorded defeats. Left/Right browses living enemies there; Back preserves your attack target. Inspecting uses no turn. Battle pacing in Options offers Manual or Auto return after ordinary results; victories and defeats wait for confirmation.\nGRAPHICS\nVisual settings are in Options > Display and Options > Atmosphere. Escape/East or Start returns one menu level; play stays paused while editing.\nTIME OF DAY\nAuto cycles through day, dusk, night and dawn over twenty minutes of elapsed time. Time continues through pause, menus, combat, maps and focus loss, and catches up after suspension. Movement and combat still pause normally. Day, Dusk and Night hold a fixed look. Rain and Pixel lighting have separate settings.\nTEXT ENTRY\nType your name or code. Enter confirms, Escape cancels, Backspace deletes.\nSAVE AND LOAD\nUse Pause > Adventure. New adventures have separate save slots. Return to Title opens New Adventure, Load Game and Options. Finish an interaction before saving. Saving during a step records its accepted destination. Focus loss or controller disconnect pauses play.\nEdit controls changes gameplay shortcuts; menus keep these standard controls.", ShowHelp);
    }

    private void ShowAbout() => Notice("About Quest of Yipe!",
        File.ReadAllText(Path.Combine(contentDirectory, "Credits", "game.txt")), ShowHelp);

    private void ShowCredits() => Choices("Credits", "The people behind the adventure.", [
        new("Game & remaster", () => Notice("Game & remaster",
            File.ReadAllText(Path.Combine(contentDirectory, "Credits", "game.txt")), ShowCredits)),
        new("Music", () => Notice("Music credits",
            File.ReadAllText(Path.Combine(contentDirectory, "Audio", "Music", "CREDITS.txt")), ShowCredits)),
        new("Sound & ambience", () => Notice("Sound & ambience",
            File.ReadAllText(Path.Combine(contentDirectory, "Credits", "sound.txt")), ShowCredits)),
        new("Technology", () => Notice("Technology",
            File.ReadAllText(Path.Combine(contentDirectory, "Credits", "technology.txt")), ShowCredits)),
        new("Back", ShowHelp)], ShowHelp);

    private void ShowManualContents()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(contentDirectory, "original-help.json")));
        var options = document.RootElement.EnumerateArray().Select((page, index) =>
            new OverlayOption(page.GetProperty("title").GetString()!, () => ShowManualPage(index))).ToList();
        options.Add(new("Back", ShowHelp));
        Choices("Gameplay guide", "Gameplay notes; current controls are in Help.", options, ShowHelp);
    }

    private void ShowManualPage(int index)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(contentDirectory, "original-help.json")));
        var pages = document.RootElement;
        var page = pages[index];
        var options = new List<OverlayOption>();
        if (index > 0) options.Add(new("Previous topic", () => ShowManualPage(index - 1)));
        if (index + 1 < pages.GetArrayLength()) options.Add(new("Next topic", () => ShowManualPage(index + 1)));
        options.Add(new("Contents", ShowManualContents));
        Choices(page.GetProperty("title").GetString()!, page.GetProperty("body").GetString()!.Replace('\t', ' '), options, ShowManualContents);
    }

    private void EditControls(int selectedIndex = 0)
    {
        var controls = profile.Data.Controls;
        var options = ControlBindings.KeyboardActions.Select((action, index) => new OverlayOption(
            $"Key {action}: {controls.Keyboard[index]}", () => BindKey(index))).ToList();
        options.AddRange(ControlBindings.ControllerActions.Select((action, index) => new OverlayOption(
            $"Pad {action}: {ControlBindings.ButtonName(controls.Controller[index])}", () => ChooseButton(index))));
        options.Add(new("Reset controls", () => Choices("Reset controls?", "Restore the standard gameplay bindings?", [
            new("Keep my controls", () => EditControls(11)), new("Restore defaults", () =>
            { profile.Data.Controls = new(); SavePreferences(); EditControls(11); })], () => EditControls(11))));
        options.Add(new("Back", ReturnToPause));
        Choices("Edit controls", "Gameplay shortcuts only. Menus always use standard controls.\n" + menuMessage, options, ReturnToPause);
        overlay!.Selected = selectedIndex;
    }

    private void BindKey(int index)
    {
        bindingAction = index;
        Notice("Key: " + ControlBindings.KeyboardActions[index], "Press one key, without modifiers. Escape or East cancels. Conflicts are rejected.", () =>
        { bindingAction = -1; EditControls(index); });
    }

    private void UpdateBinding(KeyboardState keyboard)
    {
        var keys = keyboard.GetPressedKeys();
        if (!keys.Any(key => previousKeyboardState.IsKeyUp(key))) return;
        var index = bindingAction;
        bindingAction = -1;
        if (keys.Contains(Keys.Escape)) { EditControls(index); return; }
        var error = keys.Length == 1 ? profile.Data.Controls.BindKey(index, keys[0]) : "Choose a single key, without modifiers.";
        if (error.Length != 0) Notice("Key not changed", error, () => EditControls(index));
        else { SavePreferences(); EditControls(index); }
    }

    private void ChooseButton(int index)
    {
        var options = ControlBindings.BindableButtons.Select(button => new OverlayOption(ControlBindings.ButtonName(button), () =>
        {
            var error = profile.Data.Controls.BindButton(index, button);
            if (error.Length != 0) Notice("Button not changed", error, () => ChooseButton(index));
            else { SavePreferences(); EditControls(index + 7); }
        })).ToList();
        options.Add(new("Cancel", () => EditControls(index + 7)));
        Choices("Pad: " + ControlBindings.ControllerActions[index], "Choose by physical button position. D-pad and stick movement stay fixed.", options, () => EditControls(index + 7));
    }
}
