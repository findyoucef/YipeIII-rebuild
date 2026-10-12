using Microsoft.Xna.Framework;
using Yipe.Desktop;

internal static class DayNightClockChecks
{
    public static void Run(Action<bool, string> check)
    {
        var type = typeof(GameInput).Assembly.GetType("Yipe.Desktop.DayNightCycle")!;
        double Advance(double seconds, double elapsed) => (double)type.GetMethod("Advance")!.Invoke(null, [seconds, elapsed])!;
        (Color Ambient, float Daylight) At(double seconds, int mode = 0)
        {
            var light = type.GetMethod("At")!.Invoke(null, [seconds, mode])!;
            return ((Color)light.GetType().GetProperty("Ambient")!.GetValue(light)!,
                (float)light.GetType().GetProperty("Daylight")!.GetValue(light)!);
        }
        check(Advance(1199.75, .5) == .25 && Advance(300, 2400) == 300,
            "Day/night clock wraps a twenty-minute cycle without retaining unbounded elapsed time");
        double Timed(int fps)
        {
            var seconds = 300d;
            for (var i = 0; i < fps * 1250; i++) seconds = Advance(seconds, 1d / fps);
            return seconds;
        }
        check(new[] { 24, 30, 60 }.All(fps => Math.Abs(Timed(fps) - 350) < .00001),
            "Day/night phase depends on elapsed time rather than update rate");
        check(At(599).Ambient == Color.White && At(599).Daylight == 1
            && At(720).Daylight == 0 && At(1079).Daylight == 0 && At(1200).Ambient == Color.White,
            "Ten-minute day, two-minute dusk, six-minute night and two-minute dawn meet continuously");
        var samples = Enumerable.Range(0, 12001).Select(i => At(i / 10d)).ToArray();
        check(samples.All(light => light.Daylight is >= 0 and <= 1 && light.Ambient.A == 255
                && light.Ambient.R >= 142 && light.Ambient.G >= 167 && light.Ambient.B >= 150)
            && samples.Zip(samples.Skip(1)).All(pair => Math.Abs(pair.First.Daylight - pair.Second.Daylight) < .002
                && Math.Abs(pair.First.Ambient.R - pair.Second.Ambient.R) <= 1
                && Math.Abs(pair.First.Ambient.G - pair.Second.Ambient.G) <= 1
                && Math.Abs(pair.First.Ambient.B - pair.Second.Ambient.B) <= 1),
            "Moonlight retains a readable brightness floor and every transition has small continuous color changes");
        check(At(900, 1) == At(300) && At(50, 2) == At(660) && At(50, 3) == At(900),
            "Fixed Day, Dusk and Night resolve their palettes independently of the suspended phase");
    }
}
