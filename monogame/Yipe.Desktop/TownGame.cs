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
    private static readonly string[] EffectStrengths = ["Off", "Subtle", "Strong"];
    private static readonly string[] TimesOfDay = ["Auto", "Day", "Dusk", "Night"];
    private readonly GraphicsDeviceManager graphics;
    private readonly GameInput input = new();
    private readonly PlayerMotion player = new(new(5, 5));
    private readonly bool smoke;
    private readonly string? selectedSave;
    private readonly string? screenshot;
    private SpriteBatch batch = null!;
    private PixelFont font = null!;
    private Texture2D pixel = null!;
    private Texture2D studioLogo = null!;
    private const double StudioSplashDuration = 3.2;
    private double studioSplashRemaining;
    private Texture2D vignetteMask = null!;
    private RenderTarget2D canvas = null!;
    private MiniatureEffect miniature = null!;
    private HeatEffect heat = null!;
    private PixelLighting lighting = null!;
    private SunShafts sunShafts = null!;
    private TownMap map = null!;
    private GameSession session = null!;
    private MapCatalog catalog = null!;
    private readonly string contentDirectory;
    private readonly string dataDirectory;
    private GameSaveStore saves = null!;
    private TownRenderer town = null!;
    private WorldRenderer world = null!;
    private GameAudio audio = null!;
    private GameMusic music = null!;
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
    private Point iceDirection;
    private PlayerStride nextStride;
    private Vector2 cameraPosition;
    private double sceneTime;
    private bool walkingStep;
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
    private int sunShaftStrength = 1;
    private int cloudShadowStrength = 1;
    private int rainStrength = 1;
    private double outdoorWeatherTime;
    private int timeOfDayMode;
    private double dayNightSeconds = DayNightCycle.OpeningTime;
    private DateTime dayNightUpdatedAt;
    private bool screenShake;
    private bool controllerConnected;
    private KeyboardState previousKeyboardState;
    private int selected;
    private int smokeFrames;
    private Task? saving;
    private Task<GameSession>? loading;

    public TownGame(bool smoke = false, string? screenshot = null, string? selectedSave = null)
        : this(smoke, screenshot, selectedSave, null, null) { }

    internal TownGame(bool smoke, string? screenshot, string? selectedSave, string? contentRoot, string? dataRoot)
    {
        contentDirectory = contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Content");
        dataDirectory = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YipeIII-MonoGame");
        this.smoke = smoke;
        this.selectedSave = selectedSave;
        this.screenshot = screenshot;
        graphics = new GraphicsDeviceManager(this)
        {
#if !ANDROID
            PreferredBackBufferWidth = CanvasWidth * 4,
            PreferredBackBufferHeight = CanvasHeight * 4,
#endif
            // SDL's macOS OpenGL display-link wait can stall before the first frame.
            // The fixed 60 Hz game loop below also paces presentation without VSync.
            SynchronizeWithVerticalRetrace = !smoke && !OperatingSystem.IsMacOS(),
            HardwareModeSwitch = false,
        };
#if ANDROID
        graphics.IsFullScreen = true;
        graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
#else
        Window.Title = "Yipe! III | Enter / A: actions | Esc / Start: pause";
        Window.AllowUserResizing = true;
        Window.TextInput += (_, e) => { if (IsActive && !paused) textReceiver?.Invoke(e.Character); };
        IsMouseVisible = true;
#endif
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60);
        Deactivated += (_, _) => { if (studioSplashRemaining <= 0 && !titleScreen) paused = true; };
        Exiting += (_, _) => { if (profile is not null) SavePreferences(); };
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
        sunShafts = new SunShafts(GraphicsDevice, CanvasWidth, CanvasHeight);
        mapView = new RenderTarget2D(GraphicsDevice, Map.Width * 32, Map.Height * 32);
        using (var stream = File.OpenRead(Path.Combine(contentDirectory, "Branding", "icosium-labs.png")))
            studioLogo = Texture2D.FromStream(GraphicsDevice, stream);
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
        saves = new GameSaveStore(selectedSave ?? Path.Combine(dataDirectory, "game.y3m"), catalog);

        adventureDirectory = Path.GetDirectoryName(saves.FilePath)!;
        var saveFolder = new DirectoryInfo(adventureDirectory);
        if (saveFolder.Parent?.Name == "Saved Adventures") adventureDirectory = saveFolder.Parent.Parent!.FullName;
        town = new TownRenderer(GraphicsDevice, map, contentDirectory, catalog);
        world = new WorldRenderer(GraphicsDevice, contentDirectory, catalog);
        lighting = new PixelLighting(GraphicsDevice, pixel, CanvasWidth, CanvasHeight, world);
        audio = new GameAudio(contentDirectory);
        music = new GameMusic(contentDirectory);
        profile = new DesktopProfile(selectedSave is not null ? Path.ChangeExtension(selectedSave, ".profile.json")
            : Path.Combine(dataDirectory, "profile.json"));
        scanlines = profile.Data.Scanlines; miniatureMode = profile.Data.Miniature;
        vignette = profile.Data.Vignette;
        pixelLighting = profile.Data.PixelLighting;
        sunShaftStrength = profile.Data.SunShaftStrength;
        cloudShadowStrength = profile.Data.CloudShadowStrength;
        rainStrength = profile.Data.RainStrength;
        screenShake = profile.Data.ScreenShake;
        timeOfDayMode = profile.Data.TimeOfDayMode;
        dayNightSeconds = profile.Data.DayNightSeconds;
        dayNightUpdatedAt = DateTime.UtcNow;
        audio.Enabled = profile.Data.Sound; audio.Volume = profile.Data.Volume;
        audio.EffectsVolume = profile.Data.EffectsVolume;
        cameraPosition = CameraTarget(player.VisualPosition);
        if (!smoke && selectedSave is null) studioSplashRemaining = StudioSplashDuration;
        if (selectedSave is not null)
        {
            paused = true;
            menuMessage = "Loading...";
            loading = saves.LoadAsync();
        }
    }

    protected override void Update(GameTime gameTime)
    {
        // Wall time keeps the cosmetic cycle running through menus, focus loss and suspension.
        // Advance before simulation's pause gates; never catch up movement or combat.
        var now = DateTime.UtcNow;
        if (timeOfDayMode == 0)
            dayNightSeconds = DayNightCycle.Advance(dayNightSeconds, Math.Max(0, (now - dayNightUpdatedAt).TotalSeconds));
        dayNightUpdatedAt = now;
        var elapsed = gameTime.ElapsedGameTime.TotalSeconds;
        var pad = default(GamePadState);
        for (var index = 0; index < 4; index++)
        {
            var candidate = GamePad.GetState((PlayerIndex)index);
            if (!candidate.IsConnected) continue;
            pad = candidate;
            break;
        }
        if (controllerConnected && !pad.IsConnected && studioSplashRemaining <= 0 && !titleScreen)
        {
            paused = true;
            menuMessage = touchEnabled ? "Controller disconnected.\nTouch controls are available." : "Controller disconnected.\nReconnect or use the keyboard.";
        }
        controllerConnected = pad.IsConnected;

        var keyboard = Keyboard.GetState();
        var touch = touchEnabled ? ReadTouch(Microsoft.Xna.Framework.Input.Touch.TouchPanel.GetState()) : default;
        var actions = input.Update(keyboard, pad, elapsed, IsActive,
            repeatInterval: titleScreen || paused || overlay is not null || mapOpen ? 0.12 : PlayerMotion.StepDuration, textInput: textReceiver is not null,
            controls: titleScreen || studioSplashRemaining > 0 || paused || overlay is not null || mapOpen ? null : profile.Data.Controls,
            touchDirection: touchEnabled ? touchInput.HeldDirection : default);
        if (touchEnabled) actions = new(actions.Direction != Point.Zero ? actions.Direction : touch.Direction,
            actions.Confirm || touch.Confirm, actions.Back || touch.Back, actions.Pause || touch.Pause, actions.Map || touch.Map);
#if ANDROID
        if (androidBack)
        {
            androidBack = false;
            actions = actions with { Back = true, Pause = true };
        }
#endif

        audio.Update(IsActive);
        UpdateCaveAmbience(elapsed);
        UpdateMusic(elapsed);
        if (titleScreen || paused || overlay is not null || mapOpen || saving is not null || loading is not null || studioSplashRemaining > 0)
            audio.StopFootsteps();
        FinishStorage();
        if (paused || overlay is not null || mapOpen || !IsActive || saving is not null || loading is not null)
        { pendingDirection = iceDirection = Point.Zero; tickRemaining = .512; }
        pauseAmount = MathHelper.Lerp(pauseAmount, paused ? 1 : 0, 1 - MathF.Exp(-(float)elapsed * 22));
        selectionY = MathHelper.Lerp(selectionY, selected * PauseRowHeight, 1 - MathF.Exp(-(float)elapsed * 25));
        if (!IsActive || saving is not null || loading is not null)
        { previousKeyboardState = keyboard; return; }
        if (studioSplashRemaining > 0)
        {
            previousKeyboardState = keyboard;
            UpdateStudioSplash(actions, elapsed);
            base.Update(gameTime);
            return;
        }
        if (titleScreen) titleTime += elapsed;
        if (bindingAction >= 0)
        {
            if (actions.Back || actions.Pause) overlay!.Back?.Invoke();
            else UpdateBinding(keyboard);
            previousKeyboardState = keyboard;
            base.Update(gameTime);
            return;
        }
        previousKeyboardState = keyboard;

        if (titleScreen)
        {
            if (paused) UpdatePauseMenu(actions);
            else if (restorePauseOverlay is not null && actions.Pause && !actions.Back) ReturnToPause();
            else if (overlay is not null) UpdateOverlay(actions);
            else UpdateTitleMenu(actions);
            base.Update(gameTime);
            return;
        }
        if (paused) UpdatePauseMenu(actions);
        else if (restorePauseOverlay is not null && actions.Pause && !actions.Back) ReturnToPause();
        else if (mapOpen) UpdateMap(actions);
        else if (actions.Map && overlay is null && command is null) OpenMap();
        else if (actions.Pause && !actions.Back || overlay is null && (actions.Pause || actions.Back))
        {
            paused = true;
            pausePage = PausePage.Main;
            pendingDirection = Point.Zero;
            selected = 0;
            selectionY = 0;
            menuMessage = "";
        }
        else if (overlay is not null)
        {
            if (overlay.Npc != NpcActor.None) overlay.NpcTime += elapsed;
            if (!UpdateCombatPresentation(elapsed)) UpdateOverlay(actions);
        }
        else
        {
            sceneTime += elapsed;
            if (rainStrength > 0 && session.CurrentMap.Id is >= 1 and <= 40)
                outdoorWeatherTime += elapsed;
            var wasMoving = player.IsMoving;
            world.UpdateRaft(elapsed);
            player.Update(elapsed, world.RaftSupport(adventure, sceneTime));
            if (walkingStep && wasMoving && !player.IsMoving)
            {
                walkingStep = false;
                world.SnowStep(session.CurrentMap, player, sceneTime);
                audio.Footstep(session.CurrentMap.Id == TownMap.Id ? town.FootstepAt(player.Position)
                    : world.FootstepAt(session.CurrentMap, player.Position));
            }
            world.UpdateMonsters(elapsed);
            cameraPosition = Vector2.Lerp(cameraPosition, CameraTarget(player.VisualPosition), 1 - MathF.Exp(-(float)elapsed * 14));
            messageRemaining = Math.Max(0, messageRemaining - elapsed);
            if (!player.IsMoving && !world.MonstersMoving && pendingEvent is not null)
            {
                var next = pendingEvent; pendingEvent = null;
                ShowInteraction(next);
            }
            else if (actions.Confirm && !player.IsMoving && !world.MonstersMoving) ShowActions();
            else
            {
                if (actions.Direction != Point.Zero) pendingDirection = actions.Direction;
                if (!player.IsMoving && !world.MonstersMoving && command is null && pendingEvent is null)
                {
                    var direction = pendingDirection;
                    pendingDirection = Point.Zero;
                    MovePlayer(direction, input.HeldDirection);
                }
                tickRemaining -= elapsed;
                if (tickRemaining <= 0 && overlay is null && command is null)
                {
                    tickRemaining = .512;
                    StartCommand(adventure.Tick());
                }
            }
        }
        sunShafts.Daylight = DayNightCycle.At(dayNightSeconds, timeOfDayMode).Daylight;
        sunShafts.Update(session.CurrentMap, player, sceneTime, sunShaftStrength, cloudShadowStrength);
        if (paused || overlay is not null || mapOpen || pendingEvent is not null)
        { iceDirection = Point.Zero; audio.StopFootsteps(); }
        UpdateCaveAmbience(0);
        base.Update(gameTime);
    }

    private void UpdateMusic(double elapsed)
    {
        var track = titleScreen ? MusicTrack.Town : session.CurrentMap.Id switch
        {
            8 or 17 or 27 => MusicTrack.Town,
            >= 1 and <= 36 => MusicTrack.Overworld,
            _ when WorldRenderer.IsIce(session.CurrentMap) || WorldRenderer.IsWaterCave(session.CurrentMap) => MusicTrack.CrystalCave,
            _ => MusicTrack.Cave,
        };
        if (!titleScreen && adventure.Battle is not null) track = MusicTrack.Battle;
        if (studioSplashRemaining > 0 || !titleScreen && Hero.Health <= 0)
            track = MusicTrack.None;
        var intensity = track is MusicTrack.Cave or MusicTrack.CrystalCave ? .7f : 1f;
        if (paused || overlay is { Combat: false } || mapOpen) intensity *= .65f;
        if (overlay?.Presentation is { Turn.Outcome: CombatOutcome.Victory }) intensity *= .5f;
        music.Update(track, IsActive && audio.Enabled && profile.Data.Music,
            audio.Volume * profile.Data.MusicVolume, intensity, elapsed);
    }

    private void UpdateCaveAmbience(double elapsed)
    {
        var playing = !titleScreen && IsActive && !paused && overlay is null && !mapOpen && saving is null && loading is null && studioSplashRemaining <= 0;
        if (session.CurrentMap.Id == TownMap.Id)
        {
            audio.StopOutdoorNature();
            var water = town.RiverSoundAt(player.VisualPosition);
            audio.UpdateTownAmbience(DayNightCycle.At(dayNightSeconds, timeOfDayMode).Daylight, playing, elapsed);
            audio.UpdateRiverAmbience(water.Gain, water.Pan, playing, elapsed);
            return;
        }
        if (session.CurrentMap.Id is >= 1 and <= 36)
        {
            var habitat = world.OutdoorHabitatAt(session.CurrentMap, player.VisualPosition);
            var water = world.OutdoorWaterSoundAt(session.CurrentMap, player.VisualPosition);
            var daylight = DayNightCycle.At(dayNightSeconds, timeOfDayMode).Daylight;
            var rain = rainStrength == 0 ? 0 : Rain.WeatherAmount(outdoorWeatherTime);
            audio.UpdateOutdoorAmbience(habitat.Dry, habitat.Cold, habitat.Wood, daylight, rain, playing, elapsed);
            audio.UpdateRiverAmbience(water.Gain, water.Pan, playing, elapsed);
            return;
        }
        audio.StopOutdoorNature();
        audio.StopRiver();
        if (session.CurrentMap.Id is >= 117 and <= 120)
            audio.UpdateHellAmbience(playing, elapsed);
        else if (WorldRenderer.IsFire(session.CurrentMap))
            audio.UpdateFireAmbience(world.FireAmbienceAt(adventure), playing, elapsed);
        else if (WorldRenderer.IsEarth(session.CurrentMap))
            audio.UpdateEarthAmbience(session.CurrentMap.Id > 80, playing, elapsed);
        else if (WorldRenderer.IsStone(session.CurrentMap))
            audio.UpdateStoneAmbience(session.CurrentMap.Id == 51, playing, elapsed);
        else if (WorldRenderer.IsIce(session.CurrentMap))
            audio.UpdateIceAmbience(session.CurrentMap.Id > 80, playing, elapsed);
        else if (WorldRenderer.IsWaterCave(session.CurrentMap))
            audio.UpdateWaterCaveAmbience(world.WaterCaveWetness(session.CurrentMap, player.VisualPosition), playing, elapsed);
        else if (WorldRenderer.IsFinal(session.CurrentMap))
            audio.UpdateFinalAmbience(playing, elapsed);
        else audio.UpdateAmbience(WorldRenderer.IsBreeding(session.CurrentMap), playing, elapsed);
    }

    private static Vector2 CameraTarget(Vector2 position) => Vector2.Clamp(
        position * 32 + new Vector2(16) - new Vector2(CanvasWidth, CanvasHeight) / 2,
        Vector2.Zero, new Vector2(TownMap.Width * 32 - CanvasWidth, TownMap.Height * 32 - CanvasHeight));

    private void RestorePosition(MapPosition position)
    {
        session.Location.Position = position;
        world.SynchronizeRaft(adventure, position, false);
        player.SnapTo(position);
        player.Update(0, world.RaftSupport(adventure, sceneTime));
        walkingStep = false;
        world.ClearSnowTracks();
        cameraPosition = CameraTarget(player.VisualPosition);
        pendingDirection = iceDirection = Point.Zero;
        messageRemaining = 0;
    }

    private void FinishStorage()
    {
        if (saving?.IsCompleted == true)
        {
            try
            {
                saving.GetAwaiter().GetResult();
                menuMessage = SavePreferences()
                    ? saves.JournalWarning.Length == 0 ? "Your game is saved." : "Game saved. Map notes\ncould not be saved."
                    : "Game saved. Preferences\ncould not be saved.";
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(error.Message);
                menuMessage = "Could not save. Your current\ngame is still here.";
            }
            saving = null;
        }
        if (loading?.IsCompleted == true)
        {
            if (smoke) loading.GetAwaiter().GetResult(); // Verification must fail instead of masking a bad selected save.
            try
            {
                var next = loading.GetAwaiter().GetResult();
                command?.Dispose(); command = null; pendingEvent = null; overlay = null; textReceiver = null;
                saves = loadingStore ?? saves;
                session = next;
                titleScreen = false; restorePauseOverlay = null; pausePage = PausePage.Main;
                selected = 0; selectionY = 0;
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
            if (titleScreen && loadingStore is not null) ShowLoadGames();
            loadingStore = null;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        BeginTouchDraw();
        if (studioSplashRemaining > 0) { DrawStudioSplash(); base.Draw(gameTime); return; }
        if (titleScreen) { DrawTitleScreen(); base.Draw(gameTime); return; }
        var outdoor = session.CurrentMap.Id is >= 1 and <= 40;
        var daylight = outdoor ? DayNightCycle.At(dayNightSeconds, timeOfDayMode)
            : new DayNightCycle.Light(Color.White, 1);
        sunShafts.Daylight = daylight.Daylight;
        lighting.NightAmount = daylight.Night;
        town.Sunlight = null;
        if (mapOpen && !worldMapOpen) RenderAreaMap();
        town.Sunlight = sunShaftStrength > 0 && session.CurrentMap.Id is >= 1 and <= 40 ? sunShafts : null;
        // Round both axes together: one world pixel always occupies one canvas pixel.
        var camera = Matrix.CreateTranslation(-MathF.Round(cameraPosition.X), -MathF.Round(cameraPosition.Y), 0);
        if (sunShaftStrength > 0 && session.CurrentMap.Id is >= 1 and <= 40)
            sunShafts.Prepare(batch, cameraPosition, sceneTime, cloudShadowStrength);
        if (pixelLighting) lighting.Prepare(batch, adventure, camera, sceneTime);
        GraphicsDevice.SetRenderTarget(miniatureMode == 0 && !vignette ? canvas : miniature.World);
        GraphicsDevice.Clear(Background);
        var rainAmount = rainStrength == 0 ? 0 : rainStrength * Rain.WeatherAmount(outdoorWeatherTime);
        batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera);
        if (session.CurrentMap.Id == TownMap.Id) town.Draw(batch, player, sceneTime, miniatureMode != 0, rainAmount, world);
        else
        {
            world.OutdoorDaylight = daylight.Daylight;
            world.Draw(batch, adventure, player, town, sceneTime, miniatureMode != 0, rainAmount);
        }
        if (pixelLighting) lighting.DrawTorches(batch, adventure.Visibility, sceneTime);
        batch.End();
        sunShafts.DrawClouds(batch, session.CurrentMap, cameraPosition, sceneTime, cloudShadowStrength);
        lighting.ApplyAmbient(batch, daylight.Ambient);
        if (WorldRenderer.IsFire(session.CurrentMap)) lighting.ApplyFireAmbient(batch, session.CurrentMap.Id > 80);
        if (pixelLighting) lighting.Apply(batch);
        sunShafts.Draw(batch, session.CurrentMap, cameraPosition, sceneTime, sunShaftStrength, cloudShadowStrength);
        if (outdoor)
        {
            batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera);
            Rain.DrawStreaks(batch, pixel, sceneTime, rainAmount,
                session.CurrentMap.Id == TownMap.Id ? null
                    : (x, y) => world.SnowColdAt(x, y) && adventure.Visibility.Contains(new(x / 32, y / 32)));
            if (session.CurrentMap.Id != TownMap.Id) world.DrawSnowfall(batch, adventure, sceneTime, rainAmount);
            batch.End();
        }

        Texture2D? presentedWorld = miniatureMode == 0 && vignette ? miniature.World : null;
        var hell = session.CurrentMap.Id is >= 117 and <= 120;
        var crispCave = hell || WorldRenderer.IsFire(session.CurrentMap);
        if (miniatureMode != 0)
        {
            var focusY = player.VisualPosition.Y * 32 + 12 - cameraPosition.Y;
            // Hell keeps its contact shadows, but its pixel art must not be blurred.
            miniature.Apply(batch, focusY, miniatureMode == 2, crispCave ? 0f : 1f);
            presentedWorld = miniature.Result;
        }
        if (session.CurrentMap.Id != TownMap.Id && world.HasHeat && (!outdoor || daylight.Daylight > 0))
            presentedWorld = heat.Apply(batch, presentedWorld ?? canvas, world,
                new(MathF.Round(cameraPosition.X), MathF.Round(cameraPosition.Y)),
                player.VisualPosition * 32 + new Vector2(16, 12), sceneTime, crispCave, daylight.Daylight);
        if (presentedWorld is not null)
        {
            GraphicsDevice.SetRenderTarget(canvas);
            GraphicsDevice.Clear(Color.Transparent);
        }

        // Keep awareness cues crisp above heat, lighting and miniature effects.
        if (!mapOpen && overlay is null && !paused && pauseAmount <= .005f)
        {
            batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera);
            world.DrawPursuitAlerts(batch, adventure, sceneTime);
            batch.End();
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

        if (pauseAmount > 0.005f) DrawPauseMenu();
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        var destination = CanvasDestination();
        var scale = destination.Width / (float)CanvasWidth;
        var shake = CombatShake();
        destination.Offset((int)MathF.Round(shake.X * scale), (int)MathF.Round(shake.Y * scale));
        batch.Begin(samplerState: SamplerState.PointClamp);
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
                Fill(new(destination.X, destination.Y + (int)((y + 1) * scale) - 1, destination.Width, 1), Color.Black * 0.12f);
        batch.End();

        DrawTouchControls();
        if (smoke && loading is null && ++smokeFrames == 3)
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

    private Rectangle CanvasDestination()
    {
        var viewport = GraphicsDevice.Viewport;
        var scale = Math.Min(viewport.Width / (float)CanvasWidth, viewport.Height / (float)CanvasHeight);
        // Whole-number enlargement keeps every pixel the same size at normal window sizes.
        if (scale >= 1) scale = MathF.Floor(scale);
        var width = (int)(CanvasWidth * scale);
        var height = (int)(CanvasHeight * scale);
        return new((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }

    private void UpdateStudioSplash(InputFrame actions, double elapsed)
    {
        studioSplashRemaining = actions.Confirm || actions.Back || actions.Pause ? 0 : Math.Max(0, studioSplashRemaining - elapsed);
        if (studioSplashRemaining <= 0) { paused = false; ShowWelcome(); }
    }

    private void DrawStudioSplash()
    {
        GraphicsDevice.SetRenderTarget(canvas);
        GraphicsDevice.Clear(new Color(9, 14, 22));
        var elapsed = StudioSplashDuration - studioSplashRemaining;
        var opacity = MathHelper.SmoothStep(0, 1, (float)Math.Min(1, elapsed / .45))
            * MathHelper.SmoothStep(0, 1, (float)Math.Min(1, studioSplashRemaining / .55));
        var aspect = studioLogo.Width / (float)studioLogo.Height;
        var height = Math.Min(CanvasHeight * .64f, CanvasWidth * .70f / aspect);
        var width = height * aspect;
        var bounds = new Rectangle((int)((CanvasWidth - width) / 2),
            (int)(CanvasHeight * .48f - height / 2), (int)width, (int)height);
        batch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp);
        batch.Draw(studioLogo, bounds, new Color(1f, 1f, 1f, opacity));
        var hint = controllerConnected ? "SOUTH: CONTINUE" : touchEnabled ? "TAP TO CONTINUE" : "ENTER: CONTINUE";
        var caption = new Color(Muted.R, Muted.G, Muted.B, (byte)(opacity * 200));
        font.Draw(batch, hint, (CanvasWidth - hint.Length * 6) / 2, 159, caption);
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(canvas, CanvasDestination(), Color.White);
        batch.End();
        TouchTarget(new(0, 0, CanvasWidth, CanvasHeight), TouchConfirm);
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
        music?.Dispose();
        audio?.Dispose();
        world?.Dispose();
        town?.Dispose();
        titleTown?.Dispose();
        miniature?.Dispose();
        heat?.Dispose();
        lighting?.Dispose();
        sunShafts?.Dispose();
        mapView?.Dispose();
        canvas?.Dispose();
        vignetteMask?.Dispose();
        pixel?.Dispose();
        studioLogo?.Dispose();
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
