using Yipe.Desktop;

internal static class RainWeatherChecks
{
    public static void Run(Action<bool, string> check)
    {
        var weather = typeof(GameInput).Assembly.GetType("Yipe.Desktop.Rain")!.GetMethod("WeatherAmount")!;
        float Amount(double seconds) => (float)weather.Invoke(null, [seconds])!;
        var samples = Enumerable.Range(0, 86401).Select(second => Amount(second)).ToArray();
        check(samples.All(value => float.IsFinite(value) && value is >= 0 and <= 1)
            && samples.Take(301).All(value => value == 0) && samples.Contains(1f),
            "Automatic weather stays bounded over 24 outdoor hours and starts with at least five clear minutes");
        var drySpells = new List<int>(); var showers = new List<int>();
        var start = 0;
        for (var second = 1; second < samples.Length; second++)
        {
            if ((samples[second] > 0) == (samples[second - 1] > 0)) continue;
            (samples[second] > 0 ? drySpells : showers).Add(second - start);
            start = second;
        }
        // One-second sampling includes each zero-valued boundary in the clear spell.
        check(drySpells.Count > 100 && drySpells.All(seconds => seconds is >= 301 and <= 601)
            && drySpells.Distinct().Count() > 10, "Automatic clear spells vary between five and ten minutes");
        check(showers.Count > 100 && showers.All(seconds => seconds is >= 29 and <= 59)
            && showers.Distinct().Count() > 10, "Automatic showers vary between 30 and 60 seconds including their transition boundaries");
        check(samples.Zip(samples.Skip(1)).All(pair => Math.Abs(pair.First - pair.Second) < .4f)
            && samples.Any(value => value is > 0 and < 1), "Automatic showers fade through intermediate intensity without abrupt starts or stops");
    }
}
