using Njord.Core;

namespace Njord.Messages.Results;

public sealed record WeatherForecastPoint(
    DateTimeOffset Time,
    double Temperature,
    double ApparentTemperature,
    double Humidity,
    double WindSpeed,
    double WindDirection,
    double Pressure,
    string Condition,
    double? PrecipitationProbability = null,
    double? Precipitation = null,
    double? CloudCover = null);

public sealed record WeatherResult(
    Location Location,
    double Temperature,
    double ApparentTemperature,
    double Humidity,
    double WindSpeed,
    double WindDirection,
    double Pressure,
    string Condition,
    IReadOnlyList<WeatherForecastPoint> HourlyForecast) : FeatureResult(Location);
