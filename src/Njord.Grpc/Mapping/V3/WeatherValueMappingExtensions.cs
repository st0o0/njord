using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Results;

namespace Njord.Grpc.Mapping.V3;

internal static class WeatherValueMappingExtensions
{
    public static Grpc.V3.WeatherValue ToWeatherValue(this WeatherResult r)
    {
        var proto = new Grpc.V3.WeatherValue
        {
            Temperature = r.Temperature,
            ApparentTemperature = r.ApparentTemperature,
            Humidity = r.Humidity,
            WindSpeed = r.WindSpeed,
            WindDirection = r.WindDirection,
            Pressure = r.Pressure,
            Condition = r.Condition,
        };
        foreach (var point in r.HourlyForecast)
        {
            proto.HourlyForecast.Add(point.ToProto());
        }

        return proto;
    }

    private static Grpc.V3.WeatherForecastPoint ToProto(this WeatherForecastPoint p)
    {
        var proto = new Grpc.V3.WeatherForecastPoint
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
