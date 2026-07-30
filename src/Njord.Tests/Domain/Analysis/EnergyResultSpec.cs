using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class EnergyResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather", "Solar"], [], []);

    private static ModelForecast MakeForecast(
        WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 48; h++)
        {
            var values = new Dictionary<ParameterDef, double?>();
            foreach (var (param, value) in hourlyValues)
                values[param] = value;
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
    public void Compute_produces_all_energy_values()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"),
                (Temperature, 10.0), (WindSpeed, 3.0), (CloudCover, 50.0)));

        var result = EnergyResult.Compute(snap, "lucerne", Parameters, Time, new EnergyOptions());

        Assert.Equal("lucerne", result.Location);
        Assert.InRange(result.HeatingDemand, 1, 100);
        Assert.NotNull(result.CopEstimate);
        Assert.NotEmpty(result.BatteryStrategy);
    }

    [Fact(Timeout = 5000)]
    public void Compute_with_multiple_models_produces_heating_demand_max()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 15.0), (WindSpeed, 2.0), (CloudCover, 30.0)),
            MakeForecast(new("m2"), (Temperature, 2.0), (WindSpeed, 10.0), (CloudCover, 90.0)));

        var result = EnergyResult.Compute(snap, "lucerne", Parameters, Time, new EnergyOptions());

        Assert.True(result.HeatingDemandMax >= result.HeatingDemand);
    }

    [Fact(Timeout = 5000)]
    public void Compute_with_multiple_models_produces_cop_estimate_min()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 15.0), (WindSpeed, 2.0), (CloudCover, 30.0)),
            MakeForecast(new("m2"), (Temperature, -5.0), (WindSpeed, 5.0), (CloudCover, 80.0)));

        var result = EnergyResult.Compute(snap, "lucerne", Parameters, Time, new EnergyOptions());

        Assert.NotNull(result.CopEstimateMin);
        Assert.True(result.CopEstimateMin <= result.CopEstimate);
    }

    [Fact(Timeout = 5000)]
    public void Compute_single_model_envelope_equals_primary()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 10.0), (WindSpeed, 3.0), (CloudCover, 50.0)));

        var result = EnergyResult.Compute(snap, "lucerne", Parameters, Time, new EnergyOptions());

        Assert.Equal(result.HeatingDemand, result.HeatingDemandMax);
        Assert.Equal(result.CopEstimate, result.CopEstimateMin);
    }

    [Fact(Timeout = 5000)]
    public void Compute_cop_optimal_conservative_is_intersection()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 10.0), (WindSpeed, 2.0), (CloudCover, 30.0)),
            MakeForecast(new("m2"), (Temperature, 8.0), (WindSpeed, 3.0), (CloudCover, 40.0)));

        var result = EnergyResult.Compute(snap, "lucerne", Parameters, Time, new EnergyOptions());

        Assert.NotNull(result.CopOptimalConservative);
        foreach (var hour in result.CopOptimalConservative!)
        {
            Assert.Contains(result.CopOptimal, o => o.HoursFromNow == hour);
        }
    }
}
