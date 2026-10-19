using Njord.Messages.Alerts;

namespace Njord.Core.Configuration;

public static class AlertOptionsThresholds
{
    public static SeverityThresholds Frost(this AlertOptions o) => Create(o.FrostThresholds, ScanDirection.Min, nameof(o.FrostThresholds));

    public static SeverityThresholds Heat(this AlertOptions o) => Create(o.HeatThresholds, ScanDirection.Max, nameof(o.HeatThresholds));

    public static SeverityThresholds StormGust(this AlertOptions o) => Create(o.StormGustThresholds, ScanDirection.Max, nameof(o.StormGustThresholds));

    public static SeverityThresholds WindChill(this AlertOptions o) => Create(o.WindChillThresholds, ScanDirection.Min, nameof(o.WindChillThresholds));

    public static SeverityThresholds Visibility(this AlertOptions o) => Create(o.VisibilityThresholds, ScanDirection.Min, nameof(o.VisibilityThresholds));

    public static SeverityThresholds TropicalNight(this AlertOptions o) => Create(o.TropicalNightThresholds, ScanDirection.Max, nameof(o.TropicalNightThresholds));

    public static SeverityThresholds Humidity(this AlertOptions o) => Create(o.HumidityThresholds, ScanDirection.Max, nameof(o.HumidityThresholds));

    private static SeverityThresholds Create(IReadOnlyList<double> values, ScanDirection direction, string property) =>
        SeverityThresholds.TryCreate(values, direction, out var thresholds)
            ? thresholds
            : throw new InvalidOperationException(
                $"Alerts.{property} needs three values; AlertOptionsValidator rejects this configuration.");
}
