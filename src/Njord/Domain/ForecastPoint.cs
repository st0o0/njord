namespace Njord.Domain;

public sealed record ForecastPoint(
    DateTimeOffset ValidAt,
    double? Temperature = null,
    double? ApparentTemperature = null,
    double? Precipitation = null,
    double? WindSpeed = null,
    double? WindGust = null,
    double? Dewpoint = null,
    double? RelativeHumidity = null,
    double? CloudCover = null,
    double? PressureMsl = null)
{
    public double? Get(WeatherParameter parameter) => parameter switch
    {
        WeatherParameter.Temperature => Temperature,
        WeatherParameter.ApparentTemperature => ApparentTemperature,
        WeatherParameter.Precipitation => Precipitation,
        WeatherParameter.WindSpeed => WindSpeed,
        WeatherParameter.WindGust => WindGust,
        WeatherParameter.Dewpoint => Dewpoint,
        WeatherParameter.RelativeHumidity => RelativeHumidity,
        WeatherParameter.CloudCover => CloudCover,
        WeatherParameter.PressureMsl => PressureMsl,
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
    };
}
