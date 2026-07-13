using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain;
using Njord.Enrichment;

namespace Njord.Tests.Enrichment;

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
    public void ToMqttMessages_produces_single_energy_topic()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"),
                (Temperature, 15.0), (WindSpeed, 2.0), (CloudCover, 30.0)));

        var result = EnergyResult.Compute(snap, "lucerne", Parameters, Time, new EnergyOptions());
        var messages = result.ToMqttMessages("njord");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/energy", messages[0].Topic);
        Assert.True(messages[0].Retain);

        var json = JsonNode.Parse(messages[0].Payload)!;
        Assert.NotNull(json["heating_demand"]);
        Assert.NotNull(json["cop_optimal"]);
        Assert.NotNull(json["battery_strategy"]);
    }
}
