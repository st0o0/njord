using System.Text.Json.Nodes;
using Njord.Domain;
using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class StatePayloadBuilderSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");

    private static ModelForecast Forecast(DateTimeOffset tick, DateTimeOffset firstPoint, int pointCount)
        => new(
            IconD2,
            "home",
            new CycleId(tick),
            tick,
            new ForecastSeries(Enumerable.Range(0, pointCount)
                .Select(i => new ForecastPoint(firstPoint.AddHours(i), Temperature: i, WindSpeed: i * 0.1))));

    [Fact(Timeout = 5000)]
    public void Horizons_anchor_to_the_next_full_grid_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 19, 31, 0, TimeSpan.Zero);
        var firstPoint = new DateTimeOffset(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, firstPoint, 100), [3, 24]))!;

        // tick + 3 h = 22:31 → next full hour 23:00 = index 3 from 20:00
        Assert.Equal("2026-07-12T23:00:00.0000000+00:00", (string?)json["h3"]!["valid_at"]);
        Assert.Equal(3.0, (double?)json["h3"]!["temperature"]);
        // tick + 24 h = 19:31 next day → 20:00 next day = index 24
        Assert.Equal(24.0, (double?)json["h24"]!["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void A_tick_on_the_full_hour_is_not_bumped()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, tick, 100), [3]))!;

        Assert.Equal("2026-07-12T15:00:00.0000000+00:00", (string?)json["h3"]!["valid_at"]);
        Assert.Equal(3.0, (double?)json["h3"]!["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void Horizons_beyond_the_series_yield_nulls_but_keep_the_anchor()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, tick, 49), [24, 72]))!;

        Assert.Equal(24.0, (double?)json["h24"]!["temperature"]);
        Assert.Equal("2026-07-15T12:00:00.0000000+00:00", (string?)json["h72"]!["valid_at"]);
        Assert.True(json["h72"]!.AsObject().ContainsKey("temperature"));
        Assert.Null(json["h72"]!["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void Every_parameter_key_is_present_per_horizon()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

        var json = JsonNode.Parse(StatePayloadBuilder.Build(Forecast(tick, tick, 10), [3]))!;
        var h3 = json["h3"]!.AsObject();

        foreach (var parameter in Enum.GetValues<WeatherParameter>())
        {
            Assert.True(h3.ContainsKey(parameter.JsonKey()), $"missing {parameter.JsonKey()}");
        }
    }
}
