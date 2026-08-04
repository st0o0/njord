using Microsoft.Extensions.Time.Testing;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Analysis;

public sealed class TimeSliceAggregatorSpec
{
    private static readonly ParameterDef Temperature = ParameterRegistry.Temperature2m;
    private static readonly ParameterDef Humidity = ParameterRegistry.RelativeHumidity2m;
    private static readonly ParameterDef IsDay = ParameterRegistry.IsDay;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
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

        var m1 = new ModelForecast(new("m1"), "lucerne", new CycleId(now),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        var m2 = new ModelForecast(new("m2"), "lucerne", new CycleId(now),
            new ForecastSeries(points2), DailyForecastSeries.Empty);

        var snap = ModelSnapshot.Empty.Update(m1).Update(m2);
        var time = new FakeTimeProvider(now);
        return new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");
    }

    [Fact(Timeout = 5000)]
    public void Three_full_days_produces_three_slices()
    {
        var now = new DateTimeOffset(2026, 8, 4, 6, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 72, h =>
        {
            var absHour = (now.AddHours(h).UtcDateTime.Hour);
            var isDay = absHour >= 6 && absHour < 20 ? 1.0 : 0.0;
            return (20.0, 50.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, Parameters, time);

        Assert.Equal(3, slices.Count);
        Assert.Equal(0, slices[0].DayOffset);
        Assert.Equal(1, slices[1].DayOffset);
        Assert.Equal(2, slices[2].DayOffset);
    }

    [Fact(Timeout = 5000)]
    public void Daylight_and_nighttime_hours_partitioned_by_is_day()
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 48, h =>
        {
            var absHour = (now.AddHours(h).UtcDateTime.Hour);
            var isDay = absHour >= 6 && absHour < 20 ? 1.0 : 0.0;
            return (20.0, 50.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, Parameters, time);

        var d0 = slices.First(s => s.DayOffset == 0);
        Assert.Equal(14, d0.DaylightHoursCount);
        Assert.Equal(10, d0.NighttimeHoursCount);
    }

    [Fact(Timeout = 5000)]
    public void Partial_today_has_fewer_hours()
    {
        var now = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 48, h =>
        {
            var absHour = (now.AddHours(h).UtcDateTime.Hour);
            var isDay = absHour >= 6 && absHour < 20 ? 1.0 : 0.0;
            return (22.0, 55.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, Parameters, time);

        var d0 = slices.First(s => s.DayOffset == 0);
        Assert.Equal(2, d0.DaylightHoursCount);
        Assert.Equal(4, d0.NighttimeHoursCount);
    }

    [Fact(Timeout = 5000)]
    public void Midnight_boundary_hour_belongs_to_next_day()
    {
        var now = new DateTimeOffset(2026, 8, 4, 22, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 48, h =>
            (18.0, 50.0, 0.0));

        var time = new FakeTimeProvider(now);
        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, Parameters, time);

        var d0 = slices.First(s => s.DayOffset == 0);
        Assert.Equal(2, d0.NighttimeHoursCount + d0.DaylightHoursCount);
    }

    [Fact(Timeout = 5000)]
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

        var m1 = new ModelForecast(new("m1"), "lucerne", new CycleId(now),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        var m2 = new ModelForecast(new("m2"), "lucerne", new CycleId(now),
            new ForecastSeries(points2), DailyForecastSeries.Empty);

        var snap = ModelSnapshot.Empty.Update(m1).Update(m2);
        var time = new FakeTimeProvider(now);
        var consensus = new ConsensusSnapshotFactory(Parameters, time).Create(snap, "lucerne");

        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, Parameters, time);

        var d0 = slices.First(s => s.DayOffset == 0);
        Assert.Equal(24, d0.DaylightHoursCount);
        Assert.Equal(0, d0.NighttimeHoursCount);
    }

    [Fact(Timeout = 5000)]
    public void Fewer_than_three_days_returns_available_slices_only()
    {
        var now = new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 30, h =>
        {
            var absHour = (now.AddHours(h).UtcDateTime.Hour);
            var isDay = absHour >= 6 && absHour < 20 ? 1.0 : 0.0;
            return (20.0, 50.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, Parameters, time);

        Assert.Equal(2, slices.Count);
        Assert.DoesNotContain(slices, s => s.DayOffset == 2);
    }

    [Fact(Timeout = 5000)]
    public void Day_means_computed_from_daylight_hours_only()
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var consensus = BuildConsensus(now, 24, h =>
        {
            var isDay = h >= 6 && h < 20 ? 1.0 : 0.0;
            var temp = isDay > 0.5 ? 25.0 : 12.0;
            return (temp, 50.0, isDay);
        });

        var time = new FakeTimeProvider(now);
        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, Parameters, time);

        var d0 = slices.First(s => s.DayOffset == 0);
        var dayTemp = d0.DayMeans[Temperature];
        var nightTemp = d0.NightMeans[Temperature];

        Assert.NotNull(dayTemp);
        Assert.NotNull(nightTemp);
        Assert.True(dayTemp > 24.0);
        Assert.True(nightTemp < 13.0);
    }

    [Fact(Timeout = 5000)]
    public void Empty_consensus_returns_empty_list()
    {
        var now = new DateTimeOffset(2026, 8, 4, 6, 0, 0, TimeSpan.Zero);
        var consensus = new ConsensusSnapshot("lucerne",
            new HourlyConsensus([], -1),
            new DailyConsensus([], 0),
            now);

        var time = new FakeTimeProvider(now);
        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, Parameters, time);

        Assert.Empty(slices);
    }
}
