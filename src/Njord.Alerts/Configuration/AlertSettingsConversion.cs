using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;

namespace Njord.Alerts.Configuration;

public static class AlertSettingsConversion
{
    public static AlertThresholdValues ToThresholdValues(this AlertOptions options) =>
        new(
            [.. options.FrostThresholds],
            [.. options.HeatThresholds],
            [.. options.StormGustThresholds],
            options.HeavyRainHourlyThreshold,
            options.HeavyRainDailyThreshold,
            options.PressureDropThreshold,
            options.PressureDropSevereThreshold,
            options.FogPersistentHours,
            options.CapeThreshold,
            options.ThunderstormPrecipThreshold,
            options.ThunderstormGustThreshold,
            options.IceThreshold,
            [.. options.WindChillThresholds],
            [.. options.VisibilityThresholds],
            [.. options.TropicalNightThresholds],
            [.. options.HumidityThresholds]);

    public static AlertSettings ToSettings(this AlertOptions options) =>
        options.ToThresholdValues().ToSettings();

    public static AlertSettings ToSettings(this AlertThresholdValues v) => new(
        Frost: Thresholds(v.FrostThresholds, ScanDirection.Min, nameof(v.FrostThresholds)),
        Heat: Thresholds(v.HeatThresholds, ScanDirection.Max, nameof(v.HeatThresholds)),
        StormGust: Thresholds(v.StormGustThresholds, ScanDirection.Max, nameof(v.StormGustThresholds)),
        HeavyRainHourlyThreshold: v.HeavyRainHourlyThreshold,
        HeavyRainDailyThreshold: v.HeavyRainDailyThreshold,
        PressureDropThreshold: v.PressureDropThreshold,
        PressureDropSevereThreshold: v.PressureDropSevereThreshold,
        FogPersistentHours: v.FogPersistentHours,
        CapeThreshold: v.CapeThreshold,
        ThunderstormPrecipThreshold: v.ThunderstormPrecipThreshold,
        ThunderstormGustThreshold: v.ThunderstormGustThreshold,
        IceThreshold: v.IceThreshold,
        WindChill: Thresholds(v.WindChillThresholds, ScanDirection.Min, nameof(v.WindChillThresholds)),
        Visibility: Thresholds(v.VisibilityThresholds, ScanDirection.Min, nameof(v.VisibilityThresholds)),
        TropicalNight: Thresholds(v.TropicalNightThresholds, ScanDirection.Max, nameof(v.TropicalNightThresholds)),
        Humidity: Thresholds(v.HumidityThresholds, ScanDirection.Max, nameof(v.HumidityThresholds)));

    private static SeverityThresholds Thresholds(IReadOnlyList<double> values, ScanDirection direction, string property) =>
        SeverityThresholds.TryCreate([.. values], direction, out var thresholds)
            ? thresholds
            : throw new InvalidOperationException(
                $"Alerts.{property} needs three values; AlertOptionsValidator rejects this configuration.");
}
