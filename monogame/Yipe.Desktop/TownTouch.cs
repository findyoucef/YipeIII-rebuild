using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private bool touchEnabled = OperatingSystem.IsAndroid();
    private readonly TouchInput touchInput = new();
    private object? touchContext;
    private Rectangle touchCanvas;
    private Vector4 touchInsets;
    private bool touchSettingsDirty;
    private static readonly InputFrame TouchConfirm = new(default, true, false, false);
    private static readonly InputFrame TouchBack = new(default, false, true, false);

    internal void SetTouchInsets(int left, int top, int right, int bottom) => touchInsets = new(left, top, right, bottom);

    private InputFrame ReadTouch(TouchCollection touches)
    {
        object context = (titleScreen, paused, pausePage, overlay, mapOpen, worldMapOpen,
            studioSplashRemaining > 0, controllerConnected, GraphicsDevice.Viewport.Bounds);
        if (!Equals(context, touchContext))
        {
            touchInput.Reset(); touchContext = context;
            pendingDirection = Point.Zero;
        }
        var action = touchInput.Update(touches, IsActive && saving is null && loading is null);
        if (touchInput.SelectedRow is var row && row >= 0)
        {
            if (paused) { selected = row; selectionY = row * PauseRowHeight; }
            else if (overlay is not null) overlay.Selected = row;
            else if (titleScreen) titleSelected = row;
        }
        if (paused && touchInput.SliderRow >= 0)
        {
            selected = touchInput.SliderRow;
            var value = MathF.Round(touchInput.SliderValue, 2);
            if (pausePage == PausePage.Audio)
            {
                if (selected == 1) audio.Volume = value;
                else if (selected == 2) audio.EffectsVolume = value;
                else if (selected == 4) profile.Data.MusicVolume = value;
            }
            else if (pausePage == PausePage.Touch)
            {
                if (selected == 0) profile.Data.TouchSize = .8f + value * .5f;
                else if (selected == 1) profile.Data.TouchOpacity = .2f + value * .8f;
            }
            touchSettingsDirty = true;
        }
        if (touchSettingsDirty && (touchInput.SliderFinished || touchInput.SliderRow < 0))
        { SavePreferences(); touchSettingsDirty = false; }
        return action;
    }

    // Targets are registered alongside the actual rendered rectangles, so layout and input cannot drift apart.
    private void TouchTarget(Rectangle bounds, InputFrame action, int row = -1, bool slider = false)
    {
        if (!touchEnabled) return;
        var scale = touchCanvas.Width / (float)CanvasWidth;
        touchInput.Regions.Add(new(new(touchCanvas.X + (int)(bounds.X * scale), touchCanvas.Y + (int)(bounds.Y * scale),
            (int)(bounds.Width * scale), (int)(bounds.Height * scale)), action, row, slider));
    }

    private void TouchChoice(Rectangle bounds, int row) => TouchTarget(bounds, TouchConfirm, row);

    private void DrawTouchButton(Rectangle bounds, string label, InputFrame action)
    {
        Fill(bounds, new Color(45, 57, 72));
        font.Draw(batch, label, bounds.Center.X - label.Length * 3, bounds.Center.Y - 3, Ink);
        TouchTarget(bounds, action);
    }

    private void DrawTouchNavigation(bool horizontal = false)
    {
        Fill(new(20, 159, 280, 15), Panel);
        DrawTouchButton(new(22, 159, 48, 15), "BACK", TouchBack);
        DrawTouchButton(new(77, 159, 48, 15), "UP", new(new(0, -1), false, false, false));
        DrawTouchButton(new(132, 159, 48, 15), "DOWN", new(new(0, 1), false, false, false));
        var count = paused ? CurrentPauseItems.Length : overlay?.Options.Length ?? 0;
        var choice = paused ? selected : overlay?.Selected ?? 0;
        if (count is > 0 and < 10) font.Draw(batch, $"{choice + 1}/{count}", 182, 163, Muted);
        if (horizontal)
        {
            DrawTouchButton(new(202, 159, 42, 15), "<", new(new(-1, 0), false, false, false));
            DrawTouchButton(new(251, 159, 42, 15), ">", new(new(1, 0), false, false, false));
        }
    }

    private void DrawTouchPauseMenu()
    {
        Fill(new(0, 0, CanvasWidth, CanvasHeight), Color.Black * .5f);
        DrawPanel(new(20, 4, 280, 172));
        var title = pausePage == PausePage.Main ? "Paused" : pausePage == PausePage.Audio ? "Sound"
            : pausePage == PausePage.Touch ? "Touch controls" : pausePage.ToString();
        font.Draw(batch, title, 30, 13, Accent, 2);
        var hint = menuMessage.Length == 0 ? "TAP TO SELECT / SWIPE TO SCROLL" : menuMessage.Split('\n')[0];
        font.Draw(batch, hint.Length > 43 ? hint[..43] : hint, 30, 32, Muted);
        var items = CurrentPauseItems;
        var first = Math.Clamp(selected - 3, 0, Math.Max(0, items.Length - 6));
        for (var row = 0; row < Math.Min(6, items.Length); row++)
        {
            var index = first + row;
            var bounds = new Rectangle(26, 47 + row * 18, 268, 18);
            if (index == selected) Fill(bounds, new Color(64, 73, 87));
            font.Draw(batch, items[index], 32, bounds.Y + 5, index == selected ? Accent : Ink);
            TouchChoice(bounds, index);
            var slider = pausePage == PausePage.Audio && index is 1 or 2 or 4 || pausePage == PausePage.Touch && index < 2;
            if (slider)
            {
                var value = pausePage == PausePage.Touch ? index == 0 ? (profile.Data.TouchSize - .8f) / .5f
                    : (profile.Data.TouchOpacity - .2f) / .8f : index == 1 ? audio.Volume : index == 2 ? audio.EffectsVolume : profile.Data.MusicVolume;
                var track = new Rectangle(188, bounds.Y + 1, 96, 16);
                var percent = pausePage == PausePage.Touch ? index == 0 ? profile.Data.TouchSize : profile.Data.TouchOpacity : value;
                font.Draw(batch, $"{MathF.Round(percent * 100)}%", 158, bounds.Y + 5, Accent);
                Fill(new(track.X, track.Center.Y - 1, track.Width, 3), Muted * .35f);
                Fill(new(track.X, track.Center.Y - 1, (int)(track.Width * value), 3), Accent);
                Fill(new(track.X + (int)((track.Width - 4) * value), track.Y + 3, 4, 10), Ink);
                TouchTarget(track, default, index, slider: true);
            }
            else
            {
                var value = PauseValue(index);
                font.Draw(batch, value, 282 - value.Length * 6, bounds.Y + 5, Muted);
            }
        }
        DrawTouchNavigation(pausePage is PausePage.Audio or PausePage.Touch);
    }

    private void DrawTouchKeyboard(Overlay current)
    {
        font.Draw(batch, current.Lines[0].Length > 42 ? current.Lines[0][^42..] : current.Lines[0], 24, 39, Ink);
        for (var i = 2; i < current.Options.Length; i++)
        {
            var bounds = new Rectangle(24 + (i - 2) % 10 * 27, 57 + (i - 2) / 10 * 21, 25, 19);
            Fill(bounds, i == current.Selected ? new Color(83, 84, 70) : new Color(45, 57, 72));
            var label = current.Options[i].Label == "Space" ? "_" : current.Options[i].Label;
            font.Draw(batch, label, bounds.Center.X - 3, bounds.Center.Y - 3, Ink);
            TouchChoice(bounds, i);
        }
        DrawTouchButton(new(24, 146, 78, 22), "DONE", TouchConfirm);
        TouchChoice(new(24, 146, 78, 22), 0);
        DrawTouchButton(new(108, 146, 90, 22), "DELETE", TouchConfirm);
        TouchChoice(new(108, 146, 90, 22), 1);
        DrawTouchButton(new(204, 146, 90, 22), "BACK", TouchBack);
    }

    private void BeginTouchDraw()
    {
        if (!touchEnabled) return;
        touchCanvas = CanvasDestination();
        touchInput.Regions.Clear(); touchInput.Pad = Rectangle.Empty;
        touchInput.Scale = touchCanvas.Width / (float)CanvasWidth;
        touchInput.CanSwipe = paused || overlay is { Keyboard: false } || mapOpen;
    }

    private void DrawTouchControls()
    {
        if (!touchEnabled || controllerConnected || titleScreen || paused || overlay is not null || mapOpen
            || studioSplashRemaining > 0 || saving is not null || loading is not null) return;
        var viewport = GraphicsDevice.Viewport;
        var scale = touchInput.Scale;
        var size = scale * profile.Data.TouchSize;
        var margin = (int)(8 * scale);
        var left = (int)touchInsets.X + margin;
        var right = viewport.Width - (int)touchInsets.Z - margin;
        var top = (int)touchInsets.Y + margin;
        var bottom = viewport.Height - (int)touchInsets.W - margin;
        var padSize = (int)(54 * size);
        var pad = new Rectangle(left, bottom - padSize, padSize, padSize);
        touchInput.Pad = pad;
        var cell = padSize / 3;
        var opacity = profile.Data.TouchOpacity;
        batch.Begin(samplerState: SamplerState.PointClamp);
        for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
            {
                var bounds = new Rectangle(pad.X + (x + 1) * cell + 1, pad.Y + (y + 1) * cell + 1, cell - 2, cell - 2);
                Fill(bounds, (touchInput.HeldDirection == new Point(x, y) && (x != 0 || y != 0) ? Accent : Panel) * opacity);
                if ((x == 0) == (y == 0)) continue;
                var label = y < 0 ? "^" : y > 0 ? "V" : x < 0 ? "<" : ">";
                var glyphScale = Math.Max(1, (int)scale);
                font.Draw(batch, label, bounds.Center.X - 3 * glyphScale, bounds.Center.Y - 3 * glyphScale, Ink * opacity, glyphScale);
            }
        Button(new(right - (int)(36 * size), bottom - (int)(30 * size), (int)(36 * size), (int)(30 * size)), "ACT", TouchConfirm);
        Button(new(right - (int)(70 * size), bottom - (int)(22 * size), (int)(30 * size), (int)(22 * size)), "BACK", TouchBack);
        Button(new(right - (int)(34 * scale), top, (int)(34 * scale), (int)(20 * scale)), "MENU", new(default, false, false, true));
        Button(new(right - (int)(74 * scale), top, (int)(34 * scale), (int)(20 * scale)), "MAP", new(default, false, false, false, true));
        batch.End();

        void Button(Rectangle bounds, string label, InputFrame action)
        {
            Fill(bounds, Accent * opacity);
            var inner = bounds; inner.Inflate(-(int)scale, -(int)scale);
            Fill(inner, Panel * opacity);
            var glyphScale = Math.Max(1, (int)scale);
            font.Draw(batch, label, bounds.Center.X - label.Length * 3 * glyphScale, bounds.Center.Y - 3 * glyphScale, Ink * Math.Min(1, opacity + .2f), glyphScale);
            touchInput.Regions.Add(new(bounds, action));
        }
    }
}
