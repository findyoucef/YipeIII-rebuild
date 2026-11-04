namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private bool androidBack;
    internal void RequestAndroidBack() => androidBack = true;

    // Android can kill a background process without calling Exiting or EndRun.
    // RenderOnUIThread is MonoGame's default, so suspension cannot race Update.
    internal void SuspendAndroid()
    {
        if (profile is null) return;
        touchInput.Reset();
        audio.Update(false);
        music.Update(MusicTrack.None, false, 0, 0, 0);
        SavePreferences();
        if (!titleScreen && studioSplashRemaining <= 0) paused = true;
        try
        {
            if (saving is not null) saving.GetAwaiter().GetResult();
            else if (!titleScreen && studioSplashRemaining <= 0 && loading is null &&
                command is null && textReceiver is null && Hero.Health > 0)
            {
                // Use the same safe boundary as manual saving; never persist a half-finished interaction.
                saves.SaveAsync(session, journal).GetAwaiter().GetResult();
                menuMessage = "Adventure saved on suspension.";
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            menuMessage = "Could not save on suspension.";
            Console.Error.WriteLine(error.Message);
        }
    }
}
