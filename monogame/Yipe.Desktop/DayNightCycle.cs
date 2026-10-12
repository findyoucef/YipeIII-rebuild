using Microsoft.Xna.Framework;

namespace Yipe.Desktop;

/// <summary>Cosmetic time, independent of the adventure's rules and random stream.</summary>
internal static class DayNightCycle
{
    public const double Duration = 1200;
    public const double OpeningTime = 300;
    public readonly record struct Light(Color Ambient, float Daylight)
    {
        public float Night => 1 - Daylight;
    }

    public static double Advance(double seconds, double elapsed) => (seconds + elapsed) % Duration;

    public static Light At(double seconds, int mode)
    {
        seconds = mode switch { 1 => OpeningTime, 2 => 660, 3 => 900, _ => seconds };
        var moonlight = new Color(142, 167, 214);
        if (seconds < 600) return new(Color.White, 1);
        if (seconds < 720)
        {
            var progress = (float)((seconds - 600) / 120);
            return new(Transition(Color.White, new(255, 190, 150), moonlight, progress),
                1 - MathHelper.SmoothStep(0, 1, progress));
        }
        if (seconds < 1080) return new(moonlight, 0);
        var dawn = (float)((seconds - 1080) / 120);
        return new(Transition(moonlight, new(245, 208, 183), Color.White, dawn),
            MathHelper.SmoothStep(0, 1, dawn));
    }

    private static Color Transition(Color first, Color middle, Color last, float progress) => progress < .5f
        ? Color.Lerp(first, middle, MathHelper.SmoothStep(0, 1, progress * 2))
        : Color.Lerp(middle, last, MathHelper.SmoothStep(0, 1, (progress - .5f) * 2));
}
