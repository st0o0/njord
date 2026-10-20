using System.Globalization;

namespace Njord.Messages.Pipeline;

public static class LifestyleMappings
{
    public static string ToWireName(this LifestyleKind kind) => kind switch
    {
        LifestyleKind.Ventilation => "ventilation",
        LifestyleKind.Exercise => "exercise",
        LifestyleKind.LaundryScore => "laundry_score",
        LifestyleKind.CommuteSafety => "commute_safety",
        LifestyleKind.PollenTrend => "pollen_trend",
        LifestyleKind.NightVentilation => "night_ventilation",
        LifestyleKind.ConfidenceTrend => "confidence_trend",
        LifestyleKind.SolarYield => "solar_yield",
        LifestyleKind.Gardening => "gardening",
        LifestyleKind.OutdoorScore => "outdoor_score",
        LifestyleKind.BbqScore => "bbq_score",
        LifestyleKind.FloodTimeline => "flood_timeline",
        LifestyleKind.FrontApproaching => "front_approaching",
        LifestyleKind.ClimateAnomaly => "climate_anomaly",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static LifestyleUpdate? ToLifestyleUpdate(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.VentilationAdvised e => new LifestyleUpdate(e.Location, LifestyleKind.Ventilation, Fields(
            ("best_start", Time(e.BestStart)),
            ("best_end", Time(e.BestEnd)),
            ("reason", e.Reason))),
        PipelineEvent.ExerciseAdvised e => new LifestyleUpdate(e.Location, LifestyleKind.Exercise, Fields(
            ("level", e.Level),
            ("reasons", string.Join(",", e.Reasons)),
            ("best_window", Time(e.BestWindow)))),
        PipelineEvent.LaundryScoreComputed e => new LifestyleUpdate(e.Location, LifestyleKind.LaundryScore,
            ScoreFields(e.Score, e.BestStart, e.BestHours)),
        PipelineEvent.CommuteSafety e => new LifestyleUpdate(e.Location, LifestyleKind.CommuteSafety, Fields(
            ("level", e.Level),
            ("risks", string.Join(",", e.Risks)))),
        PipelineEvent.PollenTrendComputed e => new LifestyleUpdate(e.Location, LifestyleKind.PollenTrend, PollenFields(e)),
        PipelineEvent.NightVentilationAdvised e => new LifestyleUpdate(e.Location, LifestyleKind.NightVentilation, Fields(
            ("open_at", Time(e.OpenAt)),
            ("close_at", Time(e.CloseAt)),
            ("low_temp", Number(e.LowTemp)),
            ("tomorrow_peak", Number(e.TomorrowPeak)))),
        PipelineEvent.ConfidenceTrend e => new LifestyleUpdate(e.Location, LifestyleKind.ConfidenceTrend, Fields(
            ("direction", e.Direction),
            ("current_avg_std_dev", Number(e.CurrentAvgStdDev)),
            ("previous_avg_std_dev", Number(e.PreviousAvgStdDev)))),
        PipelineEvent.SolarYieldComputed e => new LifestyleUpdate(e.Location, LifestyleKind.SolarYield, Fields(
            ("kwh_per_kwp", Number(e.KwhPerKwp)),
            ("peak_hour", Number(e.PeakHour)))),
        PipelineEvent.GardeningAdvised e => new LifestyleUpdate(e.Location, LifestyleKind.Gardening, Fields(
            ("planting_safe", Flag(e.PlantingSafe)),
            ("watering_needed", Flag(e.WateringNeeded)),
            ("frost_risk", Flag(e.FrostRisk)))),
        PipelineEvent.OutdoorScoreComputed e => new LifestyleUpdate(e.Location, LifestyleKind.OutdoorScore,
            ScoreFields(e.Score, e.BestStart, e.BestHours)),
        PipelineEvent.BBQScoreComputed e => new LifestyleUpdate(e.Location, LifestyleKind.BbqScore,
            ScoreFields(e.Score, e.BestStart, e.BestHours)),
        PipelineEvent.FloodTimelineComputed e => new LifestyleUpdate(e.Location, LifestyleKind.FloodTimeline, Fields(
            ("peak_time", Time(e.PeakTime)),
            ("peak_discharge", Number(e.PeakDischarge)),
            ("days_above_warning", Number(e.DaysAboveWarning)))),
        PipelineEvent.FrontApproaching e => new LifestyleUpdate(e.Location, LifestyleKind.FrontApproaching, Fields(
            ("estimated_in_hours", Number(e.EstimatedIn.TotalHours)),
            ("pressure_drop", Number(e.PressureDrop)),
            ("temp_drop", Number(e.TempDrop)))),
        PipelineEvent.ClimateAnomalyComputed e => new LifestyleUpdate(e.Location, LifestyleKind.ClimateAnomaly, Fields(
            ("temp_anomaly", Number(e.TempAnomaly)),
            ("precip_anomaly", Number(e.PrecipAnomaly)),
            ("baseline_period", e.BaselinePeriod))),
        _ => null,
    };

    private static IReadOnlyDictionary<string, string> ScoreFields(int score, DateTimeOffset bestStart, int bestHours) => Fields(
        ("score", Number(score)),
        ("best_start", Time(bestStart)),
        ("best_hours", Number(bestHours)));

    private static IReadOnlyDictionary<string, string> PollenFields(PipelineEvent.PollenTrendComputed e)
    {
        var fields = new Dictionary<string, string>();
        foreach (var (pollen, trend) in e.Trends.OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            fields[$"trend_{pollen}"] = trend;
        }

        if (e.PeakType is { } peakType)
        {
            fields["peak_type"] = peakType;
        }

        if (e.PeakTime is { } peakTime)
        {
            fields["peak_time"] = Time(peakTime);
        }

        return fields;
    }

    private static IReadOnlyDictionary<string, string> Fields(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value);

    private static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "true" : "false";
}
