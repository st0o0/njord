namespace Njord.Messages.Alerts;

public sealed record AlertThresholdValues(
    IReadOnlyList<double> FrostThresholds,
    IReadOnlyList<double> HeatThresholds,
    IReadOnlyList<double> StormGustThresholds,
    double HeavyRainHourlyThreshold,
    double HeavyRainDailyThreshold,
    double PressureDropThreshold,
    double PressureDropSevereThreshold,
    int FogPersistentHours,
    double CapeThreshold,
    double ThunderstormPrecipThreshold,
    double ThunderstormGustThreshold,
    double IceThreshold,
    IReadOnlyList<double> WindChillThresholds,
    IReadOnlyList<double> VisibilityThresholds,
    IReadOnlyList<double> TropicalNightThresholds,
    IReadOnlyList<double> HumidityThresholds);

public sealed record AlertThresholdOverrides(
    IReadOnlyList<double>? FrostThresholds,
    IReadOnlyList<double>? HeatThresholds,
    IReadOnlyList<double>? StormGustThresholds,
    double? HeavyRainHourlyThreshold,
    double? HeavyRainDailyThreshold,
    double? PressureDropThreshold,
    double? PressureDropSevereThreshold,
    int? FogPersistentHours,
    double? CapeThreshold,
    double? ThunderstormPrecipThreshold,
    double? ThunderstormGustThreshold,
    double? IceThreshold,
    IReadOnlyList<double>? WindChillThresholds,
    IReadOnlyList<double>? VisibilityThresholds,
    IReadOnlyList<double>? TropicalNightThresholds,
    IReadOnlyList<double>? HumidityThresholds);
