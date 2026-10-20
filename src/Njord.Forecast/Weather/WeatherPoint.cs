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
    private static readonly WeatherParam[] Flags =
        [.. Enum.GetValues<WeatherParam>().Where(p => p != WeatherParam.None)];

    public bool HasFields(WeatherParam required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(WeatherParam param) => param switch
    {
        WeatherParam.Temperature => Temperature is not null,
        WeatherParam.ApparentTemperature => ApparentTemperature is not null,
        WeatherParam.WindSpeed => WindSpeed is not null,
        WeatherParam.Precipitation => Precipitation is not null,
        WeatherParam.PrecipitationProbability => PrecipitationProbability is not null,
        WeatherParam.CloudCover => CloudCover is not null,
        WeatherParam.Pressure => Pressure is not null,
        WeatherParam.WeatherCode => WeatherCode is not null,
        WeatherParam.Humidity => Humidity is not null,
        WeatherParam.DewPoint => DewPoint is not null,
        WeatherParam.UvIndex => UvIndex is not null,
        WeatherParam.EnsembleStdDev => EnsembleStdDev is not null,
        WeatherParam.WindGusts => WindGusts is not null,
        WeatherParam.Visibility => Visibility is not null,
        WeatherParam.Cape => Cape is not null,
        WeatherParam.Rain => Rain is not null,
        WeatherParam.Snowfall => Snowfall is not null,
        WeatherParam.SoilTemperature => SoilTemperature is not null,
        WeatherParam.FreezingLevelHeight => FreezingLevelHeight is not null,
        WeatherParam.IsDay => IsDay is not null,
        WeatherParam.ShortwaveRadiation => ShortwaveRadiation is not null,
        WeatherParam.DirectRadiation => DirectRadiation is not null,
        WeatherParam.DiffuseRadiation => DiffuseRadiation is not null,
        WeatherParam.DirectNormalIrradiance => DirectNormalIrradiance is not null,
        WeatherParam.SunshineDuration => SunshineDuration is not null,
        WeatherParam.WindDirection => WindDirection is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
