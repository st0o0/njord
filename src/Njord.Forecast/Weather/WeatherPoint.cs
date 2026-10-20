namespace Njord.Forecast.Weather;

public sealed record WeatherPoint(
    DateTimeOffset ValidFrom,
    double? Temperature = null,
    double? ApparentTemperature = null,
    double? WindSpeed = null,
    double? Precipitation = null,
    double? PrecipitationProbability = null,
    double? CloudCover = null,
    double? Pressure = null,
    int? WeatherCode = null,
    double? Humidity = null,
    double? DewPoint = null,
    double? UvIndex = null,
    double? EnsembleStdDev = null,
    double? WindGusts = null,
    double? Visibility = null,
    double? Cape = null,
    double? Rain = null,
    double? Snowfall = null,
    double? SoilTemperature = null,
    double? FreezingLevelHeight = null,
    bool? IsDay = null,
    double? ShortwaveRadiation = null,
    double? DirectRadiation = null,
    double? DiffuseRadiation = null,
    double? DirectNormalIrradiance = null,
    double? SunshineDuration = null,
    double? WindDirection = null) : IHasValidTime
{
    private static readonly WeatherParams[] Flags =
        [.. Enum.GetValues<WeatherParams>().Where(p => p != WeatherParams.None)];

    public bool HasFields(WeatherParams required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(WeatherParams param) => param switch
    {
        WeatherParams.Temperature => Temperature is not null,
        WeatherParams.ApparentTemperature => ApparentTemperature is not null,
        WeatherParams.WindSpeed => WindSpeed is not null,
        WeatherParams.Precipitation => Precipitation is not null,
        WeatherParams.PrecipitationProbability => PrecipitationProbability is not null,
        WeatherParams.CloudCover => CloudCover is not null,
        WeatherParams.Pressure => Pressure is not null,
        WeatherParams.WeatherCode => WeatherCode is not null,
        WeatherParams.Humidity => Humidity is not null,
        WeatherParams.DewPoint => DewPoint is not null,
        WeatherParams.UvIndex => UvIndex is not null,
        WeatherParams.EnsembleStdDev => EnsembleStdDev is not null,
        WeatherParams.WindGusts => WindGusts is not null,
        WeatherParams.Visibility => Visibility is not null,
        WeatherParams.Cape => Cape is not null,
        WeatherParams.Rain => Rain is not null,
        WeatherParams.Snowfall => Snowfall is not null,
        WeatherParams.SoilTemperature => SoilTemperature is not null,
        WeatherParams.FreezingLevelHeight => FreezingLevelHeight is not null,
        WeatherParams.IsDay => IsDay is not null,
        WeatherParams.ShortwaveRadiation => ShortwaveRadiation is not null,
        WeatherParams.DirectRadiation => DirectRadiation is not null,
        WeatherParams.DiffuseRadiation => DiffuseRadiation is not null,
        WeatherParams.DirectNormalIrradiance => DirectNormalIrradiance is not null,
        WeatherParams.SunshineDuration => SunshineDuration is not null,
        WeatherParams.WindDirection => WindDirection is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
