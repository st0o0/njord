using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;
using Njord.Core.Configuration;
using Njord.Weather;

namespace Njord.Indices.Tests;

public sealed class DaySlicerSpec
{
    private static readonly ParameterDef Temperature = ParameterRegistry.Temperature2m;
    private static readonly ParameterDef Humidity = ParameterRegistry.RelativeHumidity2m;
    private static readonly ParameterDef IsDay = ParameterRegistry.IsDay;

    private static readonly ResolvedParameterSet Parameters = ParameterSelection.Resolve(
        ["Weather", "Solar"], [], []);

    private static ConsensusSnapshot BuildConsensus(
        DateTimeOffset now, int hours,
        Func<int, (double temp, double humidity, double isDay)> hourValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < hours; h++)
        {
            var (temp, hum, isD) = hourValues(h);
            var values = new Dictionary<ParameterDef, double?>
            {
                [Temperature] = temp,
                [Humidity] = hum,
                [IsDay] = isD,
            };
            points.Add(new ForecastPoint(now.AddHours(h), values));
        }

        var points2 = new List<ForecastPoint>();
        for (var h = 0; h < hours; h++)
        {
            var (temp, hum, isD) = hourValues(h);
            var values = new Dictionary<ParameterDef, double?>
            {
                [Temperature] = temp + 1,
                [Humidity] = hum + 1,
                [IsDay] = isD,
            };
            points2.Add(new ForecastPoint(now.AddHours(h), values));
        }

        var m1 = new Forecast(new WeatherModel("m1"), "lucerne", new CycleId(now),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        var m2 = new Forecast(new WeatherModel("m2"), "lucerne", new CycleId(now),
            new ForecastSeries(points2), DailyForecastSeries.Empty);

        var snap = ModelSnapshot.Empty.Update(m1).Update(m2);
        var time = new FakeTimeProvider(now);
        return new ConsensusBuilder(Parameters, time).Create(snap, "lucerne", ConsensusSettings.Default);
    }

    [Fact]
    public void Three_full_days_produces_three_slices()
    {
        var now = new DateTimeOffset(2026, 8, 4, 6, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 72, h =>
        {
            var absHour = (now.AddHours(h).UtcDateTime.Hour);
            var isDay = absHour is >= 6 and < 20 ? 1.0 : 0.0;
            return (20.0, 50.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = DaySlicer.Slice(consensus, time.GetUtcNow());

        Assert.Equal(3, slices.Count);
        Assert.Equal(0, slices[0].DayOffset);
        Assert.Equal(1, slices[1].DayOffset);
        Assert.Equal(2, slices[2].DayOffset);
    }

    [Fact]
    public void Daylight_and_nighttime_hours_partitioned_by_is_day()
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 48, h =>
        {
            var absHour = (now.AddHours(h).UtcDateTime.Hour);
            var isDay = absHour is >= 6 and < 20 ? 1.0 : 0.0;
            return (20.0, 50.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = DaySlicer.Slice(consensus, time.GetUtcNow());

        var d0 = slices.First(s => s.DayOffset == 0);
        Assert.Equal(14, d0.DaylightHoursCount);
        Assert.Equal(10, d0.NighttimeHoursCount);
    }

    [Fact]
    public void Partial_today_has_fewer_hours()
    {
        var now = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 48, h =>
        {
            var absHour = (now.AddHours(h).UtcDateTime.Hour);
            var isDay = absHour is >= 6 and < 20 ? 1.0 : 0.0;
            return (22.0, 55.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = DaySlicer.Slice(consensus, time.GetUtcNow());

        var d0 = slices.First(s => s.DayOffset == 0);
        Assert.Equal(2, d0.DaylightHoursCount);
        Assert.Equal(4, d0.NighttimeHoursCount);
    }

    [Fact]
    public void Midnight_boundary_hour_belongs_to_next_day()
    {
        var now = new DateTimeOffset(2026, 8, 4, 22, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 48, h =>
            (18.0, 50.0, 0.0));

        var time = new FakeTimeProvider(now);
        var slices = DaySlicer.Slice(consensus, time.GetUtcNow());

        var d0 = slices.First(s => s.DayOffset == 0);
        Assert.Equal(2, d0.NighttimeHoursCount + d0.DaylightHoursCount);
    }

    [Fact]
    public void Is_day_missing_treats_all_as_daylight()
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 48; h++)
        {
            var values = new Dictionary<ParameterDef, double?>
            {
                [Temperature] = 20.0,
                [Humidity] = 50.0,
            };
            points.Add(new ForecastPoint(now.AddHours(h), values));
        }

        var points2 = new List<ForecastPoint>();
        for (var h = 0; h < 48; h++)
        {
            var values = new Dictionary<ParameterDef, double?>
            {
                [Temperature] = 21.0,
                [Humidity] = 51.0,
            };
            points2.Add(new ForecastPoint(now.AddHours(h), values));
        }

        var m1 = new Forecast(new WeatherModel("m1"), "lucerne", new CycleId(now),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        var m2 = new Forecast(new WeatherModel("m2"), "lucerne", new CycleId(now),
            new ForecastSeries(points2), DailyForecastSeries.Empty);

        var snap = ModelSnapshot.Empty.Update(m1).Update(m2);
        var time = new FakeTimeProvider(now);
        var consensus = new ConsensusBuilder(Parameters, time).Create(snap, "lucerne", ConsensusSettings.Default);

        var slices = DaySlicer.Slice(consensus, time.GetUtcNow());

        var d0 = slices.First(s => s.DayOffset == 0);
        Assert.Equal(24, d0.DaylightHoursCount);
        Assert.Equal(0, d0.NighttimeHoursCount);
    }

    [Fact]
    public void Fewer_than_three_days_returns_available_slices_only()
    {
        var now = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 30, h =>
        {
            var absHour = (now.AddHours(h).UtcDateTime.Hour);
            var isDay = absHour is >= 6 and < 20 ? 1.0 : 0.0;
            return (20.0, 50.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = DaySlicer.Slice(consensus, time.GetUtcNow());

        Assert.Equal(2, slices.Count);
        Assert.DoesNotContain(slices, s => s.DayOffset == 2);
    }

    [Fact]
    public void Day_means_computed_from_daylight_hours_only()
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 24, h =>
        {
            var isDay = h is >= 6 and < 20 ? 1.0 : 0.0;
            var temp = isDay > 0.5 ? 25.0 : 12.0;
            return (temp, 50.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = DaySlicer.Slice(consensus, time.GetUtcNow());

        var d0 = slices.First(s => s.DayOffset == 0);
        var dayTemp = d0.DayMeans[Temperature];
        var nightTemp = d0.NightMeans[Temperature];

        Assert.NotNull(dayTemp);
        Assert.NotNull(nightTemp);
        Assert.True(dayTemp > 24.0);
        Assert.True(nightTemp < 13.0);
    }

    [Fact]
    public void Empty_consensus_returns_empty_list()
    {
        var now = new DateTimeOffset(2026, 8, 4, 6, 0, 0, TimeSpan.Zero);
        var consensus = new ConsensusSnapshot(new LocationId("lucerne"),
            new HourlyConsensus([], -1),
            new DailyConsensus([], 0),
            now);

        var time = new FakeTimeProvider(now);
        var slices = DaySlicer.Slice(consensus, time.GetUtcNow());

        Assert.Empty(slices);
    }

    [Fact]
    public void Sunshine_percent_is_the_daylight_sunshine_share()
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 24; h++)
        {
            var isDay = h is >= 6 and < 20 ? 1.0 : 0.0;
            points.Add(new ForecastPoint(now.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = 20.0,
                [IsDay] = isDay,
                [ParameterRegistry.SunshineDuration] = isDay > 0.5 ? 1800.0 : 0.0,
            }));
        }

        var forecast = new Forecast(new WeatherModel("m1"), "lucerne", new CycleId(now),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        var snap = ModelSnapshot.Empty.Update(forecast).Update(forecast with { Model = new WeatherModel("m2") });
        var consensus = new ConsensusBuilder(Parameters, new FakeTimeProvider(now)).Create(snap, "lucerne", ConsensusSettings.Default);

        var d0 = DaySlicer.Slice(consensus, now).First(s => s.DayOffset == 0);

        Assert.Equal(50.0, d0.SunshinePercent);
    }

    [Fact]
    public void Single_model_slices_carry_no_confidence_bounds()
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        Forecast Single(string model, ParameterDef parameter) => new(new WeatherModel(model), "lucerne", new CycleId(now),
            new ForecastSeries(Enumerable.Range(0, 24).Select(h => new ForecastPoint(now.AddHours(h),
                new Dictionary<ParameterDef, double?> { [parameter] = 20.0 }))),
            DailyForecastSeries.Empty);
        var snap = ModelSnapshot.Empty.Update(Single("m1", Temperature)).Update(Single("m2", Humidity));
        var consensus = new ConsensusBuilder(Parameters, new FakeTimeProvider(now))
            .Create(snap, "lucerne", ConsensusSettings.Default with { MinQuorum = 1 });

        var slices = DaySlicer.Slice(consensus, now);

        Assert.NotEmpty(slices);
        Assert.All(slices, s => Assert.Null(s.Bounds));
    }

    [Fact]
    public void Multi_model_slices_carry_pessimistic_and_optimistic_means()
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 24, h => (20.0, 50.0, 1.0));

        var d0 = DaySlicer.Slice(consensus, now).First(s => s.DayOffset == 0);

        Assert.NotNull(d0.Bounds);
        var pessimistic = d0.Bounds.Pessimistic.For(DayPeriod.Daylight);
        var optimistic = d0.Bounds.Optimistic.For(DayPeriod.Daylight);
        Assert.True(pessimistic[Temperature] < optimistic[Temperature]);
        Assert.True(pessimistic[Humidity] > optimistic[Humidity]);
    }
}
