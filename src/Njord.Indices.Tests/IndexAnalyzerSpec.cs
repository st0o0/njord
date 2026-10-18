using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Consensus;
using Njord.Consensus.Settings;
using Njord.Core.Configuration;
using Njord.Indices.Configuration;
using Njord.Messages.Indices;
using Njord.Sensors;
using Njord.Tests.Shared;
using Njord.Model;
using Njord.Forecast;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Indices.Tests;

public sealed class IndexAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 6, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterSelection.Resolve(["Weather"], [], []);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;
    private static readonly ParameterDef IsDay = ParameterRegistry.IsDay;

    [Fact]
    public void Compute_returns_index_result()
    {
        var analyzer = CreateAnalyzer();
        var settings = Settings();

        var result = analyzer.Compute(BuildConsensus(), null, settings);

        Assert.NotNull(result);
        Assert.Equal(new LocationId("lucerne"), result.Location);
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var analyzer = CreateAnalyzer(enabled: false);
        var settings = Settings();

        var result = analyzer.Compute(BuildConsensus(), null, settings);

        Assert.Null(result);
    }

    [Fact]
    public void Compute_with_sensor_overrides_indoor_temp()
    {
        var analyzer = CreateAnalyzer();
        var settings = Settings(indoorTemp: 28.0);
        var consensus = BuildConsensusWithWeather("lucerne");
        var sensors = BuildSensorReadings("lucerne", 18.0);

        var withSensor = analyzer.Compute(consensus, sensors, settings);
        var withoutSensor = analyzer.Compute(consensus, null, settings);

        Assert.NotNull(withSensor);
        Assert.NotNull(withoutSensor);
        Assert.NotEmpty(withSensor.Days);
        Assert.NotEmpty(withoutSensor.Days);
        Assert.NotEqual(withoutSensor.Days[0].Scores[IndexKind.NightVentilation].Value, withSensor.Days[0].Scores[IndexKind.NightVentilation].Value);
    }

    [Fact]
    public void Sensor_override_does_not_persist_across_calls()
    {
        var analyzer = CreateAnalyzer();
        var settings = Settings(indoorTemp: 28.0);
        var consensus = BuildConsensusWithWeather("lucerne");
        var sensors = BuildSensorReadings("lucerne", 18.0);

        _ = analyzer.Compute(consensus, sensors, settings);
        var afterSensorCall = analyzer.Compute(consensus, null, settings);
        var fresh = analyzer.Compute(consensus, null, settings);

        Assert.NotNull(afterSensorCall);
        Assert.NotNull(fresh);
        Assert.NotEmpty(afterSensorCall.Days);
        Assert.NotEmpty(fresh.Days);
        Assert.Equal(fresh.Days[0].Scores[IndexKind.NightVentilation].Value, afterSensorCall.Days[0].Scores[IndexKind.NightVentilation].Value);
    }

    [Fact]
    public void Compute_uses_the_preferences_passed_with_each_computation()
    {
        var analyzer = CreateAnalyzer();
        var consensus = BuildConsensusWithWeather("lucerne");

        var warm = analyzer.Compute(consensus, null, Settings(indoorTemp: 28.0));
        var cool = analyzer.Compute(consensus, null, Settings(indoorTemp: 18.0));

        Assert.NotNull(warm);
        Assert.NotNull(cool);
        Assert.NotEmpty(warm.Days);
        Assert.NotEmpty(cool.Days);
        Assert.NotEqual(warm.Days[0].Scores[IndexKind.NightVentilation].Value, cool.Days[0].Scores[IndexKind.NightVentilation].Value);
    }

    private static IndexAnalyzer CreateAnalyzer(bool enabled = true) => FeatureServices.Indices(Time, enabled);

    private static IndexSettings Settings(double? indoorTemp = null)
    {
        var options = AppSettingsDefaults.Options<IndexOptions>();
        if (indoorTemp.HasValue)
        {
            options.Preferences.IndoorTemp = indoorTemp.Value;
        }

        return options.ToSettings(["lucerne"]);
    }

    private static LocationConsensus BuildConsensus(string location = "lucerne")
    {
        var snap = ModelForecasts.Empty;
        return new ConsensusBuilder(Parameters, Time).Create(snap, location, ConsensusSettings.Default);
    }

    private static LocationConsensus BuildConsensusWithWeather(string location)
    {
        var snap = ModelForecasts.Empty
            .Update(MakeForecast(new WeatherModel("m1"), location, 24, _ => (15.0, 60.0, 3.0, 30.0, 0.0)))
            .Update(MakeForecast(new WeatherModel("m2"), location, 24, _ => (15.0, 60.0, 3.0, 30.0, 0.0)));
        return new ConsensusBuilder(Parameters, Time).Create(snap, location, ConsensusSettings.Default);
    }

    private static WeatherForecast MakeForecast(
        WeatherModel model, string location, int hours,
        Func<int, (double temp, double hum, double wind, double cloud, double isDay)> valueFunc)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < hours; h++)
        {
            var (temp, hum, wind, cloud, isD) = valueFunc(h);
            var values = new Dictionary<ParameterDef, ForecastValue>
            {
                [Temperature] = temp,
                [Humidity] = hum,
                [WindSpeed] = wind,
                [CloudCover] = cloud,
                [IsDay] = isD,
            };
            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }
        return new WeatherForecast(model, location, new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private static SensorReadings BuildSensorReadings(string location, double indoorTemp)
    {
        var readings = new Dictionary<SensorKind, AggregatedReading>
        {
            [SensorKind.IndoorTemperature] = new(indoorTemp, 1, T0),
        };
        return new SensorReadings(location, readings);
    }

    [Fact]
    public void Missing_score_fails_fast_at_construction()
    {
        var scores = Enum.GetValues<IndexKind>().Where(k => k != IndexKind.Solar).Select(k => new FixedScore(k));

        var error = Assert.Throws<InvalidOperationException>(() => new IndexAnalyzer(scores, EnabledOptions, Time));

        Assert.Contains(nameof(IndexKind.Solar), error.Message);
    }

    [Fact]
    public void Duplicate_score_fails_fast_at_construction()
    {
        var scores = Enum.GetValues<IndexKind>().Select(k => new FixedScore(k)).Append(new FixedScore(IndexKind.Bbq));

        var error = Assert.Throws<InvalidOperationException>(() => new IndexAnalyzer(scores, EnabledOptions, Time));

        Assert.Contains(nameof(IndexKind.Bbq), error.Message);
    }

    [Fact]
    public void Reverse_registration_produces_the_same_result()
    {
        using var provider = FeatureServices.Build(Time, services => services.AddIndices(AppSettingsDefaults.Configuration()));
        var registered = provider.GetServices<IIndexScore>().ToList();
        var forward = new IndexAnalyzer(registered, EnabledOptions, Time);
        var reverse = new IndexAnalyzer(Enumerable.Reverse(registered), EnabledOptions, Time);
        var consensus = BuildConsensusWithWeather("lucerne");

        var forwardResult = forward.Compute(consensus, null, Settings());
        var reverseResult = reverse.Compute(consensus, null, Settings());

        Assert.NotNull(forwardResult);
        Assert.NotNull(reverseResult);
        Assert.Equal(forwardResult.Days.Count, reverseResult.Days.Count);
        for (var i = 0; i < forwardResult.Days.Count; i++)
        {
            var fwd = forwardResult.Days[i];
            var rev = reverseResult.Days[i];
            Assert.Equal(fwd.DayOffset, rev.DayOffset);
            Assert.Equal(fwd.HoursIncluded, rev.HoursIncluded);
            foreach (var kind in Enum.GetValues<IndexKind>())
            {
                Assert.Equal(fwd.Scores[kind].Value, rev.Scores[kind].Value);
                Assert.Equal(fwd.Scores[kind].Envelope, rev.Scores[kind].Envelope);
            }
        }
    }

    [Fact]
    public void Add_indices_registers_the_analyzer_and_one_score_per_index_kind()
    {
        using var provider = FeatureServices.Build(Time, services => services.AddIndices(AppSettingsDefaults.Configuration()));

        var kinds = provider.GetServices<IIndexScore>().Select(s => s.Kind).Order();

        Assert.Equal(Enum.GetValues<IndexKind>(), kinds);
        Assert.NotNull(provider.GetRequiredService<IndexAnalyzer>());
    }

    private static readonly IOptions<IndexOptions> EnabledOptions = Options.Create(new IndexOptions { Enabled = true });

    private sealed class FixedScore(IndexKind kind) : IIndexScore
    {
        public IndexKind Kind => kind;

        public ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences) => new(50, null);
    }

    // Ported from Njord.Consensus.Tests IndexResultSpec (formerly via IndexComputer).

    private static IndexResult Analyze(FakeTimeProvider time, LocationConsensus consensus)
    {
        var result = FeatureServices.Indices(time).Compute(consensus, null, ResultPrefs());
        Assert.NotNull(result);
        return result;
    }

    private static readonly DateTimeOffset ResultT0 = new(2026, 7, 11, 6, 0, 0, TimeSpan.Zero);

    private static readonly ResolvedParameterSet ResultParameters = ParameterSelection.Resolve(
        ["Weather", "Solar"], [], []);

    private static IndexSettings ResultPrefs(string location = "lucerne") =>
        new(IndexKinds.All.ToDictionary(kind => (new LocationId(location), kind), _ => ResolvedPreferences.Default));

    private static WeatherForecast MakeResultForecast(
        WeatherModel model, int hours, Func<int, (double temp, double hum, double wind, double cloud, double isDay)> valueFunc)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < hours; h++)
        {
            var (temp, hum, wind, cloud, isD) = valueFunc(h);
            var values = new Dictionary<ParameterDef, ForecastValue>
            {
                [Temperature] = temp,
                [Humidity] = hum,
                [WindSpeed] = wind,
                [CloudCover] = cloud,
                [IsDay] = isD,
            };
            points.Add(new ForecastPoint(ResultT0.AddHours(h), values));
        }
        return new WeatherForecast(model, "lucerne", new CycleId(ResultT0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private static ModelForecasts ResultForecastsWith(params WeatherForecast[] forecasts)
    {
        var snap = ModelForecasts.Empty;
        foreach (var f in forecasts)
        {
            snap = snap.Update(f);
        }

        return snap;
    }

    [Fact]
    public void Compute_produces_multiple_day_slices()
    {
        var snap = ResultForecastsWith(
            MakeResultForecast(new WeatherModel("m1"), 72, h =>
            {
                var hour = ResultT0.AddHours(h).UtcDateTime.Hour;
                var isDay = hour is >= 6 and < 20 ? 1.0 : 0.0;
                return (22.0, 50.0, 3.0, 20.0, isDay);
            }),
            MakeResultForecast(new WeatherModel("m2"), 72, h =>
            {
                var hour = ResultT0.AddHours(h).UtcDateTime.Hour;
                var isDay = hour is >= 6 and < 20 ? 1.0 : 0.0;
                return (21.0, 52.0, 3.5, 22.0, isDay);
            }));

        var time = new FakeTimeProvider(ResultT0);
        var consensus = new ConsensusBuilder(ResultParameters, time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = Analyze(time, consensus);

        Assert.Equal(new LocationId("lucerne"), result.Location);
        Assert.True(result.Days.Count >= 2);
        Assert.Equal(0, result.Days[0].DayOffset);
        Assert.Equal(1, result.Days[1].DayOffset);
    }

    [Fact]
    public void Activity_scores_use_daylight_means_only()
    {
        var snap = ResultForecastsWith(
            MakeResultForecast(new WeatherModel("m1"), 48, h =>
            {
                var hour = ResultT0.AddHours(h).UtcDateTime.Hour;
                var isDay = hour is >= 6 and < 20 ? 1.0 : 0.0;
                var temp = isDay > 0.5 ? 25.0 : 8.0;
                return (temp, 50.0, 3.0, 20.0, isDay);
            }),
            MakeResultForecast(new WeatherModel("m2"), 48, h =>
            {
                var hour = ResultT0.AddHours(h).UtcDateTime.Hour;
                var isDay = hour is >= 6 and < 20 ? 1.0 : 0.0;
                var temp = isDay > 0.5 ? 24.0 : 7.0;
                return (temp, 52.0, 3.5, 22.0, isDay);
            }));

        var time = new FakeTimeProvider(ResultT0);
        var consensus = new ConsensusBuilder(ResultParameters, time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = Analyze(time, consensus);

        var d0 = result.Days.First(d => d.DayOffset == 0);
        Assert.InRange(d0.Scores[IndexKind.Outdoor].Value, 50, 100);
    }

    [Fact]
    public void NightVentilation_uses_nighttime_means()
    {
        var snap = ResultForecastsWith(
            MakeResultForecast(new WeatherModel("m1"), 48, h =>
            {
                var hour = ResultT0.AddHours(h).UtcDateTime.Hour;
                var isDay = hour is >= 6 and < 20 ? 1.0 : 0.0;
                var temp = isDay > 0.5 ? 30.0 : 16.0;
                return (temp, 50.0, 3.0, 20.0, isDay);
            }),
            MakeResultForecast(new WeatherModel("m2"), 48, h =>
            {
                var hour = ResultT0.AddHours(h).UtcDateTime.Hour;
                var isDay = hour is >= 6 and < 20 ? 1.0 : 0.0;
                var temp = isDay > 0.5 ? 31.0 : 17.0;
                return (temp, 52.0, 3.5, 22.0, isDay);
            }));

        var time = new FakeTimeProvider(ResultT0);
        var consensus = new ConsensusBuilder(ResultParameters, time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = Analyze(time, consensus);

        var d0 = result.Days.First(d => d.DayOffset == 0);
        Assert.InRange(d0.Scores[IndexKind.NightVentilation].Value, 50, 100);
    }

    [Fact]
    public void Frost_protection_computed_once_not_per_day()
    {
        var snap = ResultForecastsWith(
            MakeResultForecast(new WeatherModel("m1"), 48, h =>
                (h == 10 ? -2.0 : 15.0, 50.0, 3.0, 20.0, 1.0)),
            MakeResultForecast(new WeatherModel("m2"), 48, h =>
                (h == 10 ? -1.0 : 14.0, 52.0, 3.5, 22.0, 1.0)));

        var time = new FakeTimeProvider(ResultT0);
        var consensus = new ConsensusBuilder(ResultParameters, time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = Analyze(time, consensus);

        Assert.NotNull(result.FrostProtection);
        Assert.Equal(10, result.FrostProtection.HoursUntilFrost);
    }

    [Fact]
    public void Vpd_computed_once()
    {
        var snap = ResultForecastsWith(
            MakeResultForecast(new WeatherModel("m1"), 48, h => (25.0, 60.0, 3.0, 20.0, 1.0)),
            MakeResultForecast(new WeatherModel("m2"), 48, h => (24.0, 62.0, 3.5, 22.0, 1.0)));

        var time = new FakeTimeProvider(ResultT0);
        var consensus = new ConsensusBuilder(ResultParameters, time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = Analyze(time, consensus);

        Assert.NotNull(result.Vpd);
    }

    [Fact]
    public void Multi_model_produces_envelopes_per_day()
    {
        var snap = ResultForecastsWith(
            MakeResultForecast(new WeatherModel("m1"), 48, h => (22.0, 50.0, 3.0, 20.0, 1.0)),
            MakeResultForecast(new WeatherModel("m2"), 48, h => (10.0, 90.0, 15.0, 95.0, 1.0)),
            MakeResultForecast(new WeatherModel("m3"), 48, h => (20.0, 55.0, 4.0, 30.0, 1.0)));

        var time = new FakeTimeProvider(ResultT0);
        var consensus = new ConsensusBuilder(ResultParameters, time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = Analyze(time, consensus);

        var d0 = result.Days.First(d => d.DayOffset == 0);
        var outdoor = d0.Scores[IndexKind.Outdoor];
        Assert.NotNull(outdoor.Envelope);
        Assert.True(outdoor.Envelope.Min <= outdoor.Envelope.Max);
    }

    [Fact]
    public void Hours_included_reflects_daylight_or_night_count()
    {
        var snap = ResultForecastsWith(
            MakeResultForecast(new WeatherModel("m1"), 48, h =>
            {
                var hour = ResultT0.AddHours(h).UtcDateTime.Hour;
                var isDay = hour is >= 6 and < 20 ? 1.0 : 0.0;
                return (20.0, 50.0, 3.0, 20.0, isDay);
            }),
            MakeResultForecast(new WeatherModel("m2"), 48, h =>
            {
                var hour = ResultT0.AddHours(h).UtcDateTime.Hour;
                var isDay = hour is >= 6 and < 20 ? 1.0 : 0.0;
                return (21.0, 52.0, 3.5, 22.0, isDay);
            }));

        var time = new FakeTimeProvider(ResultT0);
        var consensus = new ConsensusBuilder(ResultParameters, time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = Analyze(time, consensus);

        var d0 = result.Days.First(d => d.DayOffset == 0);
        Assert.True(d0.HoursIncluded > 0);
    }
}
