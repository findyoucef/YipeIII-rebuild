using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;
using Yipe.Desktop;

namespace Yipe.Android;

[Activity(Name = "com.icosiumlabs.yipeiii.MainActivity", Label = "Yipe! III", Icon = "@drawable/yipe",
    MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTask,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
public sealed class MainActivity : AndroidGameActivity
{
    private TownGame? game;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // GameMusic owns mute/resume; the framework must not resume a muted song.
        AutoPauseAndResumeMediaPlayer = false;
        var root = FilesDir!.AbsolutePath;
        var content = InstallContent(root);
        game = new TownGame(false, null, null, content, Path.Combine(root, "Saves"));
        var view = (View)game.Services.GetService(typeof(View))!;
        view.SetOnApplyWindowInsetsListener(new TouchInsets(game));
        SetContentView(view);
        game.Run();
    }

    private string InstallContent(string root)
    {
        var content = Path.Combine(root, "Content");
        var stamp = Path.Combine(content, ".installed");
        // Reinstall assets after any APK update, including another build of the same version.
        var version = PackageManager!.GetPackageInfo(PackageName!, PackageInfoFlags.MetaData)!
            .LastUpdateTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (File.Exists(stamp) && File.ReadAllText(stamp) == version) return content;

        var staging = Path.Combine(root, "Content.installing");
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Copy("Content", staging);
        File.WriteAllText(Path.Combine(staging, ".installed"), version);
        if (Directory.Exists(content)) Directory.Delete(content, recursive: true);
        Directory.Move(staging, content);
        return content;

        void Copy(string asset, string destination)
        {
            var children = Assets!.List(asset)!;
            if (children.Length == 0)
            {
                using var source = Assets.Open(asset);
                using var target = File.Create(destination);
                source.CopyTo(target);
                return;
            }
            Directory.CreateDirectory(destination);
            foreach (var child in children) Copy(asset + "/" + child, Path.Combine(destination, child));
        }
    }

    protected override void OnPause()
    {
        game?.SuspendAndroid();
        base.OnPause();
    }

    // Route system Back through the game's normal menu navigation, including older Android versions.
    public override void OnBackPressed() => game?.RequestAndroidBack();

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e?.KeyCode != Keycode.Back) return base.DispatchKeyEvent(e);
        if (e.Action == KeyEventActions.Up) game?.RequestAndroidBack();
        return true;
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus && OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            Window!.InsetsController!.Hide(WindowInsets.Type.SystemBars());
            Window.InsetsController.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
        }
    }

    private sealed class TouchInsets(TownGame game) : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var safe = insets.GetInsets(WindowInsets.Type.DisplayCutout() | WindowInsets.Type.SystemGestures());
                game.SetTouchInsets(safe.Left, safe.Top, safe.Right, safe.Bottom);
            }
            else if (OperatingSystem.IsAndroidVersionAtLeast(28) && insets.DisplayCutout is { } cutout)
                game.SetTouchInsets(cutout.SafeInsetLeft, cutout.SafeInsetTop, cutout.SafeInsetRight, cutout.SafeInsetBottom);
            return insets;
        }
    }
}
