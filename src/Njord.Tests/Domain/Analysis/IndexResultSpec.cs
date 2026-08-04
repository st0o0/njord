using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class IndexResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 6, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;
    private static readonly ParameterDef IsDay = ParameterRegistry.IsDay;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather", "Solar"], [], []);

    private static IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> DefaultPrefs(
        string location = "lucerne") =>
        PreferenceResolver.Resolve(new IndexOptions(), [location]);

    private static ModelForecast MakeForecast(
        WeatherModel model, int hours, Func<int, (double temp, double hum, double wind, double cloud, double isDay)> valueFunc)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < hours; h++)
        {
            var (temp, hum, wind, cloud, isD) = valueFunc(h);
            var values = new Dictionary<ParameterDef, double?>
            {
                [Temperature] = temp,
                [Humidity] = hum,
                [WindSpeed] = wind,
                [CloudCover] = cloud,
                [IsDay] = isD,
            };
            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }
        return new ModelForecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private static ModelSnapshot SnapshotWith(params ModelForecast[] forecasts)
    {
        var snap = ModelSnapshot.Empty;
        foreach (var f in forecasts) snap = snap.Update(f);
        return snap;
    }

    [Fact(Timeout = 5000)]
    public void Compute_produces_multiple_day_slices()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), 72, h =>
            {
                var hour = (T0.AddHours(h).UtcDateTime.Hour);
                var isDay = hour >= 6 && hour < 20 ? 1.0 : 0.0;
                return (22.0, 50.0, 3.0, 20.0, isDay);
            }),
            MakeForecast(new("m2"), 72, h =>
            {
                var hour = (T0.AddHours(h).UtcDateTime.Hour);
                var isDay = hour >= 6 && hour < 20 ? 1.0 : 0.0;
                return (21.0, 52.0, 3.5, 22.0, isDay);
            }));

        var time = new FakeTimeProvider(T0);
        var consensus = new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");
        var result = new IndexComputer(Parameters, time).Compute(consensus, DefaultPrefs());

        Assert.Equal("lucerne", result.Location);
        Assert.True(result.Days.Count >= 2);
        Assert.Equal(0, result.Days[0].DayOffset);
        Assert.Equal(1, result.Days[1].DayOffset);
    }

    [Fact(Timeout = 5000)]
    public void Activity_scores_use_daylight_means_only()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), 48, h =>
            {
                var hour = (T0.AddHours(h).UtcDateTime.Hour);
                var isDay = hour >= 6 && hour < 20 ? 1.0 : 0.0;
                var temp = isDay > 0.5 ? 25.0 : 8.0;
                return (temp, 50.0, 3.0, 20.0, isDay);
            }),
            MakeForecast(new("m2"), 48, h =>
            {
                var hour = (T0.AddHours(h).UtcDateTime.Hour);
                var isDay = hour >= 6 && hour < 20 ? 1.0 : 0.0;
                var temp = isDay > 0.5 ? 24.0 : 7.0;
                return (temp, 52.0, 3.5, 22.0, isDay);
            }));

        var time = new FakeTimeProvider(T0);
        var consensus = new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");
        var result = new IndexComputer(Parameters, time).Compute(consensus, DefaultPrefs());

        var d0 = result.Days.First(d => d.DayOffset == 0);
        Assert.InRange(d0.Outdoor, 50, 100);
    }

    [Fact(Timeout = 5000)]
    public void NightVentilation_uses_nighttime_means()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), 48, h =>
            {
                var hour = (T0.AddHours(h).UtcDateTime.Hour);
                var isDay = hour >= 6 && hour < 20 ? 1.0 : 0.0;
                var temp = isDay > 0.5 ? 30.0 : 16.0;
                return (temp, 50.0, 3.0, 20.0, isDay);
            }),
            MakeForecast(new("m2"), 48, h =>
            {
                var hour = (T0.AddHours(h).UtcDateTime.Hour);
                var isDay = hour >= 6 && hour < 20 ? 1.0 : 0.0;
                var temp = isDay > 0.5 ? 31.0 : 17.0;
                return (temp, 52.0, 3.5, 22.0, isDay);
            }));

        var time = new FakeTimeProvider(T0);
        var consensus = new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");
        var result = new IndexComputer(Parameters, time).Compute(consensus, DefaultPrefs());

        var d0 = result.Days.First(d => d.DayOffset == 0);
        Assert.InRange(d0.NightVentilation, 50, 100);
    }

    [Fact(Timeout = 5000)]
    public void Frost_protection_computed_once_not_per_day()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), 48, h =>
                (h == 10 ? -2.0 : 15.0, 50.0, 3.0, 20.0, 1.0)),
            MakeForecast(new("m2"), 48, h =>
                (h == 10 ? -1.0 : 14.0, 52.0, 3.5, 22.0, 1.0)));

        var time = new FakeTimeProvider(T0);
        var consensus = new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");
        var result = new IndexComputer(Parameters, time).Compute(consensus, DefaultPrefs());

        Assert.NotNull(result.FrostProtection);
        Assert.Equal(10, result.FrostProtection!.HoursUntilFrost);
    }

    [Fact(Timeout = 5000)]
    public void Vpd_computed_once()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), 48, h => (25.0, 60.0, 3.0, 20.0, 1.0)),
            MakeForecast(new("m2"), 48, h => (24.0, 62.0, 3.5, 22.0, 1.0)));

        var time = new FakeTimeProvider(T0);
        var consensus = new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");
        var result = new IndexComputer(Parameters, time).Compute(consensus, DefaultPrefs());

        Assert.NotNull(result.Vpd);
    }

    [Fact(Timeout = 5000)]
    public void Multi_model_produces_envelopes_per_day()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), 48, h => (22.0, 50.0, 3.0, 20.0, 1.0)),
            MakeForecast(new("m2"), 48, h => (10.0, 90.0, 15.0, 95.0, 1.0)),
            MakeForecast(new("m3"), 48, h => (20.0, 55.0, 4.0, 30.0, 1.0)));

        var time = new FakeTimeProvider(T0);
        var consensus = new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");
        var result = new IndexComputer(Parameters, time).Compute(consensus, DefaultPrefs());

        var d0 = result.Days.First(d => d.DayOffset == 0);
        Assert.NotNull(d0.OutdoorEnvelope);
        Assert.True(d0.OutdoorEnvelope!.Min <= d0.OutdoorEnvelope.Max);
    }

    [Fact(Timeout = 5000)]
    public void Hours_included_reflects_daylight_or_night_count()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), 48, h =>
            {
                var hour = (T0.AddHours(h).UtcDateTime.Hour);
                var isDay = hour >= 6 && hour < 20 ? 1.0 : 0.0;
                return (20.0, 50.0, 3.0, 20.0, isDay);
            }),
            MakeForecast(new("m2"), 48, h =>
            {
                var hour = (T0.AddHours(h).UtcDateTime.Hour);
                var isDay = hour >= 6 && hour < 20 ? 1.0 : 0.0;
                return (21.0, 52.0, 3.5, 22.0, isDay);
            }));

        var time = new FakeTimeProvider(T0);
        var consensus = new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");
        var result = new IndexComputer(Parameters, time).Compute(consensus, DefaultPrefs());

        var d0 = result.Days.First(d => d.DayOffset == 0);
        Assert.True(d0.HoursIncluded > 0);
    }

    [Fact(Timeout = 5000)]
    public void BuildEnvelope_computes_min_max_confidence()
    {
        var envelope = IndexComputer.BuildEnvelope([70, 72, 71, 73, 70]);

        Assert.Equal(70, envelope.Min);
        Assert.Equal(73, envelope.Max);
        Assert.Equal(1.0, envelope.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void BuildEnvelope_low_confidence_for_wide_spread()
    {
        var envelope = IndexComputer.BuildEnvelope([10, 50, 90]);

        Assert.Equal(10, envelope.Min);
        Assert.Equal(90, envelope.Max);
        Assert.True(envelope.Confidence < 1.0);
    }
}
