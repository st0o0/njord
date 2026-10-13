using Njord.Compute.Analysis;
using Njord.Compute.Settings;

namespace Njord.Core.Configuration.Alerts;

public static class AlertSettingsConversion
{
    public static AlertSettings ToSettings(this AlertOptions options) => new(
        Frost: Thresholds(options.FrostThresholds, ScanDirection.Min, nameof(AlertOptions.FrostThresholds)),
        Heat: Thresholds(options.HeatThresholds, ScanDirection.Max, nameof(AlertOptions.HeatThresholds)),
        StormGust: Thresholds(options.StormGustThresholds, ScanDirection.Max, nameof(AlertOptions.StormGustThresholds)),
        HeavyRainHourlyThreshold: options.HeavyRainHourlyThreshold,
        HeavyRainDailyThreshold: options.HeavyRainDailyThreshold,
        PressureDropThreshold: options.PressureDropThreshold,
        PressureDropSevereThreshold: options.PressureDropSevereThreshold,
        FogPersistentHours: options.FogPersistentHours,
        CapeThreshold: options.CapeThreshold,
        ThunderstormPrecipThreshold: options.ThunderstormPrecipThreshold,
        ThunderstormGustThreshold: options.ThunderstormGustThreshold,
        IceThreshold: options.IceThreshold,
        WindChill: Thresholds(options.WindChillThresholds, ScanDirection.Min, nameof(AlertOptions.WindChillThresholds)),
        Visibility: Thresholds(options.VisibilityThresholds, ScanDirection.Min, nameof(AlertOptions.VisibilityThresholds)),
        TropicalNight: Thresholds(options.TropicalNightThresholds, ScanDirection.Max, nameof(AlertOptions.TropicalNightThresholds)),
        Humidity: Thresholds(options.HumidityThresholds, ScanDirection.Max, nameof(AlertOptions.HumidityThresholds)));

    private static SeverityThresholds Thresholds(double[] values, ScanDirection direction, string property) =>
        SeverityThresholds.TryCreate(values, direction, out var thresholds)
            ? thresholds
            : throw new InvalidOperationException(
                $"Alerts.{property} needs three values; AlertOptionsValidator rejects this configuration.");
}
