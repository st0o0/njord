using System.Collections.Immutable;
using Njord.Alerts.Configuration;
using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Alerts.Tests.Rules;

internal static class AlertRuleFixture
{
    public static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    public static readonly AlertSettings Defaults = AppSettingsDefaults.Options<AlertOptions>().ToSettings();

    public static WeatherPoint Benign(DateTimeOffset validFrom) => new(
        ValidFrom: validFrom,
        Temperature: 15.0,
        ApparentTemperature: 15.0,
        WindSpeed: 5.0,
        Precipitation: 0.0,
        PrecipitationProbability: 0.0,
        CloudCover: 50.0,
        Pressure: 1013.0,
        WeatherCode: 0,
        Humidity: 50.0,
        DewPoint: 5.0,
        UvIndex: 0.0);

    public static DomainForecast<WeatherPoint> Hourly(int hours, Func<int, WeatherPoint, WeatherPoint>? shape = null)
    {
        var points = Enumerable.Range(0, hours)
            .Select(hour =>
            {
                var point = Benign(Now.AddHours(hour));
                return shape is null ? point : shape(hour, point);
            })
            .ToImmutableArray();
        return new DomainForecast<WeatherPoint>(DomainKind.Weather, Now, TimeResolution.Hourly, points);
    }

    public static DomainForecast<WeatherPoint> Day(Func<int, WeatherPoint, WeatherPoint>? shape = null) => Hourly(24, shape);

    public static DomainForecast<WeatherPoint> Uniform(Func<WeatherPoint, WeatherPoint> change) => Day((_, point) => change(point));

    public static DomainForecast<WeatherPoint> Empty() => Hourly(0);
}
