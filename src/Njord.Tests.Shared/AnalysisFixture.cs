using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.Configuration;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Tests.Shared;

public sealed record LocationAnalysis(string Location, AlertResult Alerts);

public sealed class AnalysisFixture
{
    public static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    public const string Lucerne = "Lucerne";
    public const string Bern = "Bern";
    public const int ForecastDays = 4;

    public static readonly IReadOnlyList<string> Locations = [Lucerne, Bern];
    public static readonly WeatherModel IconD2 = new("icon_d2");
    public static readonly WeatherModel IconEu = new("icon_eu");
    public static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");
    public static readonly WeatherModel Gfs = new("gfs_seamless");
    public static readonly IReadOnlyList<WeatherModel> Models = [IconD2, Ecmwf, Gfs];

    public static IReadOnlyList<WeatherModel> ModelsFor(string location) =>
        location == Lucerne ? [IconD2, IconEu, Ecmwf, Gfs] : Models;
    public static readonly IReadOnlyList<int> Horizons = [3, 6, 12, 24, 48, 72];

    private const int IconD2LastHour = 48;

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

    private AnalysisFixture(IReadOnlyList<LocationAnalysis> analyses) => Analyses = analyses;

    public IReadOnlyList<LocationAnalysis> Analyses { get; }

    public static AnalysisFixture Create()
    {
        var time = new FakeTimeProvider(Now);
        var alerts = FeatureServices.Alerts(time);
        var alertSettings = AppSettingsDefaults.Options<AlertOptions>().ToSettings();

        var analyses = new List<LocationAnalysis>();
        foreach (var location in Locations)
        {
            var weather = ConsensusWeather(location);
            analyses.Add(new LocationAnalysis(
                location,
                new AlertResult(
                    new Location(location, 0, 0),
                    alerts.Compute(weather, alertSettings, Now) ?? throw new InvalidOperationException("Alerts are disabled."))));
        }

        return new AnalysisFixture(analyses);
    }

    public LocationAnalysis For(string location) =>
        Analyses.Single(a => a.Location == location);

    private static DomainForecast<WeatherPoint> ConsensusWeather(string location)
    {
        var models = ModelsFor(location);

        double? Median(ParameterDef parameter, int hour)
        {
            var values = models
                .Select(model => HourlyValue(location, parameter, hour, model, isPrevious: false))
                .OfType<double>()
                .Order()
                .ToArray();
            if (values.Length == 0)
            {
                return null;
            }

            var mid = values.Length / 2;
            return values.Length % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2.0;
        }

        double Required(ParameterDef parameter, int hour) => Median(parameter, hour) ?? 0.0;

        var points = Enumerable.Range(0, IconD2LastHour + 1).Select(hour => new WeatherPoint(
            Now.AddHours(hour),
            Required(ParameterRegistry.Temperature2m, hour),
            Required(ParameterRegistry.ApparentTemperature, hour),
            Required(ParameterRegistry.WindSpeed10m, hour),
            Required(ParameterRegistry.Precipitation, hour),
            Required(ParameterRegistry.PrecipitationProbability, hour),
            Required(ParameterRegistry.CloudCover, hour),
            Required(ParameterRegistry.PressureMsl, hour),
            (int)Required(ParameterRegistry.WeatherCode, hour),
            Required(ParameterRegistry.RelativeHumidity2m, hour),
            Required(ParameterRegistry.DewPoint2m, hour),
            Required(ParameterRegistry.UvIndex, hour),
            EnsembleStdDev: 0.5,
            WindGusts: Median(ParameterRegistry.WindGusts10m, hour),
            Visibility: Median(ParameterRegistry.Visibility, hour),
            Cape: Median(ParameterRegistry.Cape, hour),
            Rain: Median(ParameterRegistry.Rain, hour),
            Snowfall: Median(ParameterRegistry.Snowfall, hour),
            SoilTemperature: Median(ParameterRegistry.SoilTemperature0cm, hour),
            FreezingLevelHeight: Median(ParameterRegistry.FreezingLevelHeight, hour),
            IsDay: Median(ParameterRegistry.IsDay, hour) is { } isDay ? isDay > 0.5 : null));

        return new DomainForecast<WeatherPoint>(DomainKind.Weather, Now, TimeResolution.Hourly, [.. points]);
    }

    private static bool IsDay(int h)
    {
        var hourOfDay = (Now.Hour + h) % 24;
        return hourOfDay is >= 6 and <= 19;
    }

    private static double ModelSign(string location, WeatherModel model) => model.Id switch
    {
        "icon_d2" => 1.0,
        "icon_eu" => 0.5,
        "ecmwf_ifs025" => location == Lucerne ? -0.5 : 0.0,
        _ => -1.0,
    };

    private static double? HourlyValue(
        string location, ParameterDef parameter, int h, WeatherModel model, bool isPrevious)
    {
        var baseValue = location == Lucerne
            ? LucerneHourly(parameter, h, isPrevious)
            : BernHourly(parameter, h, model);
        if (baseValue is not { } value)
        {
            return null;
        }

        var spread = HourlySpread(location, parameter, h, value, isPrevious);
        return Math.Round(value + ModelSign(location, model) * spread, 2);
    }

    private static double HourlySpread(string location, ParameterDef parameter, int h, double value, bool isPrevious)
    {
        if (parameter == ParameterRegistry.Temperature2m
            || parameter == ParameterRegistry.ApparentTemperature
            || parameter == ParameterRegistry.DewPoint2m)
        {
            var spread = 0.3 + 0.05 * h;
            var factor = (location, isPrevious) switch
            {
                (Lucerne, true) => 2.0,
                (Bern, true) => 0.5,
                _ => 1.0,
            };
            return spread * factor;
        }

        if (value == 0)
        {
            return 0;
        }

        if (parameter == ParameterRegistry.WindSpeed10m || parameter == ParameterRegistry.WindGusts10m)
        {
            return 0.2;
        }

        if (parameter == ParameterRegistry.Precipitation || parameter == ParameterRegistry.Rain)
        {
            return 0.3;
        }

        if (parameter == ParameterRegistry.RelativeHumidity2m || parameter == ParameterRegistry.CloudCover)
        {
            return 2.0;
        }

        if (parameter == ParameterRegistry.PressureMsl || parameter == ParameterRegistry.SurfacePressure)
        {
            return 0.5;
        }

        return 0;
    }

    private static double? LucerneHourly(ParameterDef p, int h, bool isPrevious)
    {
        var day = IsDay(h);
        var fog = h is >= 10 and <= 13;
        var temperature = h switch
        {
            2 => -6.0,
            3 => isPrevious ? 22.0 : 25.0,
            4 => 30.0,
            48 => 5.0,
            _ when !day && h <= 24 => 24.0,
            _ => day ? 20.0 : 14.0,
        };

        if (p == ParameterRegistry.Temperature2m)
        {
            return temperature;
        }

        if (p == ParameterRegistry.ApparentTemperature)
        {
            return h switch
            {
                2 => -22.0,
                4 => 36.0,
                _ => temperature,
            };
        }

        if (p == ParameterRegistry.DewPoint2m)
        {
            return h switch
            {
                0 => temperature - 1.0,
                3 => 22.0,
                _ when fog => 23.0,
                _ => temperature - 8.0,
            };
        }

        if (p == ParameterRegistry.RelativeHumidity2m)
        {
            return fog ? 95.0 : 60.0;
        }

        if (p == ParameterRegistry.WindSpeed10m)
        {
            return h switch
            {
                3 => isPrevious ? 6.0 : 4.0,
                48 => 5.0,
                _ when fog => 1.5,
                _ => 4.0,
            };
        }

        if (p == ParameterRegistry.WindGusts10m)
        {
            return h == 5 ? 26.0 : 8.0;
        }

        if (p == ParameterRegistry.Precipitation)
        {
            return h switch { 5 => 12.0, 6 => 2.0, _ => 0.0 };
        }

        if (p == ParameterRegistry.Rain)
        {
            return h switch { 2 => 0.5, 5 => 12.0, 6 => 2.0, _ => 0.0 };
        }

        if (p == ParameterRegistry.PrecipitationProbability)
        {
            return h is 5 or 6 ? 80.0 : 10.0;
        }

        if (p == ParameterRegistry.Snowfall)
        {
            return h == 2 ? 2.0 : 0.0;
        }

        if (p == ParameterRegistry.FreezingLevelHeight)
        {
            return 2500.0;
        }

        if (p == ParameterRegistry.PressureMsl)
        {
            return h <= 3 ? 1015.0 : 1003.0;
        }

        if (p == ParameterRegistry.SurfacePressure)
        {
            return 1000.0;
        }

        if (p == ParameterRegistry.CloudCover)
        {
            if (isPrevious)
            {
                return null;
            }

            return h == 3 ? 90.0 : 30.0;
        }

        if (p == ParameterRegistry.ShortwaveRadiation)
        {
            return day ? 500.0 : 0.0;
        }

        if (p == ParameterRegistry.SunshineDuration)
        {
            return day ? 2400.0 : 0.0;
        }

        if (p == ParameterRegistry.UvIndex)
        {
            return h == 3 ? 7.0 : day ? 2.0 : 0.0;
        }

        if (p == ParameterRegistry.Cape)
        {
            return h == 5 ? 1500.0 : 100.0;
        }

        if (p == ParameterRegistry.WeatherCode)
        {
            return h switch
            {
                3 => isPrevious ? 1.0 : 63.0,
                5 => 95.0,
                6 => 81.0,
                12 => 45.0,
                24 => 3.0,
                48 => 71.0,
                72 => 0.0,
                _ => 1.0,
            };
        }

        if (p == ParameterRegistry.IsDay)
        {
            return day ? 1.0 : 0.0;
        }

        if (p == ParameterRegistry.SoilTemperature0cm)
        {
            return h == 2 ? -1.0 : 15.0;
        }

        if (p == ParameterRegistry.Visibility)
        {
            return h == 10 ? 150.0 : 20000.0;
        }

        if (p == ParameterRegistry.Et0FaoEvapotranspiration)
        {
            return day ? 0.3 : 0.05;
        }

        return null;
    }

    private static double? BernHourly(ParameterDef p, int h, WeatherModel model)
    {
        var day = IsDay(h);
        var temperature = day ? 20.0 : 18.0;

        if (p == ParameterRegistry.Temperature2m || p == ParameterRegistry.ApparentTemperature)
        {
            return temperature;
        }

        if (p == ParameterRegistry.DewPoint2m
            || p == ParameterRegistry.FreezingLevelHeight
            || p == ParameterRegistry.SurfacePressure)
        {
            return null;
        }

        if (p == ParameterRegistry.RelativeHumidity2m)
        {
            return 60.0;
        }

        if (p == ParameterRegistry.WindSpeed10m)
        {
            return model == IconD2 ? null : 3.0;
        }

        if (p == ParameterRegistry.WindGusts10m)
        {
            return 6.0;
        }

        if (p == ParameterRegistry.Precipitation
            || p == ParameterRegistry.Rain
            || p == ParameterRegistry.Snowfall)
        {
            return 0.0;
        }

        if (p == ParameterRegistry.PrecipitationProbability)
        {
            return 5.0;
        }

        if (p == ParameterRegistry.PressureMsl)
        {
            return 1013.0;
        }

        if (p == ParameterRegistry.CloudCover)
        {
            return 20.0;
        }

        if (p == ParameterRegistry.ShortwaveRadiation)
        {
            return day ? 400.0 : 0.0;
        }

        if (p == ParameterRegistry.SunshineDuration)
        {
            return day ? 3000.0 : 0.0;
        }

        if (p == ParameterRegistry.UvIndex)
        {
            return day ? 2.0 : 0.0;
        }

        if (p == ParameterRegistry.Cape)
        {
            return 50.0;
        }

        if (p == ParameterRegistry.WeatherCode)
        {
            return h switch
            {
                3 => 2.0,
                6 => 150.0,
                _ => 1.0,
            };
        }

        if (p == ParameterRegistry.IsDay)
        {
            return day ? 1.0 : 0.0;
        }

        if (p == ParameterRegistry.SoilTemperature0cm)
        {
            return 15.0;
        }

        if (p == ParameterRegistry.Visibility)
        {
            return 30000.0;
        }

        if (p == ParameterRegistry.Et0FaoEvapotranspiration)
        {
            return day ? 0.25 : 0.05;
        }

        return null;
    }
}
