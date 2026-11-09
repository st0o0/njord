namespace Njord.Core.Seasonal;

// One week of the seasonal forecast, ValidFrom is the first day of the week. Efi is the Extreme Forecast Index (-1..1),
// Sot the Shift of Tails, the Gt/Lt fields the probability in percent that the weekly anomaly is above/below the
// threshold (temperature in K, precipitation in mm).
public sealed record SeasonalWeeklyPoint(
    DateTimeOffset ValidFrom,
    double? TemperatureEfi = null,
    double? PrecipitationEfi = null,
    double? TemperatureSot10 = null,
    double? TemperatureSot90 = null,
    double? PrecipitationSot90 = null,
    double? TemperatureAnomalyGt0 = null,
    double? TemperatureAnomalyGt1 = null,
    double? TemperatureAnomalyGt2 = null,
    double? TemperatureAnomalyLtM1 = null,
    double? TemperatureAnomalyLtM2 = null,
    double? PrecipitationAnomalyGt0 = null,
    double? PrecipitationAnomalyGt10 = null,
    double? PrecipitationAnomalyGt20 = null) : IHasValidTime
{
    private static readonly SeasonalWeeklyParams[] _flags =
        [.. Enum.GetValues<SeasonalWeeklyParams>().Where(p => p != SeasonalWeeklyParams.None)];

    public bool HasFields(SeasonalWeeklyParams required) =>
        _flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(SeasonalWeeklyParams param) => param switch
    {
        SeasonalWeeklyParams.TemperatureEfi => TemperatureEfi is not null,
        SeasonalWeeklyParams.PrecipitationEfi => PrecipitationEfi is not null,
        SeasonalWeeklyParams.TemperatureSot10 => TemperatureSot10 is not null,
        SeasonalWeeklyParams.TemperatureSot90 => TemperatureSot90 is not null,
        SeasonalWeeklyParams.PrecipitationSot90 => PrecipitationSot90 is not null,
        SeasonalWeeklyParams.TemperatureAnomalyGt0 => TemperatureAnomalyGt0 is not null,
        SeasonalWeeklyParams.TemperatureAnomalyGt1 => TemperatureAnomalyGt1 is not null,
        SeasonalWeeklyParams.TemperatureAnomalyGt2 => TemperatureAnomalyGt2 is not null,
        SeasonalWeeklyParams.TemperatureAnomalyLtM1 => TemperatureAnomalyLtM1 is not null,
        SeasonalWeeklyParams.TemperatureAnomalyLtM2 => TemperatureAnomalyLtM2 is not null,
        SeasonalWeeklyParams.PrecipitationAnomalyGt0 => PrecipitationAnomalyGt0 is not null,
        SeasonalWeeklyParams.PrecipitationAnomalyGt10 => PrecipitationAnomalyGt10 is not null,
        SeasonalWeeklyParams.PrecipitationAnomalyGt20 => PrecipitationAnomalyGt20 is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
