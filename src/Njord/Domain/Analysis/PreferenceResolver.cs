using Njord.Configuration;

namespace Njord.Domain.Analysis;

public static class PreferenceResolver
{
    public static readonly IReadOnlyList<string> ScoreNames =
        ["Laundry", "Outdoor", "Running", "Cycling", "Bbq", "Irrigation", "Solar", "Ventilation"];

    public static IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> Resolve(
        IndexOptions options, IEnumerable<string> locationNames)
    {
        var result = new Dictionary<(string, string), ResolvedPreferences>();

        foreach (var location in locationNames)
        {
            var locationOverride = FindLocationOverride(options, location);

            foreach (var score in ScoreNames)
            {
                var resolved = ResolveForLocationAndScore(options, locationOverride, score);
                result[(location, score)] = resolved;
            }
        }

        return result;
    }

    public static ResolvedPreferences ResolveForLocationAndScore(
        IndexOptions options, LocationIndexOverride? locationOverride, string score)
    {
        var locationScorePrefs = locationOverride != null
            ? FindScoreOverride(locationOverride.ScoreOverrides, score)
            : null;
        var locationGlobalPrefs = locationOverride?.Preferences;
        var scorePrefs = FindScoreOverride(options.ScoreOverrides, score);
        var globalPrefs = options.Preferences;

        return new ResolvedPreferences(
            IdealTemp: Cascade(
                locationScorePrefs?.IdealOutdoorTemp,
                locationGlobalPrefs?.IdealOutdoorTemp,
                scorePrefs?.IdealOutdoorTemp,
                globalPrefs.IdealOutdoorTemp,
                22.0),
            IdealTempLow: Cascade(
                locationScorePrefs?.RunningIdealTempLow,
                locationGlobalPrefs?.RunningIdealTempLow,
                scorePrefs?.RunningIdealTempLow,
                globalPrefs.RunningIdealTempLow,
                5.0),
            IdealTempHigh: Cascade(
                locationScorePrefs?.RunningIdealTempHigh,
                locationGlobalPrefs?.RunningIdealTempHigh,
                scorePrefs?.RunningIdealTempHigh,
                globalPrefs.RunningIdealTempHigh,
                20.0),
            MinTemp: Cascade(
                locationScorePrefs?.BbqMinTemp,
                locationGlobalPrefs?.BbqMinTemp,
                scorePrefs?.BbqMinTemp,
                globalPrefs.BbqMinTemp,
                10.0),
            IdealWindLow: Cascade(
                locationScorePrefs?.BbqIdealWindLow,
                locationGlobalPrefs?.BbqIdealWindLow,
                scorePrefs?.BbqIdealWindLow,
                globalPrefs.BbqIdealWindLow,
                1.0),
            IdealWindHigh: Cascade(
                locationScorePrefs?.BbqIdealWindHigh,
                locationGlobalPrefs?.BbqIdealWindHigh,
                scorePrefs?.BbqIdealWindHigh,
                globalPrefs.BbqIdealWindHigh,
                3.0),
            IndoorTemp: Cascade(
                locationScorePrefs?.IndoorTemp,
                locationGlobalPrefs?.IndoorTemp,
                scorePrefs?.IndoorTemp,
                globalPrefs.IndoorTemp,
                22.0),
            HeatSensitivity: ClampSensitivity(Cascade(
                locationScorePrefs?.HeatSensitivity,
                locationGlobalPrefs?.HeatSensitivity,
                scorePrefs?.HeatSensitivity,
                globalPrefs.HeatSensitivity,
                1.0)),
            HumiditySensitivity: ClampSensitivity(Cascade(
                locationScorePrefs?.HumiditySensitivity,
                locationGlobalPrefs?.HumiditySensitivity,
                scorePrefs?.HumiditySensitivity,
                globalPrefs.HumiditySensitivity,
                1.0)),
            WindSensitivity: ClampSensitivity(Cascade(
                locationScorePrefs?.WindSensitivity,
                locationGlobalPrefs?.WindSensitivity,
                scorePrefs?.WindSensitivity,
                globalPrefs.WindSensitivity,
                1.0)),
            RainSensitivity: ClampSensitivity(Cascade(
                locationScorePrefs?.RainSensitivity,
                locationGlobalPrefs?.RainSensitivity,
                scorePrefs?.RainSensitivity,
                globalPrefs.RainSensitivity,
                1.0)));
    }

    private static LocationIndexOverride? FindLocationOverride(IndexOptions options, string location) =>
        options.LocationOverrides.FirstOrDefault(
            o => string.Equals(o.Location, location, StringComparison.OrdinalIgnoreCase));

    private static IndexPreferences? FindScoreOverride(IDictionary<string, IndexPreferences> overrides, string score) =>
        overrides.TryGetValue(score, out var prefs) ? prefs : null;

    private static double Cascade(double? l1, double? l2, double? l3, double? l4, double fallback) =>
        l1 ?? l2 ?? l3 ?? l4 ?? fallback;

    private static double ClampSensitivity(double value) =>
        Math.Clamp(value, 0.0, 5.0);
}
