using Microsoft.Extensions.Time.Testing;
using Njord.Domain.Weather;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class ConsensusResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");
    private static readonly WeatherModel Gfs = new("gfs_seamless");

    private static ModelForecast MakeForecast(WeatherModel model, double tempAt3H)
    {
        var point = new ForecastPoint(T0.AddHours(3),
            new Dictionary<ParameterDef, double?> { [Temperature] = tempAt3H });
        return new ModelForecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries([point]), DailyForecastSeries.Empty);
    }

    [Fact(Timeout = 5000)]
    public void Compute_produces_result_with_metrics_per_parameter_and_horizon()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeForecast(IconD2, 20.0))
            .Update(MakeForecast(Ecmwf, 22.0))
            .Update(MakeForecast(Gfs, 21.0));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusResult.Compute(snapshot, parameters, [3], "lucerne", timeProvider);

        Assert.Single(result.Parameters);
        Assert.Equal(Temperature, result.Parameters[0].Parameter);

        var h3 = result.Parameters[0].ByHorizon["h3"];
        Assert.Equal(21.0, h3.Median);
        Assert.NotNull(h3.Spread);
        Assert.Equal(3, h3.AvailableModels.Count);
    }

    [Fact(Timeout = 5000)]
    public void Compute_with_no_data_yields_null_metrics()
    {
        var snapshot = ModelSnapshot.Empty;
        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusResult.Compute(snapshot, parameters, [3], "lucerne", timeProvider);

        var h3 = result.Parameters[0].ByHorizon["h3"];
        Assert.Null(h3.Median);
        Assert.Null(h3.Spread);
        Assert.Empty(h3.AvailableModels);
    }

    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef PrecipSum = ParameterRegistry.GetByApiName("precipitation_sum")!;

    private static ModelForecast MakeDailyForecast(
        WeatherModel model, DateOnly baseDate, params (ParameterDef Param, double?[] Values)[] series)
    {
        var points = new List<DailyForecastPoint>();
        var dayCount = series.Length > 0 ? series[0].Values.Length : 0;

        for (var d = 0; d < dayCount; d++)
        {
            var numericValues = new Dictionary<ParameterDef, double?>();
            foreach (var (param, values) in series)
            {
                numericValues[param] = values[d];
            }
            points.Add(new DailyForecastPoint(baseDate.AddDays(d), numericValues, new Dictionary<ParameterDef, string?>()));
        }

        return new ModelForecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries([]), new DailyForecastSeries(points));
    }

    [Fact(Timeout = 5000)]
    public void Compute_daily_consensus_with_three_models()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var snapshot = ModelSnapshot.Empty
            .Update(MakeDailyForecast(IconD2, baseDate, (TempMax, [28.0, 30.0])))
            .Update(MakeDailyForecast(Ecmwf, baseDate, (TempMax, [31.0, 33.0])))
            .Update(MakeDailyForecast(Gfs, baseDate, (TempMax, [29.5, 31.5])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusResult.Compute(snapshot, parameters, [], "lucerne", timeProvider);

        Assert.Empty(result.Parameters);
        Assert.Single(result.DailyParameters);

        var d0 = result.DailyParameters[0].ByHorizon["d0"];
        Assert.Equal(29.5, d0.Median);
        Assert.Equal(3, d0.AvailableModels.Count);

        var d1 = result.DailyParameters[0].ByHorizon["d1"];
        Assert.Equal(31.5, d1.Median);
    }

    [Fact(Timeout = 5000)]
    public void Compute_daily_consensus_filters_out_single_model_horizons()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var snapshot = ModelSnapshot.Empty
            .Update(MakeDailyForecast(IconD2, baseDate, (TempMax, [28.0, 30.0, 32.0])))
            .Update(MakeDailyForecast(Ecmwf, baseDate, (TempMax, [31.0, 33.0])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusResult.Compute(snapshot, parameters, [], "lucerne", timeProvider);

        Assert.Single(result.DailyParameters);
        Assert.True(result.DailyParameters[0].ByHorizon.ContainsKey("d0"));
        Assert.True(result.DailyParameters[0].ByHorizon.ContainsKey("d1"));
        Assert.False(result.DailyParameters[0].ByHorizon.ContainsKey("d2"));
    }

    [Fact(Timeout = 5000)]
    public void Compute_daily_consensus_with_empty_daily_parameters()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeForecast(IconD2, 20.0))
            .Update(MakeForecast(Ecmwf, 22.0));

        var parameters = new ResolvedParameterSet([Temperature], []);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusResult.Compute(snapshot, parameters, [3], "lucerne", timeProvider);

        Assert.Single(result.Parameters);
        Assert.Empty(result.DailyParameters);
    }

    [Fact(Timeout = 5000)]
    public void Compute_daily_consensus_missing_point_excludes_model_from_available()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var snapshot = ModelSnapshot.Empty
            .Update(MakeDailyForecast(IconD2, baseDate, (TempMax, [28.0, 30.0])))
            .Update(MakeDailyForecast(Ecmwf, baseDate, (TempMax, [31.0, 33.0])))
            .Update(MakeDailyForecast(Gfs, baseDate, (TempMax, [29.0])));

        var parameters = new ResolvedParameterSet([], [TempMax]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusResult.Compute(snapshot, parameters, [], "lucerne", timeProvider);

        var d0 = result.DailyParameters[0].ByHorizon["d0"];
        Assert.Equal(3, d0.AvailableModels.Count);

        var d1 = result.DailyParameters[0].ByHorizon["d1"];
        Assert.Equal(2, d1.AvailableModels.Count);
        Assert.DoesNotContain(Gfs, d1.AvailableModels);
    }

    [Fact(Timeout = 5000)]
    public void Compute_daily_consensus_multiple_parameters()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var snapshot = ModelSnapshot.Empty
            .Update(MakeDailyForecast(IconD2, baseDate,
                (TempMax, [28.0]), (PrecipSum, [5.0])))
            .Update(MakeDailyForecast(Ecmwf, baseDate,
                (TempMax, [31.0]), (PrecipSum, [12.0])))
            .Update(MakeDailyForecast(Gfs, baseDate,
                (TempMax, [29.5]), (PrecipSum, [8.0])));

        var parameters = new ResolvedParameterSet([], [TempMax, PrecipSum]);
        var timeProvider = new FakeTimeProvider(T0);

        var result = ConsensusResult.Compute(snapshot, parameters, [], "lucerne", timeProvider);

        Assert.Equal(2, result.DailyParameters.Count);
        Assert.Equal(TempMax, result.DailyParameters[0].Parameter);
        Assert.Equal(PrecipSum, result.DailyParameters[1].Parameter);

        Assert.Equal(29.5, result.DailyParameters[0].ByHorizon["d0"].Median);
        Assert.Equal(8.0, result.DailyParameters[1].ByHorizon["d0"].Median);
    }
}
