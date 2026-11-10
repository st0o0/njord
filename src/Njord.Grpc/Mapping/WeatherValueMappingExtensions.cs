using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Results;

namespace Njord.Grpc.Mapping;

internal static class WeatherValueMappingExtensions
{
    public static Proto.WeatherValue ToWeatherValue(this WeatherResult r)
    {
        var proto = new Proto.WeatherValue
        {
            Temperature = r.Temperature,
            ApparentTemperature = r.ApparentTemperature,
            Humidity = r.Humidity,
            WindSpeed = r.WindSpeed,
            WindDirection = r.WindDirection,
            Pressure = r.Pressure,
            Condition = r.Condition,
        };
        if (r.CloudCover is { } cloudCover)
        {
            proto.CloudCover = cloudCover;
        }

        if (r.DewPoint is { } dewPoint)
        {
            proto.DewPoint = dewPoint;
        }

        if (r.Visibility is { } visibility)
        {
            proto.Visibility = visibility;
        }

        if (r.Precipitation is { } precipitation)
        {
            proto.Precipitation = precipitation;
        }

        if (r.WindGusts is { } windGusts)
        {
            proto.WindGusts = windGusts;
        }

        foreach (var point in r.HourlyForecast)
        {
            proto.HourlyForecast.Add(point.ToProto());
        }

        return proto;
    }

    private static Proto.WeatherForecastPoint ToProto(this WeatherForecastPoint p)
    {
        var proto = new Proto.WeatherForecastPoint
        {
            Time = Timestamp.FromDateTimeOffset(p.Time),
            Temperature = p.Temperature,
            ApparentTemperature = p.ApparentTemperature,
            Humidity = p.Humidity,
            WindSpeed = p.WindSpeed,
            WindDirection = p.WindDirection,
            Pressure = p.Pressure,
            Condition = p.Condition,
        };
        if (p.PrecipitationProbability is { } probability)
        {
            proto.PrecipitationProbability = probability;
        }

        if (p.Precipitation is { } precipitation)
        {
            proto.Precipitation = precipitation;
        }

        if (p.CloudCover is { } cloudCover)
        {
            proto.CloudCover = cloudCover;
        }

        return proto;
    }
}
