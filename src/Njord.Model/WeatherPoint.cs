namespace Njord.Model;

public sealed record WeatherPoint(
    DateTimeOffset ValidFrom,
    double Temperature,
    double ApparentTemperature,
    double WindSpeed,
    double Precipitation,
    double PrecipitationProbability,
    double CloudCover,
    double Pressure,
    int WeatherCode,
    double Humidity,
    double DewPoint,
    double UvIndex,
    double? EnsembleStdDev = null) : IHasValidTime;
