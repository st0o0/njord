using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.Configuration;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Tests.Shared;

public sealed record LocationAnalysis(string Location, AlertResult Alerts);

public sealed class AlertScenario
{
    public static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    public const string Lucerne = "Lucerne";
    public const string Bern = "Bern";

    public static readonly IReadOnlyList<string> Locations = [Lucerne, Bern];

    public static readonly ResolvedParameterSet Parameters = new(
        [
            ParameterRegistry.Temperature2m,
            ParameterRegistry.ApparentTemperature,
            ParameterRegistry.RelativeHumidity2m,
            ParameterRegistry.DewPoint2m,
            ParameterRegistry.WindSpeed10m,
            ParameterRegistry.WindGusts10m,
            ParameterRegistry.Precipitation,
            ParameterRegistry.PrecipitationProbability,
            ParameterRegistry.Rain,
            ParameterRegistry.Snowfall,
            ParameterRegistry.FreezingLevelHeight,
            ParameterRegistry.PressureMsl,
            ParameterRegistry.SurfacePressure,
            ParameterRegistry.CloudCover,
            ParameterRegistry.ShortwaveRadiation,
            ParameterRegistry.SunshineDuration,
            ParameterRegistry.UvIndex,
            ParameterRegistry.Cape,
            ParameterRegistry.WeatherCode,
            ParameterRegistry.IsDay,
            ParameterRegistry.SoilTemperature0cm,
            ParameterRegistry.Visibility,
            ParameterRegistry.Et0FaoEvapotranspiration,
        ],
        [
            ParameterRegistry.PrecipitationSum,
            ParameterRegistry.UvIndexMax,
            ParameterRegistry.SnowfallSum,
            ParameterRegistry.Temperature2mMax,
            ParameterRegistry.Temperature2mMin,
            ParameterRegistry.DailyWeatherCode,
            ParameterRegistry.Sunrise,
            ParameterRegistry.Sunset,
        ]);

    private AlertScenario(IReadOnlyList<LocationAnalysis> analyses) => Analyses = analyses;

    public IReadOnlyList<LocationAnalysis> Analyses { get; }

    public static AlertScenario Create()
    {
        var alerts = FeatureServices.Alerts(new FakeTimeProvider(Now));
        var settings = AppSettingsDefaults.Options<AlertOptions>().ToSettings();

        var analyses = Locations.Select(location =>
        {
            var weather = Weather(location);
            var result = alerts.Compute(weather, settings, Now) ?? throw new InvalidOperationException("Alerts are disabled.");
            return new LocationAnalysis(location, new AlertResult(new Location(location, 0, 0), result));
        });

        return new AlertScenario([.. analyses]);
    }

    public LocationAnalysis For(string location) =>
        Analyses.Single(a => a.Location == location);

    public static DomainForecast<WeatherPoint> Weather(string location) =>
        WeatherForecastBuilder.ForLocation(location)
            .Starting(Now)
            .WithHours(49)
            .Shape(location == Lucerne ? LucernePoint : BernPoint)
            .Build();

    private static bool IsDay(int hour) => (Now.Hour + hour) % 24 is >= 6 and <= 19;

    private static WeatherPoint LucernePoint(int h, WeatherPoint point)
    {
        var day = IsDay(h);
        var fog = h is >= 10 and <= 13;
        var temperature = h switch
        {
            2 => -6.0,
            3 => 25.0,
            4 => 30.0,
            48 => 5.0,
            _ when !day && h <= 24 => 24.0,
            _ => day ? 20.0 : 14.0,
        };

        return point with
        {
            Temperature = temperature,
            ApparentTemperature = h switch { 2 => -22.0, 4 => 36.0, _ => temperature },
            WindSpeed = h switch { 3 => 4.0, 48 => 5.0, _ when fog => 1.5, _ => 4.0 },
            Precipitation = h switch { 5 => 12.0, 6 => 2.0, _ => 0.0 },
            PrecipitationProbability = h is 5 or 6 ? 80.0 : 10.0,
            CloudCover = h == 3 ? 90.0 : 30.0,
            Pressure = h <= 3 ? 1015.0 : 1003.0,
            WeatherCode = h switch { 3 => 63, 5 => 95, 6 => 81, 12 => 45, 24 => 3, 48 => 71, _ => 1 },
            Humidity = fog ? 95.0 : 60.0,
            DewPoint = h switch { 0 => temperature - 1.0, 3 => 22.0, _ when fog => 23.0, _ => temperature - 8.0 },
            UvIndex = h == 3 ? 7.0 : day ? 2.0 : 0.0,
            WindGusts = h == 5 ? 26.0 : 8.0,
            Visibility = h == 10 ? 150.0 : 20000.0,
            Cape = h == 5 ? 1500.0 : 100.0,
            Rain = h switch { 2 => 0.5, 5 => 12.0, 6 => 2.0, _ => 0.0 },
            Snowfall = h == 2 ? 2.0 : 0.0,
            SoilTemperature = h == 2 ? -1.0 : 15.0,
            FreezingLevelHeight = 2500.0,
            IsDay = day,
        };
    }

    private static WeatherPoint BernPoint(int h, WeatherPoint point)
    {
        var day = IsDay(h);
        var temperature = day ? 20.0 : 18.0;

        return point with
        {
            Temperature = temperature,
            ApparentTemperature = temperature,
            WindSpeed = 2.9,
            Precipitation = 0.0,
            PrecipitationProbability = 5.0,
            CloudCover = 20.0,
            Pressure = 1013.0,
            WeatherCode = h switch { 3 => 2, 6 => 150, _ => 1 },
            Humidity = 60.0,
            DewPoint = 0.0,
            UvIndex = day ? 2.0 : 0.0,
            WindGusts = 6.0,
            Visibility = 30000.0,
            Cape = 50.0,
            Rain = 0.0,
            Snowfall = 0.0,
            SoilTemperature = 15.0,
            FreezingLevelHeight = null,
            IsDay = day,
        };
    }
}
