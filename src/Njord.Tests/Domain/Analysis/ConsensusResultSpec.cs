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
}
