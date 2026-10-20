namespace Njord.Forecast.Seasonal;

// One month of the seasonal forecast (ensemble mean), ValidFrom is the first day of the month. Temperature anomaly
// in K (equal to degrees Celsius), precipitation anomaly and mean in mm; the normal month is mean minus anomaly.
public sealed record SeasonalPoint(
    DateTimeOffset ValidFrom,
    double? TemperatureAnomaly = null,
    double? PrecipitationAnomaly = null,
    double? PrecipitationMean = null) : IHasValidTime
{
    private static readonly SeasonalParams[] Flags =
        [.. Enum.GetValues<SeasonalParams>().Where(p => p != SeasonalParams.None)];

    public bool HasFields(SeasonalParams required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(SeasonalParams param) => param switch
    {
        SeasonalParams.TemperatureAnomaly => TemperatureAnomaly is not null,
        SeasonalParams.PrecipitationAnomaly => PrecipitationAnomaly is not null,
        SeasonalParams.PrecipitationMean => PrecipitationMean is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
