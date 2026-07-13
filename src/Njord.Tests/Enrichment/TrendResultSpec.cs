using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Domain;
using Njord.Enrichment;

namespace Njord.Tests.Enrichment;

public sealed class TrendResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef Precipitation = ParameterRegistry.GetByApiName("precipitation")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;
    private static readonly ParameterDef WeatherCode = ParameterRegistry.GetByApiName("weather_code")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather"], [], []);

    private static ModelForecast MakeForecast(
        WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 72; h++)
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
    public void Compute_with_no_previous_produces_null_trends()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 20.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 3.0)));

        var result = TrendResult.Compute(snap, null, "lucerne", [3, 6], Parameters, Time);

        Assert.Equal("lucerne", result.Location);
        Assert.All(result.ParameterTrends.Values, v => Assert.Null(v));
        Assert.Null(result.WeatherChange);
        Assert.Null(result.Stability);
    }

    [Fact(Timeout = 5000)]
    public void Compute_with_previous_detects_rising_temperature()
    {
        var prev = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 18.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)));
        var curr = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 22.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)));

        var result = TrendResult.Compute(curr, prev, "lucerne", [3], Parameters, Time);

        var tempTrend = result.ParameterTrends["temperature_2m"];
        Assert.NotNull(tempTrend);
        Assert.Equal("rising", tempTrend.Direction);
        Assert.Equal(4.0, tempTrend.Delta);
    }

    [Fact(Timeout = 5000)]
    public void Compute_detects_weather_change()
    {
        var prev = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 20.0), (WeatherCode, 1.0)));
        var curr = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 20.0), (WeatherCode, 63.0)));

        var result = TrendResult.Compute(curr, prev, "lucerne", [3], Parameters, Time);

        Assert.NotNull(result.WeatherChange);
        Assert.Equal("clear", result.WeatherChange.FromCategory);
        Assert.Equal("rain", result.WeatherChange.ToCategory);
    }

    [Fact(Timeout = 5000)]
    public void ToMqttMessages_produces_single_trend_topic()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 20.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)));

        var result = TrendResult.Compute(snap, null, "lucerne", [3], Parameters, Time);
        var messages = result.ToMqttMessages("njord");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/trends", messages[0].Topic);
        Assert.True(messages[0].Retain);

        var json = JsonNode.Parse(messages[0].Payload)!;
        Assert.True(json.AsObject().ContainsKey("trend_temperature_dir"));
        Assert.True(json.AsObject().ContainsKey("precip_starts"));
        Assert.True(json.AsObject().ContainsKey("decay_rate"));
    }
}
