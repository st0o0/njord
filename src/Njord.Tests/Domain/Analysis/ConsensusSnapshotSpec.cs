using Microsoft.Extensions.Time.Testing;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Analysis;

public sealed class ConsensusSnapshotSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef PrecipSum = ParameterRegistry.GetByApiName("precipitation_sum")!;
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");
    private static readonly WeatherModel Gfs = new("gfs_seamless");

    private static ModelForecast MakeHourlyForecast(
        WeatherModel model, string location, DateTimeOffset baseHour, params (int Hour, double Temp)[] points)
    {
        var forecastPoints = points.Select(p =>
            new ForecastPoint(baseHour.AddHours(p.Hour),
                new Dictionary<ParameterDef, double?> { [Temperature] = p.Temp })).ToList();
        return new ModelForecast(model, location, new CycleId(baseHour),
            new ForecastSeries(forecastPoints), DailyForecastSeries.Empty);
    }

    private static ModelForecast MakeDailyForecast(
        WeatherModel model, string location, DateOnly baseDate,
        params (ParameterDef Param, double?[] Values)[] series)
    {
        var points = new List<DailyForecastPoint>();
        var dayCount = series.Length > 0 ? series[0].Values.Length : 0;

        for (var d = 0; d < dayCount; d++)
        {
            var numericValues = new Dictionary<ParameterDef, double?>();
            foreach (var (param, values) in series)
                numericValues[param] = values[d];
            points.Add(new DailyForecastPoint(baseDate.AddDays(d), numericValues, new Dictionary<ParameterDef, string?>()));
        }

        return new ModelForecast(model, location, new CycleId(T0),
            new ForecastSeries([]), new DailyForecastSeries(points));
    }

    private static ModelForecast MakeCombinedForecast(
        WeatherModel model, string location, DateTimeOffset baseHour,
        (int Hour, double Temp)[] hourlyPoints,
        (ParameterDef Param, double?[] Values)[] dailySeries)
    {
        var forecastPoints = hourlyPoints.Select(p =>
            new ForecastPoint(baseHour.AddHours(p.Hour),
                new Dictionary<ParameterDef, double?> { [Temperature] = p.Temp })).ToList();

        var baseDate = DateOnly.FromDateTime(baseHour.UtcDateTime);
        var dailyPoints = new List<DailyForecastPoint>();
        var dayCount = dailySeries.Length > 0 ? dailySeries[0].Values.Length : 0;
        for (var d = 0; d < dayCount; d++)
        {
            var numericValues = new Dictionary<ParameterDef, double?>();
            foreach (var (param, values) in dailySeries)
                numericValues[param] = values[d];
            dailyPoints.Add(new DailyForecastPoint(baseDate.AddDays(d), numericValues, new Dictionary<ParameterDef, string?>()));
        }

        return new ModelForecast(model, location, new CycleId(baseHour),
            new ForecastSeries(forecastPoints), new DailyForecastSeries(dailyPoints));
    }

    [Fact(Timeout = 5000)]
    public void Compute_produces_snapshot_with_hourly_and_daily_facets()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var snapshot = ModelSnapshot.Empty
            .Update(MakeCombinedForecast(IconD2, "lucerne", T0,
                [(0, 20.0), (1, 21.0), (2, 22.0)],
                [(TempMax, [28.0, 30.0])]))
            .Update(MakeCombinedForecast(Ecmwf, "lucerne", T0,
                [(0, 22.0), (1, 23.0), (2, 24.0)],
                [(TempMax, [31.0, 33.0])]))
            .Update(MakeCombinedForecast(Gfs, "lucerne", T0,
                [(0, 21.0), (1, 22.0), (2, 23.0)],
                [(TempMax, [29.5, 31.5])]));

        var parameters = new ResolvedParameterSet([Temperature], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "lucerne", timeProvider);

        Assert.Equal("lucerne", result.Location);
        Assert.NotEmpty(result.Hourly.Parameters);
        Assert.NotEmpty(result.Daily.Parameters);
    }

    [Fact(Timeout = 5000)]
    public void Compute_returns_empty_facets_for_location_with_no_data()
    {
        var snapshot = ModelSnapshot.Empty;
        var parameters = new ResolvedParameterSet([Temperature], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "zurich", timeProvider);

        Assert.Equal("zurich", result.Location);
        Assert.Empty(result.Hourly.Parameters);
        Assert.Equal(-1, result.Hourly.CutoffHour);
        Assert.Empty(result.Daily.Parameters);
        Assert.Equal(0, result.Daily.CutoffDay);
    }

    [Fact(Timeout = 5000)]
    public void CutoffHour_is_second_to_last_model_max_hour()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (24, 21.0), (48, 22.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (0, 22.0), (24, 23.0), (48, 24.0), (72, 25.0)))
            .Update(MakeHourlyForecast(Gfs, "lucerne", T0, (0, 21.0), (24, 22.0), (48, 23.0), (72, 24.0), (96, 25.0), (120, 26.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "lucerne", timeProvider);

        Assert.Equal(72, result.Hourly.CutoffHour);
    }

    [Fact(Timeout = 5000)]
    public void CutoffHour_is_negative_one_with_fewer_than_two_models()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (24, 21.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "lucerne", timeProvider);

        Assert.Equal(-1, result.Hourly.CutoffHour);
        Assert.Empty(result.Hourly.Parameters);
    }

    [Fact(Timeout = 5000)]
    public void CutoffDay_is_second_to_last_model_day_count()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var snapshot = ModelSnapshot.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [28.0, 30.0, 32.0])))
            .Update(MakeDailyForecast(Ecmwf, "lucerne", baseDate, (TempMax, [31.0, 33.0, 35.0, 37.0, 39.0])))
            .Update(MakeDailyForecast(Gfs, "lucerne", baseDate, (TempMax, [29.5, 31.5, 33.5, 35.5, 37.5, 39.5, 41.5])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "lucerne", timeProvider);

        Assert.Equal(5, result.Daily.CutoffDay);
    }

    [Fact(Timeout = 5000)]
    public void Horizons_with_fewer_than_two_models_are_filtered_out()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (1, 21.0), (2, 22.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (0, 22.0), (1, 23.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "lucerne", timeProvider);

        var byHorizon = result.Hourly.Parameters[0].ByHorizon;
        Assert.True(byHorizon.ContainsKey("h0"));
        Assert.True(byHorizon.ContainsKey("h1"));
        Assert.False(byHorizon.ContainsKey("h2"));
    }

    [Fact(Timeout = 5000)]
    public void Hourly_consensus_median_matches_expected_value()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (3, 20.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (3, 22.0)))
            .Update(MakeHourlyForecast(Gfs, "lucerne", T0, (3, 21.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "lucerne", timeProvider);

        var h3 = result.Hourly.Parameters[0].ByHorizon["h3"];
        Assert.Equal(21.0, h3.Median);
        Assert.NotNull(h3.Spread);
        Assert.Equal(3, h3.AvailableModels.Count);
    }

    [Fact(Timeout = 5000)]
    public void Daily_consensus_from_model_daily_parameters()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var snapshot = ModelSnapshot.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [28.0, 30.0])))
            .Update(MakeDailyForecast(Ecmwf, "lucerne", baseDate, (TempMax, [31.0, 33.0])))
            .Update(MakeDailyForecast(Gfs, "lucerne", baseDate, (TempMax, [29.5, 31.5])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "lucerne", timeProvider);

        Assert.Single(result.Daily.Parameters);
        var d0 = result.Daily.Parameters[0].ByHorizon["d0"];
        Assert.Equal(29.5, d0.Median);
        Assert.Equal(3, d0.AvailableModels.Count);
    }

    [Fact(Timeout = 5000)]
    public void Daily_consensus_filters_single_model_horizons()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var snapshot = ModelSnapshot.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [28.0, 30.0, 32.0])))
            .Update(MakeDailyForecast(Ecmwf, "lucerne", baseDate, (TempMax, [31.0, 33.0])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusSnapshot.Compute(snapshot, parameters, "lucerne", timeProvider);

        var byHorizon = result.Daily.Parameters[0].ByHorizon;
        Assert.True(byHorizon.ContainsKey("d0"));
        Assert.True(byHorizon.ContainsKey("d1"));
        Assert.False(byHorizon.ContainsKey("d2"));
    }
}
