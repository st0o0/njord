namespace Njord.Core.Seasonal;

// One month of the seasonal forecast (ensemble mean), ValidFrom is the first day of the month. Temperature anomaly
// in K (equal to degrees Celsius), precipitation anomaly and mean in mm; the normal month is mean minus anomaly.
// The remaining fields are absolute monthly means in API units (temperature degC, snowfall cm, snow depth m,
// cloud cover %, wind m/s, pressure hPa, sunshine duration s, shortwave radiation W/m2, sea surface temperature degC).
public sealed record SeasonalPoint(
    DateTimeOffset ValidFrom,
    double? TemperatureAnomaly = null,
    double? PrecipitationAnomaly = null,
    double? PrecipitationMean = null,
    double? Temperature = null,
    double? Snowfall = null,
    double? SnowDepth = null,
    double? CloudCover = null,
    double? WindSpeed = null,
    double? Pressure = null,
    double? SunshineDuration = null,
    double? ShortwaveRadiation = null,
    double? WindGusts = null,
    double? SeaSurfaceTemperature = null) : IHasValidTime
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
        SeasonalParams.Temperature => Temperature is not null,
        SeasonalParams.Snowfall => Snowfall is not null,
        SeasonalParams.SnowDepth => SnowDepth is not null,
        SeasonalParams.CloudCover => CloudCover is not null,
        SeasonalParams.WindSpeed => WindSpeed is not null,
        SeasonalParams.Pressure => Pressure is not null,
        SeasonalParams.SunshineDuration => SunshineDuration is not null,
        SeasonalParams.ShortwaveRadiation => ShortwaveRadiation is not null,
        SeasonalParams.WindGusts => WindGusts is not null,
        SeasonalParams.SeaSurfaceTemperature => SeaSurfaceTemperature is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
