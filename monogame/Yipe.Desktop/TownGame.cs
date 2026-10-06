using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame : Game
{
    private const int CanvasWidth = 320;
    private const int CanvasHeight = 180;
    private static readonly Color Background = new(20, 25, 36);
    private static readonly Color Panel = new(37, 43, 59);
    private static readonly Color Ink = new(241, 220, 158);
    private static readonly Color Muted = new(164, 173, 176);
    private static readonly Color Accent = new(217, 172, 79);
    private static readonly string[] Menu = ["Continue", "Save game", "Load game", "New adventure", "Fullscreen", "Scanlines", "Miniature effect", "Vignette", "Pixel lighting", "Quit"];
    private static readonly string[] MiniatureModes = ["Off", "Subtle", "Strong"];
    private readonly GraphicsDeviceManager graphics;
    private readonly GameInput input = new();
    private readonly PlayerMotion player = new(new(5, 5));
    private readonly bool smoke;
    private readonly string? selectedSave;
    private readonly string? screenshot;
    private SpriteBatch batch = null!;
    private PixelFont font = null!;
    private Texture2D pixel = null!;
    private Texture2D vignetteMask = null!;
    private RenderTarget2D canvas = null!;
    private MiniatureEffect miniature = null!;
    private HeatEffect heat = null!;
    private PixelLighting lighting = null!;
    private TownMap map = null!;
    private GameSession session = null!;
    private MapCatalog catalog = null!;
    private string contentDirectory = null!;
    private GameSaveStore saves = null!;
    private TownRenderer town = null!;
    private WorldRenderer world = null!;
    private GameAudio audio = null!;
    private DesktopProfile profile = null!;
    private ItemNames itemNames = new();
    private Adventure adventure = null!;
    private Overlay? overlay;
    private MapJournal journal = new();
    private RenderTarget2D mapView = null!;
    private bool mapOpen;
    private bool worldMapOpen;
    private int mapLayer;
    private IEnumerator<AdventureEvent>? command;
    private AdventureEvent? pendingEvent;
    private Map renderedMap = null!;
    private double tickRemaining = .5;
    private Point pendingDirection;
    private Vector2 cameraPosition;
    private double sceneTime;
    private float pauseAmount;
    private float selectionY;
    private string message = "";
    private double messageRemaining;
    private string menuMessage = "";
    private bool paused;
    private bool scanlines = true;
    private int miniatureMode = 2;
    private bool vignette;
    private bool pixelLighting = true;
    private bool controllerConnected;
    private int selected;
    private int smokeFrames;
    private Task? saving;
    private Task<GameSession>? loading;

    public TownGame(bool smoke = false, string? screenshot = null, string? selectedSave = null)
    {
        this.smoke = smoke;
        this.selectedSave = selectedSave;
        this.screenshot = screenshot;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = CanvasWidth * 4,
            PreferredBackBufferHeight = CanvasHeight * 4,
            SynchronizeWithVerticalRetrace = !smoke,
            HardwareModeSwitch = false,
        };
        Window.Title = "Yipe! III | Enter / A: actions | Esc / Start: pause";
        Window.AllowUserResizing = true;
        Window.TextInput += (_, e) => { if (IsActive && !paused) textReceiver?.Invoke(e.Character); };
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60);
        Deactivated += (_, _) => paused = true;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = new PixelFont(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([Color.White]);
        vignetteMask = new Texture2D(GraphicsDevice, CanvasWidth, CanvasHeight);
        var vignettePixels = new Color[CanvasWidth * CanvasHeight];
        for (var y = 0; y < CanvasHeight; y++)
            for (var x = 0; x < CanvasWidth; x++)
            {
                var dx = (x + .5f) / CanvasWidth * 2 - 1;
                var dy = (y + .5f) / CanvasHeight * 2 - 1;
                var falloff = Math.Clamp(MathF.Sqrt(dx * dx + dy * dy) - .4f, 0, 1);
                vignettePixels[y * CanvasWidth + x] = Color.Black * (.42f * MathHelper.SmoothStep(0, 1, falloff));
            }
        vignetteMask.SetData(vignettePixels);
        canvas = new RenderTarget2D(GraphicsDevice, CanvasWidth, CanvasHeight);
        miniature = new MiniatureEffect(GraphicsDevice, CanvasWidth, CanvasHeight);
        heat = new HeatEffect(GraphicsDevice, CanvasWidth, CanvasHeight);
        lighting = new PixelLighting(GraphicsDevice, pixel, CanvasWidth, CanvasHeight);
        mapView = new RenderTarget2D(GraphicsDevice, Map.Width * 32, Map.Height * 32);
        contentDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        using (var stream = File.OpenRead(Path.Combine(contentDirectory, "maps.json")))
            catalog = MapCatalog.Load(stream);
        var itemPath = Path.Combine(contentDirectory, "Yipe3Items.txt");
        try { using var reader = File.OpenText(itemPath); itemNames = ItemNames.Load(reader); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine("Using embedded equipment names: " + error.Message); }
        session = new GameSession(catalog);
        map = new TownMap(session.CurrentMap);
        adventure = new Adventure(session);
        renderedMap = session.CurrentMap;
        journal.Observe(adventure);
        saves = new GameSaveStore(selectedSave ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YipeIII-MonoGame", "game.y3m"), catalog);

        town = new TownRenderer(GraphicsDevice, map, contentDirectory, catalog);
        world = new WorldRenderer(GraphicsDevice, contentDirectory, catalog);
        audio = new GameAudio(contentDirectory);
        profile = new DesktopProfile(selectedSave is not null ? Path.ChangeExtension(selectedSave, ".profile.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YipeIII-MonoGame", "profile.json"));
        scanlines = profile.Data.Scanlines; miniatureMode = profile.Data.Miniature;
        vignette = profile.Data.Vignette;
        pixelLighting = profile.Data.PixelLighting;
        audio.Enabled = profile.Data.Sound; audio.Volume = profile.Data.Volume;
        cameraPosition = CameraTarget(player.VisualPosition);
        if (!smoke && selectedSave is null) ShowWelcome();
        if (selectedSave is not null)
        {
            paused = true;
            menuMessage = "Loading...";
            loading = saves.LoadAsync();
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var elapsed = gameTime.ElapsedGameTime.TotalSeconds;
        var pad = default(GamePadState);
        for (var index = 0; index < 4; index++)
        {
            var candidate = GamePad.GetState((PlayerIndex)index);
            if (!candidate.IsConnected) continue;
            pad = candidate;
            break;
        }
        if (controllerConnected && !pad.IsConnected)
        {
            paused = true;
            menuMessage = "Controller disconnected.\nReconnect or use the keyboard.";
        }
        controllerConnected = pad.IsConnected;
        var actions = input.Update(Keyboard.GetState(), pad, elapsed, IsActive,
            repeatInterval: paused || overlay is not null || mapOpen ? 0.12 : PlayerMotion.StepDuration, textInput: textReceiver is not null);

        audio.Update(IsActive);
        FinishStorage();
        if (paused || overlay is not null || mapOpen || !IsActive) { pendingDirection = Point.Zero; tickRemaining = .512; }
        pauseAmount = MathHelper.Lerp(pauseAmount, paused ? 1 : 0, 1 - MathF.Exp(-(float)elapsed * 22));
        selectionY = MathHelper.Lerp(selectionY, selected * 10, 1 - MathF.Exp(-(float)elapsed * 25));
        if (!IsActive || saving is not null || loading is not null) return;

        if (paused)
        {
            if (actions.Back || actions.Pause) { paused = false; return; }
            if (actions.Direction.Y != 0)
                selected = (selected + actions.Direction.Y + Menu.Length) % Menu.Length;
            if (actions.Confirm) ActivateMenu();
        }
        else if (mapOpen) UpdateMap(actions);
        else if (actions.Map && overlay is null && command is null) OpenMap();
        else if (actions.Pause && !actions.Back || overlay is null && (actions.Pause || actions.Back))
        {
            paused = true;
            pendingDirection = Point.Zero;
            selected = 0;
            selectionY = 0;
            menuMessage = "";
        }
        else if (overlay is not null)
        {
            UpdateOverlay(actions);
        }
        else
        {
            sceneTime += elapsed;
            player.Update(elapsed);
            cameraPosition = Vector2.Lerp(cameraPosition, CameraTarget(player.VisualPosition), 1 - MathF.Exp(-(float)elapsed * 14));
            messageRemaining = Math.Max(0, messageRemaining - elapsed);
            if (!player.IsMoving && pendingEvent is not null)
            {
                var next = pendingEvent; pendingEvent = null;
                ShowInteraction(next);
            }
            else if (actions.Confirm && !player.IsMoving) ShowActions();
            else
            {
                if (actions.Direction != Point.Zero) pendingDirection = actions.Direction;
                if (!player.IsMoving && pendingDirection != Point.Zero)
                {
                    var direction = pendingDirection;
                    pendingDirection = Point.Zero;
                    player.Face(direction);
                    StartCommand(adventure.Move(direction.X, direction.Y, DateTime.Now.Hour));
                }
                tickRemaining -= elapsed;
                if (tickRemaining <= 0 && overlay is null && command is null)
                {
                    tickRemaining = .512;
                    StartCommand(adventure.Tick());
                }
            }
        }
        base.Update(gameTime);
    }

    private static Vector2 CameraTarget(Vector2 position) => Vector2.Clamp(
        position * 32 + new Vector2(16) - new Vector2(CanvasWidth, CanvasHeight) / 2,
        Vector2.Zero, new Vector2(TownMap.Width * 32 - CanvasWidth, TownMap.Height * 32 - CanvasHeight));

    private void RestorePosition(MapPosition position)
    {
        session.Location.Position = position;
        player.SnapTo(position);
        cameraPosition = CameraTarget(player.VisualPosition);
        pendingDirection = Point.Zero;
        messageRemaining = 0;
    }

    private void ActivateMenu()
    {
        switch (selected)
        {
            case 0: paused = false; break;
            case 1:
                if (command is not null || textReceiver is not null) { menuMessage = "Finish this interaction\nbefore saving."; break; }
                menuMessage = "Saving..."; saving = saves.SaveAsync(session, journal); break;
            case 2: menuMessage = "Loading..."; loading = saves.LoadAsync(); break;
            case 3: paused = false; mapOpen = false; ShowNewGameConfirmation(); break;
            case 4:
                graphics.IsFullScreen = !graphics.IsFullScreen;
                graphics.PreferredBackBufferWidth = graphics.IsFullScreen
                    ? GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width : CanvasWidth * 4;
                graphics.PreferredBackBufferHeight = graphics.IsFullScreen
                    ? GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height : CanvasHeight * 4;
                graphics.ApplyChanges();
                break;
            case 5: scanlines = !scanlines; SavePreferences(); break;
            case 6: miniatureMode = (miniatureMode + 1) % MiniatureModes.Length; SavePreferences(); break;
            case 7: vignette = !vignette; SavePreferences(); break;
            case 8: pixelLighting = !pixelLighting; SavePreferences(); break;
            case 9: Exit(); break;
        }
    }

    private void FinishStorage()
    {
        if (saving?.IsCompleted == true)
        {
            try { saving.GetAwaiter().GetResult(); menuMessage = saves.JournalWarning.Length == 0 ? "Your game is saved." : "Game saved. Map notes\ncould not be saved."; }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(error.Message);
                menuMessage = "Could not save. Your current\ngame is still here.";
            }
            saving = null;
        }
        if (loading?.IsCompleted == true)
        {
            try
            {
                var next = loading.GetAwaiter().GetResult();
                command?.Dispose(); command = null; pendingEvent = null; overlay = null; textReceiver = null;
                session = next;
                journal = saves.RestoredJournal; mapOpen = false;
                adventure = new Adventure(session);
                SynchronizeWorld(animate: false);
                tickRemaining = .5;
                StartCommand(adventure.Resume());
                if (Hero.GatekeeperState == 3) RecordScores();
                menuMessage = saves.JournalWarning.Length == 0 ? "Saved game restored." : "Game restored. Previous map\nnotes are unavailable.";
            }
            catch (FileNotFoundException) { menuMessage = "No saved game yet."; }
            catch (DirectoryNotFoundException) { menuMessage = "No saved game yet."; }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(error.Message);
                menuMessage = "Could not load this save.\nYour current game is unchanged.";
            }
            loading = null;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        if (mapOpen && !worldMapOpen) RenderAreaMap();
        // Round both axes together: one world pixel always occupies one canvas pixel.
        var camera = Matrix.CreateTranslation(-MathF.Round(cameraPosition.X), -MathF.Round(cameraPosition.Y), 0);
        if (pixelLighting) lighting.Prepare(batch, adventure, camera, sceneTime);
        GraphicsDevice.SetRenderTarget(miniatureMode == 0 && !vignette ? canvas : miniature.World);
        GraphicsDevice.Clear(Background);
        batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera);
        if (session.CurrentMap.Id == TownMap.Id) town.Draw(batch, player, sceneTime, miniatureMode != 0);
        else world.Draw(batch, adventure, player, town, sceneTime, miniatureMode != 0);
        if (pixelLighting) lighting.DrawTorches(batch, adventure.Visibility, sceneTime);
        batch.End();
        if (pixelLighting) lighting.Apply(batch);

        Texture2D? presentedWorld = miniatureMode == 0 && vignette ? miniature.World : null;
        var hell = session.CurrentMap.Id is >= 117 and <= 120;
        if (miniatureMode != 0)
        {
            var focusY = player.VisualPosition.Y * 32 + 12 - cameraPosition.Y;
            // Hell keeps its contact shadows, but its pixel art must not be blurred.
            miniature.Apply(batch, focusY, miniatureMode == 2, hell ? 0f : 1f);
            presentedWorld = miniature.Result;
        }
        if (session.CurrentMap.Id != TownMap.Id && world.HasHeat)
            presentedWorld = heat.Apply(batch, presentedWorld ?? canvas, world,
                new(MathF.Round(cameraPosition.X), MathF.Round(cameraPosition.Y)),
                player.VisualPosition * 32 + new Vector2(16, 12), sceneTime, hell);
        if (presentedWorld is not null)
        {
            GraphicsDevice.SetRenderTarget(canvas);
            GraphicsDevice.Clear(Color.Transparent);
        }

        batch.Begin(samplerState: SamplerState.PointClamp);
        if (!paused && messageRemaining > 0)
        {
            var y = player.VisualPosition.Y * 32 + 25 - cameraPosition.Y > 120 ? 8 : 142;
            DrawPanel(new(12, y, 296, 28));
            font.Draw(batch, message, 20, y + 5, Ink);
        }

        if (mapOpen) DrawMapOverlay();
        else if (overlay is not null) DrawOverlay();

        if (pauseAmount > 0.005f)
        {
            var offset = (int)MathF.Round((1 - pauseAmount) * 4);
            Fill(new(0, 0, CanvasWidth, CanvasHeight), Color.Black * 0.5f);
            DrawPanel(new(32, 4 + offset, 256, 172));
            font.Draw(batch, "Paused", 46, 14 + offset, Accent, 2);
            font.Draw(batch, controllerConnected ? "EAST: BACK" : "ESC: BACK", 210, 19 + offset, Muted);
            font.Draw(batch, $"{LocationName()} / {player.Position.X}, {player.Position.Y}", 46, 34 + offset, Muted);
            var rowY = 42 + (int)MathF.Round(selectionY) + offset;
            Fill(new(44, rowY, 232, 9), new Color(64, 73, 87));
            Fill(new(49, rowY + 3, 1, 5), Accent);
            Fill(new(50, rowY + 4, 1, 3), Accent);
            Fill(new(51, rowY + 5, 1, 1), Accent);
            for (var i = 0; i < Menu.Length; i++)
            {
                var label = i switch
                {
                    4 => $"Fullscreen: {(graphics.IsFullScreen ? "on" : "off")}",
                    5 => $"Scanlines: {(scanlines ? "on" : "off")}",
                    6 => $"Miniature effect: {MiniatureModes[miniatureMode]}",
                    7 => $"Vignette: {(vignette ? "on" : "off")}",
                    8 => $"Pixel lighting: {(pixelLighting ? "on" : "off")}",
                    _ => Menu[i],
                };
                font.Draw(batch, label, 60, 44 + i * 10 + offset, i == selected ? Accent : Ink);
            }
            font.Draw(batch, menuMessage, 46, 145 + offset, Ink);
            font.Draw(batch, controllerConnected ? "D-PAD/STICK MOVE  SOUTH SELECT" : "ARROWS/WASD MOVE  ENTER SELECT", 46, 166 + offset, Muted);
        }
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        var viewport = GraphicsDevice.Viewport;
        var scale = Math.Min(viewport.Width / (float)CanvasWidth, viewport.Height / (float)CanvasHeight);
        // Whole-number enlargement keeps every pixel the same size at normal window sizes.
        if (scale >= 1) scale = MathF.Floor(scale);
        var width = (int)(CanvasWidth * scale);
        var height = (int)(CanvasHeight * scale);
        batch.Begin(samplerState: SamplerState.PointClamp);
        var destination = new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
        if (presentedWorld is not null) batch.Draw(presentedWorld, destination, Color.White);
        if (vignette)
        {
            batch.End();
            // Smooth only the darkening mask, keeping world pixels and the UI sharp.
            batch.Begin(samplerState: SamplerState.LinearClamp);
            batch.Draw(vignetteMask, destination, Color.White);
            batch.End();
            batch.Begin(samplerState: SamplerState.PointClamp);
        }
        batch.Draw(canvas, destination, Color.White);
        if (scanlines && scale >= 2)
            for (var y = 0; y < CanvasHeight; y++)
                Fill(new(destination.X, destination.Y + (int)((y + 1) * scale) - 1, width, 1), Color.Black * 0.12f);
        batch.End();

        if (smoke && ++smokeFrames == 3)
        {
            var rendered = presentedWorld ?? canvas;
            var pixels = new Color[rendered.Width * rendered.Height];
            rendered.GetData(pixels);
            if (pixels.Distinct().Take(16).Count() < 16)
                throw new InvalidOperationException("The graphics smoke test did not render the town.");
            if (screenshot is not null)
            {
                using var stream = File.Create(screenshot);
                rendered.SaveAsPng(stream, rendered.Width, rendered.Height);
            }
            Exit();
        }
        base.Draw(gameTime);
    }

    private void Fill(Rectangle rectangle, Color color) => batch.Draw(pixel, rectangle, color);

    private void DrawPanel(Rectangle bounds)
    {
        Fill(bounds, Background);
        bounds.Inflate(-1, -1);
        Fill(bounds, Accent);
        bounds.Inflate(-1, -1);
        Fill(bounds, Panel);
        Fill(new(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, 1), Ink);
    }

    protected override void UnloadContent()
    {
        command?.Dispose();
        audio?.Dispose();
        world?.Dispose();
        town?.Dispose();
        miniature?.Dispose();
        heat?.Dispose();
        lighting?.Dispose();
        mapView?.Dispose();
        canvas?.Dispose();
        vignetteMask?.Dispose();
        pixel?.Dispose();
        font?.Dispose();
        batch?.Dispose();
    }

    protected override void EndRun()
    {
        // Let an in-flight desktop write finish if the OS closes the window during saving.
        if (saving is not null)
        {
            try { saving.GetAwaiter().GetResult(); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            { Console.Error.WriteLine(error.Message); }
        }
        base.EndRun();
    }
}
