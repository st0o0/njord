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
            tick,
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
    public void Horizons_anchor_to_the_next_full_grid_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 19, 31, 0, TimeSpan.Zero);
        var firstPoint = new DateTimeOffset(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, firstPoint, 100), SmallParams, [3, 24], 4))!;

        Assert.Equal("2026-07-12T23:00:00.0000000+00:00", (string?)json["h3"]!["valid_at"]);
        Assert.Equal(3.0, (double?)json["h3"]!["temperature"]);
        Assert.Equal(24.0, (double?)json["h24"]!["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void A_tick_on_the_full_hour_is_not_bumped()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, tick, 100), SmallParams, [3], 4))!;

        Assert.Equal("2026-07-12T15:00:00.0000000+00:00", (string?)json["h3"]!["valid_at"]);
        Assert.Equal(3.0, (double?)json["h3"]!["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void Horizons_beyond_the_series_yield_nulls_but_keep_the_anchor()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, tick, 49), SmallParams, [24, 72], 4))!;

        Assert.Equal(24.0, (double?)json["h24"]!["temperature"]);
        Assert.Equal("2026-07-15T12:00:00.0000000+00:00", (string?)json["h72"]!["valid_at"]);
        Assert.True(json["h72"]!.AsObject().ContainsKey("temperature"));
        Assert.Null(json["h72"]!["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void Every_active_hourly_parameter_key_is_present_per_horizon()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, tick, 10), SmallParams, [3], 4))!;
        var h3 = json["h3"]!.AsObject();

        Assert.True(h3.ContainsKey("temperature"), "missing temperature");
        Assert.True(h3.ContainsKey("wind_speed_10m"), "missing wind_speed_10m");
    }

    [Fact(Timeout = 5000)]
    public void Daily_values_appear_with_day_offset_keys()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, tick, 10), SmallParams, [3], 4))!;

        Assert.NotNull(json["d0"]);
        Assert.Equal(28.5, (double?)json["d0"]!["temperature_max"]);
        Assert.Equal("05:31", (string?)json["d0"]!["sunrise"]);
    }
}
