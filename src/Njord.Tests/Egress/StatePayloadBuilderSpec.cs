using System.Text.Json.Nodes;
using Njord.Domain;
using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class StatePayloadBuilderSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef Sunrise = ParameterRegistry.GetByApiName("sunrise")!;

    private static readonly ResolvedParameterSet SmallParams = new(
        [Temperature, WindSpeed],
        [TempMax, Sunrise]);

    private static ModelForecast Forecast(DateTimeOffset tick, DateTimeOffset firstPoint, int pointCount)
        => new(
            IconD2,
            "home",
            new CycleId(tick),
            new ForecastSeries(Enumerable.Range(0, pointCount)
                .Select(i => new ForecastPoint(firstPoint.AddHours(i), new Dictionary<ParameterDef, double?>
                {
                    [Temperature] = i,
                    [WindSpeed] = i * 0.1,
                }))),
            new DailyForecastSeries([
                new DailyForecastPoint(DateOnly.FromDateTime(tick.UtcDateTime), new Dictionary<ParameterDef, object?>
                {
                    [TempMax] = 28.5,
                    [Sunrise] = "05:31",
                }),
            ]));

    [Fact(Timeout = 5000)]
    public void Returns_one_entry_per_configured_horizon()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 100), SmallParams, [3, 6, 24], 4, tick);

        Assert.Equal(7, result.Count);
        Assert.True(result.ContainsKey("h3"));
        Assert.True(result.ContainsKey("h6"));
        Assert.True(result.ContainsKey("h24"));
        Assert.True(result.ContainsKey("d0"));
        Assert.True(result.ContainsKey("d1"));
        Assert.True(result.ContainsKey("d2"));
        Assert.True(result.ContainsKey("d3"));
    }

    [Fact(Timeout = 5000)]
    public void Hourly_payload_is_flat_json_with_parameter_values()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 100), SmallParams, [3], 1, tick);
        var h3 = JsonNode.Parse(result["h3"])!;

        Assert.Equal(3.0, (double?)h3["temperature"]);
        Assert.InRange((double)h3["wind_speed_10m"]!, 0.29, 0.31);
        Assert.False(h3.AsObject().ContainsKey("h3"));
    }

    [Fact(Timeout = 5000)]
    public void Daily_payload_is_flat_json_with_parameter_values()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 10), SmallParams, [3], 1, tick);
        var d0 = JsonNode.Parse(result["d0"])!;

        Assert.Equal(28.5, (double?)d0["temperature_max"]);
        Assert.Equal("05:31", (string?)d0["sunrise"]);
    }

    [Fact(Timeout = 5000)]
    public void Horizons_beyond_series_yield_null_values()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 10), SmallParams, [72], 1, tick);
        var h72 = JsonNode.Parse(result["h72"])!;

        Assert.True(h72.AsObject().ContainsKey("temperature"));
        Assert.Null(h72["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void Anchor_bumps_to_next_full_hour_when_tick_is_mid_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 19, 31, 0, TimeSpan.Zero);
        var firstPoint = new DateTimeOffset(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, firstPoint, 100), SmallParams, [3], 1, tick);
        var h3 = JsonNode.Parse(result["h3"])!;

        Assert.Equal(3.0, (double?)h3["temperature"]);
    }
}
