using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private static readonly string[] TitleMenu = ["New adventure", "Load game", "Options", "Quit"];
    private bool titleScreen;
    private int titleSelected;
    private double titleTime;
    private TownRenderer? titleTown;
    private Map? titleMap;
    private string adventureDirectory = "";
    private GameSaveStore? loadingStore;

    private void ShowWelcome()
    {
        command?.Dispose(); command = null; pendingEvent = null;
        overlay = null; textReceiver = null; restorePauseOverlay = null; soundMenuParent = null;
        mapOpen = false; paused = false; pauseAmount = 0;
        titleScreen = true; titleSelected = 0; menuMessage = "";
        pausePage = PausePage.Main; selected = 0; selectionY = 0;
        audio.StopFootsteps();
    }

    private void UpdateTitleMenu(InputFrame actions)
    {
        if (actions.Direction.Y != 0)
            titleSelected = (titleSelected + actions.Direction.Y + TitleMenu.Length) % TitleMenu.Length;
        if (!actions.Confirm) return;
        switch (titleSelected)
        {
            case 0: BeginCharacterCreation(); break;
            case 1: ShowLoadGames(); break;
            case 2: OpenPausePage(PausePage.Options); break;
            case 3: Exit(); break;
        }
    }

    private void ShowReturnToTitle()
    {
        restorePauseOverlay?.Invoke(); restorePauseOverlay = null;
        var previous = overlay; var receiver = textReceiver; var wasMapOpen = mapOpen;
        paused = false; pauseAmount = 0; mapOpen = false; textReceiver = null;
        void Stay() { overlay = previous; textReceiver = receiver; mapOpen = wasMapOpen; paused = true; }
        Choices("Return to title?", "Unsaved progress will be lost. Your saved adventures will be kept.",
            [new("Keep playing", Stay), new("Return to title", ShowWelcome)], Stay);
    }

    private void ShowLoadGames()
    {
        var options = new List<OverlayOption>();
        try
        {
            var paths = Directory.Exists(adventureDirectory)
                ? Directory.EnumerateFiles(adventureDirectory, "*.y3m").ToList() : [];
            var slots = Path.Combine(adventureDirectory, "Saved Adventures");
            if (Directory.Exists(slots))
                foreach (var directory in Directory.EnumerateDirectories(slots))
                    paths.AddRange(Directory.EnumerateFiles(directory, "*.y3m"));
            // The explicitly selected adventure may live outside the ordinary library.
            if (File.Exists(saves.FilePath) && !paths.Contains(saves.FilePath)) paths.Add(saves.FilePath);
            foreach (var path in paths.OrderByDescending(File.GetLastWriteTimeUtc))
            {
                var label = Path.GetFileNameWithoutExtension(path) == "game"
                    ? Path.GetFileName(Path.GetDirectoryName(path)!) : Path.GetFileNameWithoutExtension(path);
                try
                {
                    if (new FileInfo(path).Length <= GameSaveCodec.MaximumFileSize)
                    {
                        var saved = GameSaveCodec.Decode(File.ReadAllBytes(path), catalog);
                        var name = saved.Player.Name.Length > 18 ? saved.Player.Name[..18] : saved.Player.Name;
                        label = $"{name} - L{saved.Player.Level} - {File.GetLastWriteTime(path):MM/dd HH:mm}";
                    }
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                { label = "Unavailable: " + label; }
                if (label.Length > 39) label = label[..36] + "...";
                options.Add(new(label, () =>
                {
                    loadingStore = new GameSaveStore(path, catalog);
                    menuMessage = "Loading..."; loading = loadingStore.LoadAsync();
                }));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { menuMessage = "Could not read the saved adventures."; Console.Error.WriteLine(error.Message); }
        var prompt = menuMessage.Length != 0 ? menuMessage
            : options.Count == 0 ? "No saved adventures yet. Choose New adventure on the title screen."
            : "Choose an adventure to continue.";
        options.Add(new("Back", () => { overlay = null; menuMessage = ""; }));
        Choices("Load game", prompt, options, () => { overlay = null; menuMessage = ""; });
    }

    private void DrawTitleScreen()
    {
        titleMap ??= catalog.CreateMap(TownMap.Id)!;
        titleTown ??= new TownRenderer(GraphicsDevice, new TownMap(titleMap), contentDirectory, catalog);
        // A separate clock and scenery renderer never tick or relocate the live adventure.
        var camera = new Vector2(80 + (int)MathF.Round(12 * MathF.Sin((float)titleTime / 22)),
            6 + (int)MathF.Round(6 * MathF.Sin((float)titleTime / 29)));
        sunShafts.Daylight = 1;
        if (sunShaftStrength > 0) sunShafts.Prepare(batch, camera, titleTime, cloudShadowStrength);
        GraphicsDevice.SetRenderTarget(miniature.World);
        GraphicsDevice.Clear(Background);
        batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(-camera.X, -camera.Y, 0));
        titleTown.Draw(batch, null, titleTime, miniatureMode != 0, 0, world);
        batch.End();
        sunShafts.DrawClouds(batch, titleMap, camera, titleTime, cloudShadowStrength);
        sunShafts.Draw(batch, titleMap, camera, titleTime, sunShaftStrength, cloudShadowStrength);
        if (miniatureMode != 0) miniature.Apply(batch, 90, miniatureMode == 2);
        GraphicsDevice.SetRenderTarget(canvas);
        GraphicsDevice.Clear(Background);
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(miniatureMode == 0 ? miniature.World : miniature.Result, new Rectangle(0, 0, CanvasWidth, CanvasHeight), Color.White);
        // A quiet, stepped shade leaves the village visible beside the title and menu.
        for (var x = 0; x < CanvasWidth; x += 8)
            Fill(new(x, 0, 8, CanvasHeight), Background * (.65f - .42f * x / CanvasWidth));
        if (vignette) batch.Draw(vignetteMask, Vector2.Zero, Color.White);
        if (overlay is not null) DrawOverlay();
        else if (!paused)
        {
            font.Draw(batch, "ICOSIUM LABS", 30, 14, Muted);
            font.Draw(batch, "QUEST OF", 31, 34, Ink);
            font.Draw(batch, "YIPE! III", 31, 47, Background, 3);
            font.Draw(batch, "YIPE! III", 29, 45, Accent, 3);
            Fill(new(30, 72, 158, 1), Accent * .65f);
            var rowHeight = touchEnabled ? 17 : 14;
            DrawPanel(new(24, 84, 172, touchEnabled ? 74 : 66));
            Fill(new(30, 89 + titleSelected * rowHeight, 160, rowHeight - 1), new Color(64, 73, 87));
            for (var i = 0; i < TitleMenu.Length; i++)
            {
                font.Draw(batch, (i == titleSelected ? "> " : "  ") + TitleMenu[i], 37, 92 + i * rowHeight, i == titleSelected ? Accent : Ink);
                TouchChoice(new(30, 89 + i * rowHeight, 160, rowHeight), i);
            }
            font.Draw(batch, controllerConnected ? "SOUTH: SELECT" : touchEnabled ? "TAP TO SELECT" : "ENTER: SELECT", 30, touchEnabled ? 164 : 158, Muted);
        }
        if (paused) DrawPauseMenu();
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        var destination = CanvasDestination();
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(canvas, destination, Color.White);
        var scale = destination.Width / (float)CanvasWidth;
        if (scanlines && scale >= 2)
            for (var y = 0; y < CanvasHeight; y++)
                Fill(new(destination.X, destination.Y + (int)((y + 1) * scale) - 1, destination.Width, 1), Color.Black * .12f);
        batch.End();
    }
}
