using Microsoft.Extensions.Time.Testing;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Indices;
using Njord.Indices;
using Njord.Weather;

namespace Njord.Tests.Shared;

public sealed record LocationAnalysis(
    string Location,
    ConsensusSnapshot Consensus,
    ConsensusSnapshot PreviousConsensus,
    AlertResult Alerts,
    DerivedResult Derived,
    TrendResult Trends,
    IndexResult Indices);

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

    // Lucerne gets a fourth model: the consensus IQR (and with it trend stability) needs four values.
    public static IReadOnlyList<WeatherModel> ModelsFor(string location) =>
        location == Lucerne ? [IconD2, IconEu, Ecmwf, Gfs] : Models;
    public static readonly IReadOnlyList<int> Horizons = [3, 6, 12, 24, 48, 72];

    private const int LastHour = 72;
    private const int IconD2LastHour = 48;
    private const int IconD2Days = 2;

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

    private AnalysisFixture(
        FakeTimeProvider time,
        IReadOnlyList<Forecast> forecasts,
        IReadOnlyList<LocationAnalysis> analyses)
    {
        Time = time;
        Forecasts = forecasts;
        Analyses = analyses;
    }

    public FakeTimeProvider Time { get; }

    public IReadOnlyList<Forecast> Forecasts { get; }

    public IReadOnlyList<LocationAnalysis> Analyses { get; }

    public static IndexOptions IndexOptions => new()
    {
        Enabled = true,
        ScoreOverrides = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase)
        {
            ["running"] = new IndexPreferences { RunningIdealTempLow = 8.0, RunningIdealTempHigh = 18.0 },
        },
        LocationOverrides =
        [
            new LocationIndexOverride
            {
                Location = Bern,
                Preferences = new IndexPreferences { IndoorTemp = 21.0, WindSensitivity = 2.0 },
            },
        ],
    };

    public static AnalysisFixture Create()
    {
        var time = new FakeTimeProvider(Now);
        var forecasts = new List<Forecast>();
        var current = ModelSnapshot.Empty;
        var previous = ModelSnapshot.Empty;

        foreach (var location in Locations)
        {
            foreach (var model in ModelsFor(location))
            {
                var forecast = BuildForecast(location, model, isPrevious: false);
                forecasts.Add(forecast);
                current = current.Update(forecast);
                previous = previous.Update(BuildForecast(location, model, isPrevious: true));
            }
        }

        var factory = new ConsensusBuilder(Parameters, time);
        var alerts = FeatureServices.Alerts(time);
        var indexSettings = IndexOptions.ToSettings(Locations);
        var derivedSettings = new DerivedSettings(Horizons);
        var alertSettings = new AlertOptions().ToSettings();
        var derivedComputer = new DerivedResultComputer(Parameters);
        var indices = FeatureServices.Indices(time);
        var trendComputer = new TrendComputer();

        var analyses = new List<LocationAnalysis>();
        foreach (var location in Locations)
        {
            var consensus = factory.Create(current, location, ConsensusSettings.Default);
            var previousConsensus = factory.Create(previous, location, ConsensusSettings.Default);

            analyses.Add(new LocationAnalysis(
                location,
                consensus,
                previousConsensus,
                alerts.Compute(consensus, alertSettings) ?? throw new InvalidOperationException("Alerts are disabled."),
                derivedComputer.Compute(consensus, derivedSettings),
                trendComputer.Compute(consensus, previousConsensus),
                indices.Compute(consensus, null, indexSettings) ?? throw new InvalidOperationException("Indices are disabled.")));
        }

        return new AnalysisFixture(time, forecasts, analyses);
    }

    public LocationAnalysis For(string location) =>
        Analyses.Single(a => a.Location == location);

    private static Forecast BuildForecast(string location, WeatherModel model, bool isPrevious)
    {
        var lastHour = model == IconD2 ? IconD2LastHour : LastHour;
        var hourly = new List<ForecastPoint>();
        for (var h = 0; h <= lastHour; h++)
        {
            var values = new Dictionary<ParameterDef, double?>();
            foreach (var parameter in Parameters.Hourly)
            {
                values[parameter] = HourlyValue(location, parameter, h, model, isPrevious);
            }

            hourly.Add(new ForecastPoint(Now.AddHours(h), values));
        }

        var days = model == IconD2 ? IconD2Days : ForecastDays;
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var daily = new List<DailyForecastPoint>();
        for (var d = 0; d < days; d++)
        {
            var numeric = new Dictionary<ParameterDef, double?>();
            var meta = new Dictionary<ParameterDef, string?>();
            foreach (var parameter in Parameters.Daily)
            {
                if (parameter.ValueType == ParameterValueType.TimeString)
                {
                    meta[parameter] = DailyMeta(parameter, today.AddDays(d), location, model);
                }
                else
                {
                    numeric[parameter] = DailyValue(location, parameter, d, model);
                }
            }

            daily.Add(new DailyForecastPoint(today.AddDays(d), numeric, meta));
        }

        var cycle = new CycleId(isPrevious ? Now.AddHours(-1) : Now);
        return new Forecast(model, location, cycle, new ForecastSeries(hourly), new DailyForecastSeries(daily));
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

    private static double? DailyValue(string location, ParameterDef p, int d, WeatherModel model)
    {
        var sign = ModelSign(location, model);
        var lucerne = location == Lucerne;

        if (p == ParameterRegistry.PrecipitationSum)
        {
            double[] sums = lucerne ? [30.0, 5.0, 0.0, 1.0] : [0.0, 0.0, 0.0, 0.0];
            return sums[d] == 0 ? 0.0 : sums[d] + sign * 0.5;
        }

        if (p == ParameterRegistry.UvIndexMax)
        {
            double[] uv = lucerne ? [7.0, 5.0, 6.0, 4.0] : [2.0, 2.0, 2.0, 2.0];
            return uv[d] + sign * 0.2;
        }

        if (p == ParameterRegistry.SnowfallSum)
        {
            return lucerne && d == 0 ? 2.0 : 0.0;
        }

        if (p == ParameterRegistry.Temperature2mMax)
        {
            return (lucerne ? 30.0 : 21.0) - d + sign * 0.5;
        }

        if (p == ParameterRegistry.Temperature2mMin)
        {
            return lucerne && d == 3 ? null : (lucerne ? -6.0 : 12.0) + d + sign * 0.5;
        }

        if (p == ParameterRegistry.DailyWeatherCode)
        {
            double[] codes = lucerne ? [63.0, 3.0, 1.0, 2.0] : [1.0, 2.0, 3.0, 1.0];
            return codes[d];
        }

        return null;
    }

    private static string? DailyMeta(ParameterDef p, DateOnly date, string location, WeatherModel model)
    {
        var minuteOffset = (int)Math.Round(ModelSign(location, model) * 2);
        if (p == ParameterRegistry.Sunrise)
        {
            return $"{date:yyyy-MM-dd}T03:{50 + minuteOffset:00}";
        }

        if (p == ParameterRegistry.Sunset)
        {
            return model == Gfs ? null : $"{date:yyyy-MM-dd}T19:{20 + minuteOffset:00}";
        }

        return null;
    }
}
