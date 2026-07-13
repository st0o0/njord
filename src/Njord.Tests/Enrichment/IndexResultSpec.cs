using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain;
using Njord.Enrichment;

namespace Njord.Tests.Enrichment;

public sealed class IndexResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
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
    public void Compute_produces_all_index_values()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"),
                (Temperature, 22.0), (Humidity, 50.0), (WindSpeed, 3.0), (CloudCover, 20.0)));

        var result = IndexResult.Compute(snap, "lucerne", Parameters, Time, new IndexOptions());

        Assert.Equal("lucerne", result.Location);
        Assert.InRange(result.Outdoor, 1, 100);
        Assert.InRange(result.Laundry, 1, 100);
        Assert.InRange(result.Solar, 1, 100);
    }

    [Fact(Timeout = 5000)]
    public void ToMqttMessages_produces_single_index_topic()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"),
                (Temperature, 20.0), (Humidity, 55.0), (WindSpeed, 2.0), (CloudCover, 30.0)));

        var result = IndexResult.Compute(snap, "lucerne", Parameters, Time, new IndexOptions());
        var messages = result.ToMqttMessages("njord");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/indices", messages[0].Topic);
        Assert.True(messages[0].Retain);

        var json = JsonNode.Parse(messages[0].Payload)!;
        Assert.NotNull(json["laundry"]);
        Assert.NotNull(json["outdoor"]);
        Assert.NotNull(json["hdd"]);
        Assert.NotNull(json["vpd_category"]);
    }
}
