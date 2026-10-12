using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private enum PausePage { Main, Adventure, Options, Display, Atmosphere, Audio }
    private const int PauseRowHeight = 12;
    private static readonly string[][] PauseMenus =
    [
        ["Continue", "Adventure", "Options", "Return to title", "Quit game"],
        ["Save game", "Load game", "New adventure", "Back"],
        ["Display", "Atmosphere", "Sound", "Battle pacing", "Edit controls", "Help & credits", "Back"],
        ["Fullscreen", "Scanlines", "Miniature effect", "Vignette", "Screen shake", "Back"],
        ["Pixel lighting", "Sun shafts", "Cloud shadows", "Rain", "Time of day", "Back"],
        ["Sound", "Master volume", "Sound effects volume", "Music", "Music volume", "Back"],
    ];
    private PausePage pausePage;
    private Action? restorePauseOverlay;
    private Overlay? soundMenuParent;

    private void OpenPausePage(PausePage page, int index = 0)
    {
        paused = true; pausePage = page; selected = index;
        selectionY = index * PauseRowHeight; menuMessage = "";
    }

    private void BackPausePage()
    {
        switch (pausePage)
        {
            case PausePage.Main: paused = false; break;
            case PausePage.Adventure: OpenPausePage(PausePage.Main, 1); break;
            case PausePage.Options:
                if (titleScreen) { paused = false; menuMessage = ""; }
                else OpenPausePage(PausePage.Main, 2);
                break;
            case PausePage.Display: OpenPausePage(PausePage.Options); break;
            case PausePage.Atmosphere: OpenPausePage(PausePage.Options, 1); break;
            case PausePage.Audio:
                if (soundMenuParent is not null)
                {
                    overlay = soundMenuParent; soundMenuParent = null;
                    paused = false; pauseAmount = 0;
                }
                else OpenPausePage(PausePage.Options, 2);
                break;
        }
    }

    private void UpdatePauseMenu(InputFrame actions)
    {
        if (actions.Back || actions.Pause) { BackPausePage(); return; }
        var count = PauseMenus[(int)pausePage].Length;
        if (actions.Direction.Y != 0) selected = (selected + actions.Direction.Y + count) % count;
        if (pausePage == PausePage.Audio && actions.Direction.X != 0)
        {
            var change = actions.Direction.X * .1f;
            switch (selected)
            {
                case 0: audio.Enabled = actions.Direction.X > 0; break;
                case 1: audio.Volume = Math.Clamp(MathF.Round(audio.Volume + change, 2), 0, 1); break;
                case 2: audio.EffectsVolume = Math.Clamp(MathF.Round(audio.EffectsVolume + change, 2), 0, 1); break;
                case 3: profile.Data.Music = actions.Direction.X > 0; break;
                case 4: profile.Data.MusicVolume = Math.Clamp(MathF.Round(profile.Data.MusicVolume + change, 2), 0, 1); break;
                default: return;
            }
            SavePreferences();
        }
        else if (actions.Confirm) ActivateMenu();
    }

    // Help and binding editors temporarily replace an interaction, then restore it.
    private void ShowPauseOverlay(Action show)
    {
        restorePauseOverlay?.Invoke();
        var previous = overlay; var receiver = textReceiver; var wasMapOpen = mapOpen;
        restorePauseOverlay = () => { overlay = previous; textReceiver = receiver; mapOpen = wasMapOpen; };
        textReceiver = null; mapOpen = false; paused = false; pauseAmount = 0;
        show();
    }

    private void ReturnToPause()
    {
        if (restorePauseOverlay is not null) restorePauseOverlay();
        else overlay = null;
        restorePauseOverlay = null; paused = true;
    }

    private void ActivateMenu()
    {
        if (pausePage != PausePage.Main && selected == PauseMenus[(int)pausePage].Length - 1)
        { BackPausePage(); return; }
        switch (pausePage)
        {
            case PausePage.Main:
                switch (selected)
                {
                    case 0: paused = false; break;
                    case 1: OpenPausePage(PausePage.Adventure); break;
                    case 2: OpenPausePage(PausePage.Options); break;
                    case 3: ShowReturnToTitle(); break;
                    case 4: Exit(); break;
                }
                return;
            case PausePage.Adventure:
                switch (selected)
                {
                    case 0:
                        if (command is not null || textReceiver is not null)
                        { menuMessage = "Finish this interaction\nbefore saving."; break; }
                        menuMessage = "Saving..."; saving = saves.SaveAsync(session, journal); break;
                    case 1: menuMessage = "Loading..."; loading = saves.LoadAsync(); break;
                    case 2: ShowNewGameConfirmation(); break;
                }
                return;
            case PausePage.Options:
                switch (selected)
                {
                    case 0: OpenPausePage(PausePage.Display); return;
                    case 1: OpenPausePage(PausePage.Atmosphere); return;
                    case 2: OpenPausePage(PausePage.Audio); return;
                    case 3: profile.Data.AutoBattle = !profile.Data.AutoBattle; break;
                    case 4: ShowPauseOverlay(() => EditControls()); return;
                    case 5: ShowPauseOverlay(ShowHelp); return;
                }
                break;
            case PausePage.Display:
                switch (selected)
                {
                    case 0:
                        graphics.IsFullScreen = !graphics.IsFullScreen;
                        graphics.PreferredBackBufferWidth = graphics.IsFullScreen
                            ? GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width : CanvasWidth * 4;
                        graphics.PreferredBackBufferHeight = graphics.IsFullScreen
                            ? GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height : CanvasHeight * 4;
                        graphics.ApplyChanges(); return;
                    case 1: scanlines = !scanlines; break;
                    case 2: miniatureMode = (miniatureMode + 1) % EffectStrengths.Length; break;
                    case 3: vignette = !vignette; break;
                    case 4: screenShake = !screenShake; break;
                }
                break;
            case PausePage.Atmosphere:
                switch (selected)
                {
                    case 0: pixelLighting = !pixelLighting; break;
                    case 1: sunShaftStrength = (sunShaftStrength + 1) % EffectStrengths.Length; break;
                    case 2: cloudShadowStrength = (cloudShadowStrength + 1) % EffectStrengths.Length; break;
                    case 3: rainStrength = (rainStrength + 1) % EffectStrengths.Length; break;
                    case 4: timeOfDayMode = (timeOfDayMode + 1) % TimesOfDay.Length; break;
                }
                break;
            case PausePage.Audio:
                switch (selected)
                {
                    case 0: audio.Enabled = !audio.Enabled; break;
                    case 1 or 2 or 4: return;
                    case 3: profile.Data.Music = !profile.Data.Music; break;
                }
                break;
        }
        SavePreferences();
    }

    private string PauseValue(int row) => (pausePage, row) switch
    {
        (PausePage.Main, 1 or 2) or (PausePage.Options, 0 or 1 or 2 or 4 or 5) => ">",
        (PausePage.Options, 3) => profile.Data.AutoBattle ? "Auto" : "Manual",
        (PausePage.Display, 0) => graphics.IsFullScreen ? "On" : "Off",
        (PausePage.Display, 1) => scanlines ? "On" : "Off",
        (PausePage.Display, 2) => EffectStrengths[miniatureMode],
        (PausePage.Display, 3) => vignette ? "On" : "Off",
        (PausePage.Display, 4) => screenShake ? "On" : "Off",
        (PausePage.Atmosphere, 0) => pixelLighting ? "On" : "Off",
        (PausePage.Atmosphere, 1) => EffectStrengths[sunShaftStrength],
        (PausePage.Atmosphere, 2) => EffectStrengths[cloudShadowStrength],
        (PausePage.Atmosphere, 3) => rainStrength == 0 ? "Off" : "Auto " + EffectStrengths[rainStrength],
        (PausePage.Atmosphere, 4) => TimesOfDay[timeOfDayMode],
        (PausePage.Audio, 0) => audio.Enabled ? "On" : "Off",
        (PausePage.Audio, 1) => $"{MathF.Round(audio.Volume * 100)}%",
        (PausePage.Audio, 2) => $"{MathF.Round(audio.EffectsVolume * 100)}%",
        (PausePage.Audio, 3) => music.Error.Length != 0 ? "Unavailable" : profile.Data.Music ? "On" : "Off",
        (PausePage.Audio, 4) => $"{MathF.Round(profile.Data.MusicVolume * 100)}%",
        _ => "",
    };

    private void DrawPauseMenu()
    {
        var offset = (int)MathF.Round((1 - pauseAmount) * 4);
        Fill(new(0, 0, CanvasWidth, CanvasHeight), Color.Black * .5f);
        DrawPanel(new(28, 4 + offset, 264, 172));
        var breadcrumb = soundMenuParent is not null ? "ACTIONS" : pausePage == PausePage.Main ? $"{LocationName()} / {player.Position.X},{player.Position.Y}"
            : pausePage is PausePage.Display or PausePage.Atmosphere or PausePage.Audio
                ? (titleScreen ? "TITLE" : "PAUSE") + " / OPTIONS" : titleScreen ? "TITLE" : "PAUSE";
        font.Draw(batch, breadcrumb, 42, 12 + offset, Muted);
        font.Draw(batch, pausePage == PausePage.Main ? "Paused" : pausePage == PausePage.Audio ? "Sound" : pausePage.ToString(), 42, 25 + offset, Accent, 2);
        font.Draw(batch, controllerConnected ? "EAST: BACK" : "ESC: BACK", 222, 15 + offset, Muted);
        var items = PauseMenus[(int)pausePage];
        var highlight = 52 + Math.Clamp((int)MathF.Round(selectionY), 0, (items.Length - 1) * PauseRowHeight) + offset;
        Fill(new(40, highlight, 240, 12), new Color(64, 73, 87));
        Fill(new(44, highlight + 3, 2, 6), Accent);
        for (var i = 0; i < items.Length; i++)
        {
            var color = i == selected ? Accent : Ink;
            font.Draw(batch, items[i], 53, 55 + i * PauseRowHeight + offset, color);
            var value = PauseValue(i);
            if (pausePage == PausePage.Audio && i == selected && i is 1 or 2 or 4) value = $"< {value} >";
            font.Draw(batch, value, 271 - value.Length * 6, 55 + i * PauseRowHeight + offset, color);
        }
        Fill(new(42, 140 + offset, 236, 1), Muted * .25f);
        var hint = menuMessage.Length != 0 ? menuMessage : pausePage switch
        {
            PausePage.Main => selected switch
            { 0 => "Return to your adventure.", 1 => "Save, load or start a new adventure.", 2 => "Display, atmosphere, sound and controls.", 3 => "Go to the title screen.", _ => "Close the game." },
            PausePage.Adventure => "Each new adventure has its own save.",
            PausePage.Options => selected == 3 ? "Auto returns after ordinary results.\nVictories still wait for confirmation." : "Choose a category to adjust.",
            PausePage.Display => "Change the screen and pixel-art effects.",
            PausePage.Atmosphere => "Lighting, weather and time of day.",
            _ => audio.Error.Length != 0 ? audio.Error : "Left/right adjusts volume.\nEffects: footsteps and ambience.",
        };
        font.Draw(batch, string.Join('\n', Overlay.Wrap(hint, 39).Take(2)), 42, 145 + offset, Muted);
        font.Draw(batch, pausePage == PausePage.Audio ? "UP/DOWN MOVE  LEFT/RIGHT ADJUST"
            : controllerConnected ? "D-PAD/STICK MOVE  SOUTH SELECT" : "ARROWS/WASD MOVE  ENTER SELECT", 42, 166 + offset, Muted);
    }
}
