using Microsoft.Extensions.Time.Testing;
using Njord.Consensus.Settings;
using Njord.Forecast;
using Njord.Model;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Consensus.Tests.Consensus;

public sealed class ConsensusBuilderSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef PrecipSum = ParameterRegistry.GetByApiName("precipitation_sum")!;
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");
    private static readonly WeatherModel Gfs = new("gfs_seamless");

    private static WeatherForecast MakeHourlyForecast(
        WeatherModel model, string location, DateTimeOffset baseHour, params (int Hour, double Temp)[] points)
    {
        var forecastPoints = points.Select(p =>
            new ForecastPoint(baseHour.AddHours(p.Hour),
                new Dictionary<ParameterDef, ForecastValue> { [Temperature] = p.Temp })).ToList();
        return new WeatherForecast(model, location, new CycleId(baseHour),
            new ForecastSeries(forecastPoints), DailyForecastSeries.Empty);
    }

    private static WeatherForecast MakeDailyForecast(
        WeatherModel model, string location, DateOnly baseDate,
        params (ParameterDef Param, double?[] Values)[] series)
    {
        var points = new List<DailyForecastPoint>();
        var dayCount = series.Length > 0 ? series[0].Values.Length : 0;

        for (var d = 0; d < dayCount; d++)
        {
            var numericValues = new Dictionary<ParameterDef, ForecastValue>();
            foreach (var (param, values) in series)
            {
                numericValues[param] = values[d];
            }

            points.Add(new DailyForecastPoint(baseDate.AddDays(d), numericValues));
        }

        return new WeatherForecast(model, location, new CycleId(T0),
            new ForecastSeries([]), new DailyForecastSeries(points));
    }

    private static WeatherForecast MakeCombinedForecast(
        WeatherModel model, string location, DateTimeOffset baseHour,
        (int Hour, double Temp)[] hourlyPoints,
        (ParameterDef Param, double?[] Values)[] dailySeries)
    {
        var forecastPoints = hourlyPoints.Select(p =>
            new ForecastPoint(baseHour.AddHours(p.Hour),
                new Dictionary<ParameterDef, ForecastValue> { [Temperature] = p.Temp })).ToList();

        var baseDate = DateOnly.FromDateTime(baseHour.UtcDateTime);
        var dailyPoints = new List<DailyForecastPoint>();
        var dayCount = dailySeries.Length > 0 ? dailySeries[0].Values.Length : 0;
        for (var d = 0; d < dayCount; d++)
        {
            var numericValues = new Dictionary<ParameterDef, ForecastValue>();
            foreach (var (param, values) in dailySeries)
            {
                numericValues[param] = values[d];
            }

            dailyPoints.Add(new DailyForecastPoint(baseDate.AddDays(d), numericValues));
        }

        return new WeatherForecast(model, location, new CycleId(baseHour),
            new ForecastSeries(forecastPoints), new DailyForecastSeries(dailyPoints));
    }

    [Fact]
    public void Compute_produces_consensus_with_hourly_and_daily_facets()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var forecasts = ModelForecasts.Empty
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

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(new LocationId("lucerne"), result.Location);
        Assert.NotEmpty(result.Hourly.Parameters);
        Assert.NotEmpty(result.Daily.Parameters);
        Assert.Equal(T0, result.ComputedAt);
    }

    [Fact]
    public void Compute_returns_empty_facets_for_location_with_no_data()
    {
        var forecasts = ModelForecasts.Empty;
        var parameters = new ResolvedParameterSet([Temperature], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "zurich", ConsensusSettings.Default);

        Assert.Equal(new LocationId("zurich"), result.Location);
        Assert.Empty(result.Hourly.Parameters);
        Assert.Equal(-1, result.Hourly.CutoffHour);
        Assert.Empty(result.Daily.Parameters);
        Assert.Equal(0, result.Daily.CutoffDay);
    }

    [Fact]
    public void CutoffHour_is_second_to_last_model_max_hour()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (24, 21.0), (48, 22.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (0, 22.0), (24, 23.0), (48, 24.0), (72, 25.0)))
            .Update(MakeHourlyForecast(Gfs, "lucerne", T0, (0, 21.0), (24, 22.0), (48, 23.0), (72, 24.0), (96, 25.0), (120, 26.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(72, result.Hourly.CutoffHour);
    }

    [Fact]
    public void CutoffHour_is_negative_one_with_fewer_than_two_models()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (24, 21.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(-1, result.Hourly.CutoffHour);
        Assert.Empty(result.Hourly.Parameters);
    }

    [Fact]
    public void CutoffDay_is_second_to_last_model_day_count()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var forecasts = ModelForecasts.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [28.0, 30.0, 32.0])))
            .Update(MakeDailyForecast(Ecmwf, "lucerne", baseDate, (TempMax, [31.0, 33.0, 35.0, 37.0, 39.0])))
            .Update(MakeDailyForecast(Gfs, "lucerne", baseDate, (TempMax, [29.5, 31.5, 33.5, 35.5, 37.5, 39.5, 41.5])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(5, result.Daily.CutoffDay);
    }

    [Fact]
    public void Horizons_with_fewer_than_two_models_are_filtered_out()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (1, 21.0), (2, 22.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (0, 22.0), (1, 23.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        var hourlyParam = Assert.Single(result.Hourly.Parameters);
        var byHorizon = hourlyParam.ByHorizon;
        Assert.True(byHorizon.ContainsKey(Horizon.Hours(0)));
        Assert.True(byHorizon.ContainsKey(Horizon.Hours(1)));
        Assert.False(byHorizon.ContainsKey(Horizon.Hours(2)));
    }

    [Fact]
    public void Hourly_consensus_median_matches_expected_value()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (3, 20.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (3, 22.0)))
            .Update(MakeHourlyForecast(Gfs, "lucerne", T0, (3, 21.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        var hourlyParam = Assert.Single(result.Hourly.Parameters);
        var h3 = hourlyParam.ByHorizon[Horizon.Hours(3)];
        Assert.Equal(21.0, h3.Median);
        Assert.NotNull(h3.Spread);
        Assert.Equal(3, h3.AvailableModels.Count);
    }

    [Fact]
    public void Daily_consensus_from_model_daily_parameters()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var forecasts = ModelForecasts.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [28.0, 30.0])))
            .Update(MakeDailyForecast(Ecmwf, "lucerne", baseDate, (TempMax, [31.0, 33.0])))
            .Update(MakeDailyForecast(Gfs, "lucerne", baseDate, (TempMax, [29.5, 31.5])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        var dailyParam = Assert.Single(result.Daily.Parameters);
        var d0 = dailyParam.ByHorizon[Horizon.Days(0)];
        Assert.Equal(29.5, d0.Median);
        Assert.Equal(3, d0.AvailableModels.Count);
    }

    [Fact]
    public void Daily_consensus_filters_single_model_horizons()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var forecasts = ModelForecasts.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [28.0, 30.0, 32.0])))
            .Update(MakeDailyForecast(Ecmwf, "lucerne", baseDate, (TempMax, [31.0, 33.0])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        var dailyParam = Assert.Single(result.Daily.Parameters);
        var byHorizon = dailyParam.ByHorizon;
        Assert.True(byHorizon.ContainsKey(Horizon.Days(0)));
        Assert.True(byHorizon.ContainsKey(Horizon.Days(1)));
        Assert.False(byHorizon.ContainsKey(Horizon.Days(2)));
    }

    [Fact]
    public void ComputedAt_matches_time_provider_value()
    {
        var anchorTime = new DateTimeOffset(2026, 7, 31, 6, 0, 0, TimeSpan.Zero);
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", anchorTime, (0, 20.0), (1, 21.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", anchorTime, (0, 22.0), (1, 23.0)));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(anchorTime);

        var result = new ConsensusBuilder(parameters, timeProvider).Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(anchorTime, result.ComputedAt);
    }

    private static WeatherForecast MakePointForecast(
        WeatherModel model, string location, DateTimeOffset baseHour, int hour, Dictionary<ParameterDef, ForecastValue> values) =>
        new(model, location, new CycleId(baseHour),
            new ForecastSeries([new ForecastPoint(baseHour.AddHours(hour), values)]), DailyForecastSeries.Empty);

    [Fact]
    public void CutoffHour_ignores_a_model_whose_last_point_is_now()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (48, 21.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (0, 22.0)));

        var result = new ConsensusBuilder(new ResolvedParameterSet([Temperature], []), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(-1, result.Hourly.CutoffHour);
        Assert.Empty(result.Hourly.Parameters);
    }

    [Fact]
    public void CutoffHour_sorts_the_model_horizons_before_picking_the_second_largest()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (72, 21.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (0, 22.0), (120, 23.0)))
            .Update(MakeHourlyForecast(Gfs, "lucerne", T0, (0, 21.0), (48, 22.0)));

        var result = new ConsensusBuilder(new ResolvedParameterSet([Temperature], []), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(72, result.Hourly.CutoffHour);
    }

    [Fact]
    public void CutoffHour_only_considers_models_of_the_requested_location()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (0, 20.0), (24, 21.0)))
            .Update(MakeHourlyForecast(Ecmwf, "zurich", T0, (0, 22.0), (96, 23.0)))
            .Update(MakeHourlyForecast(Gfs, "lucerne", T0, (0, 21.0), (48, 22.0)));

        var result = new ConsensusBuilder(new ResolvedParameterSet([Temperature], []), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(24, result.Hourly.CutoffHour);
    }

    [Fact]
    public void CutoffDay_sorts_the_model_day_counts_before_picking_the_second_largest()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var forecasts = ModelForecasts.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [1.0, 2.0, 3.0])))
            .Update(MakeDailyForecast(Ecmwf, "lucerne", baseDate, (TempMax, [1.0, 2.0, 3.0, 4.0, 5.0])))
            .Update(MakeDailyForecast(Gfs, "lucerne", baseDate, (TempMax, [1.0, 2.0])));

        var result = new ConsensusBuilder(new ResolvedParameterSet([], [TempMax]), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(3, result.Daily.CutoffDay);
    }

    [Fact]
    public void CutoffDay_only_considers_models_of_the_requested_location()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var forecasts = ModelForecasts.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [1.0, 2.0])))
            .Update(MakeDailyForecast(Ecmwf, "zurich", baseDate, (TempMax, [1.0, 2.0, 3.0, 4.0, 5.0])))
            .Update(MakeDailyForecast(Gfs, "lucerne", baseDate, (TempMax, [1.0, 2.0, 3.0])));

        var result = new ConsensusBuilder(new ResolvedParameterSet([], [TempMax]), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        Assert.Equal(2, result.Daily.CutoffDay);
    }

    [Fact]
    public void Daily_horizons_stop_before_the_cutoff_day()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var forecasts = ModelForecasts.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [1.0, 2.0, 3.0, 4.0, 5.0])))
            .Update(MakeDailyForecast(Ecmwf, "lucerne", baseDate, (TempMax, [1.0, 2.0, 3.0])));

        var result = new ConsensusBuilder(new ResolvedParameterSet([], [TempMax]), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", new ConsensusSettings(ConsensusMethod.Median, 0.1, 1));

        var parameter = Assert.Single(result.Daily.Parameters);
        Assert.Equal(3, result.Daily.CutoffDay);
        Assert.Equal([Horizon.Days(0), Horizon.Days(1), Horizon.Days(2)], parameter.ByHorizon.Keys.OrderBy(h => h.Offset));
    }

    [Fact]
    public void Daily_consensus_skips_forecasts_of_other_locations()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var forecasts = ModelForecasts.Empty
            .Update(MakeDailyForecast(IconD2, "lucerne", baseDate, (TempMax, [10.0, 11.0])))
            .Update(MakeDailyForecast(Ecmwf, "zurich", baseDate, (TempMax, [50.0, 51.0])))
            .Update(MakeDailyForecast(Gfs, "lucerne", baseDate, (TempMax, [12.0, 13.0])));

        var result = new ConsensusBuilder(new ResolvedParameterSet([], [TempMax]), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        var day0 = Assert.Single(result.Daily.Parameters).ByHorizon[Horizon.Days(0)];
        Assert.Equal(11.0, day0.Median);
        Assert.Equal([IconD2, Gfs], day0.AvailableModels);
    }

    [Fact]
    public void Hourly_consensus_skips_forecasts_of_other_locations()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (3, 10.0)))
            .Update(MakeHourlyForecast(Ecmwf, "zurich", T0, (3, 50.0)))
            .Update(MakeHourlyForecast(Gfs, "lucerne", T0, (3, 12.0)));

        var result = new ConsensusBuilder(new ResolvedParameterSet([Temperature], []), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        var h3 = Assert.Single(result.Hourly.Parameters).ByHorizon[Horizon.Hours(3)];
        Assert.Equal(11.0, h3.Median);
        Assert.Equal([IconD2, Gfs], h3.AvailableModels);
    }

    [Fact]
    public void Horizon_consensus_carries_agreement_outlier_and_confidence_interval()
    {
        var forecasts = ModelForecasts.Empty
            .Update(MakeHourlyForecast(IconD2, "lucerne", T0, (3, 20.0)))
            .Update(MakeHourlyForecast(Ecmwf, "lucerne", T0, (3, 21.0)))
            .Update(MakeHourlyForecast(Gfs, "lucerne", T0, (3, 30.0)));

        var result = new ConsensusBuilder(new ResolvedParameterSet([Temperature], []), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        var h3 = Assert.Single(result.Hourly.Parameters).ByHorizon[Horizon.Hours(3)];
        Assert.Equal(21.0, h3.Median);
        Assert.Equal(2.0 / 3.0, h3.Agreement);
        Assert.Equal(10.0, h3.Spread);
        Assert.NotNull(h3.Outlier);
        Assert.Equal(Gfs, h3.Outlier.Model);
        Assert.Equal(9.0, h3.Outlier.Deviation);
        Assert.NotNull(h3.ConfidenceInterval);
        Assert.Equal(20.2, h3.ConfidenceInterval.Lower, 10);
        Assert.Equal(28.2, h3.ConfidenceInterval.Upper, 10);
    }

    [Fact]
    public void Parameter_without_a_quorum_at_any_horizon_is_left_out()
    {
        var humidity = ParameterRegistry.RelativeHumidity2m;
        var forecasts = ModelForecasts.Empty
            .Update(MakePointForecast(IconD2, "lucerne", T0, 3,
                new Dictionary<ParameterDef, ForecastValue> { [Temperature] = 20.0, [humidity] = 60.0 }))
            .Update(MakePointForecast(Ecmwf, "lucerne", T0, 3,
                new Dictionary<ParameterDef, ForecastValue> { [Temperature] = 22.0 }));

        var result = new ConsensusBuilder(new ResolvedParameterSet([Temperature, humidity], []), new FakeTimeProvider(T0))
            .Create(forecasts, "lucerne", ConsensusSettings.Default);

        var kept = Assert.Single(result.Hourly.Parameters);
        Assert.Equal(Temperature, kept.Parameter);
    }
}
