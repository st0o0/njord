using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Consensus;
using Njord.Consensus.Settings;
using Njord.Core.Configuration;
using Njord.Derived;
using Njord.Forecast;
using Njord.Indices.Configuration;
using Njord.Messages.Alerts;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Model;
using Njord.Tests.Shared;
using Njord.Trends;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Mqtt.Tests;

public sealed class StatePayloadBuilderSpec
{
    private static IndexSettings DefaultPrefs(string location = "lucerne") =>
        AppSettingsDefaults.Options<IndexOptions>().ToSettings([location]);
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef Sunrise = ParameterRegistry.GetByApiName("sunrise")!;

    private static readonly ResolvedParameterSet SmallParams = new(
        [Temperature, WindSpeed],
        [TempMax, Sunrise]);

    private static WeatherForecast Forecast(DateTimeOffset tick, DateTimeOffset firstPoint, int pointCount)
        => new(
            IconD2,
            "home",
            new CycleId(tick),
            new ForecastSeries(Enumerable.Range(0, pointCount)
                .Select(i => new ForecastPoint(firstPoint.AddHours(i), new Dictionary<ParameterDef, ForecastValue>
                {
                    [Temperature] = i,
                    [WindSpeed] = i * 0.1,
                }))),
            new DailyForecastSeries([
                new DailyForecastPoint(DateOnly.FromDateTime(tick.UtcDateTime),
                    new Dictionary<ParameterDef, ForecastValue> { [TempMax] = 28.5, [Sunrise] = new ForecastValue(null, "05:31") }),
            ]));

    [Fact]
    public void Returns_one_entry_per_configured_horizon()
    {
        var tick = TestTimeConstants.NoonAnchor;
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 100), SmallParams, [3, 6, 24], 4, tick);

        Assert.True(result.ContainsKey(Horizon.Hours(3)));
        Assert.True(result.ContainsKey(Horizon.Hours(6)));
        Assert.True(result.ContainsKey(Horizon.Hours(24)));
        Assert.True(result.ContainsKey(Horizon.Days(0)));
    }

    [Fact]
    public void Hourly_payload_is_flat_json_with_parameter_values()
    {
        var tick = TestTimeConstants.NoonAnchor;
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 100), SmallParams, [3], 1, tick);
        var h3 = JsonNode.Parse(result[Horizon.Hours(3)])!;

        Assert.Equal(3.0, (double?)h3["temperature"]);
        Assert.InRange((double)h3["wind_speed_10m"]!, 0.29, 0.31);
        Assert.False(h3.AsObject().ContainsKey("h3"));
    }

    [Fact]
    public void Daily_payload_is_flat_json_with_parameter_values()
    {
        var tick = TestTimeConstants.NoonAnchor;
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 10), SmallParams, [3], 1, tick);
        var d0 = JsonNode.Parse(result[Horizon.Days(0)])!;

        Assert.Equal(28.5, (double?)d0["temperature_max"]);
        Assert.Equal("05:31", (string?)d0["sunrise"]);
    }

    [Fact]
    public void Horizons_beyond_series_are_omitted()
    {
        var tick = TestTimeConstants.NoonAnchor;
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 10), SmallParams, [72], 1, tick);

        Assert.False(result.ContainsKey(Horizon.Hours(72)));
    }

    [Fact]
    public void Anchor_floors_to_current_hour_when_tick_is_mid_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 19, 31, 0, TimeSpan.Zero);
        var firstPoint = new DateTimeOffset(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, firstPoint, 100), SmallParams, [3], 1, tick);
        var h3 = JsonNode.Parse(result[Horizon.Hours(3)])!;

        Assert.Equal(2.0, (double?)h3["temperature"]);
    }

    [Fact]
    public void FromAlerts_produces_one_message_per_alert()
    {
        var alerts = new List<Alert>
        {
            new(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, Horizon.Hours(4), 3)),
            Alert.None(AlertType.Heat),
        };
        var result = new AlertResult(new LocationId("lucerne"), alerts);

        var messages = StatePayloadBuilder.FromAlerts(result, "njord");

        Assert.Equal(2, messages.Count);
        Assert.Equal("njord/lucerne/alerts/frost", messages[0].Topic);
        Assert.Equal("njord/lucerne/alerts/heat", messages[1].Topic);
        Assert.True(messages[0].Retain);
        var payload = JsonNode.Parse(messages[0].Payload)!;
        Assert.Equal("yellow", (string?)payload["severity"]);
        Assert.Equal(0.75, (double?)payload["confidence"]);
    }

    [Fact]
    public void FromAlerts_writes_frost_details_as_snake_case_attributes()
    {
        var alert = new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, Horizon.Hours(4), 3));

        var message = Assert.Single(StatePayloadBuilder.FromAlerts(
            new AlertResult(new LocationId("lucerne"), [alert]), "njord"));

        Assert.Equal(
            "{\"severity\":\"yellow\",\"confidence\":0.75,\"trigger_value\":0,\"threshold\":0,"
            + "\"peak_value\":null,\"hours_until\":null,\"duration_hours\":null,"
            + "\"expected_low\":-2.1,\"earliest_frost\":\"\\u002B4h\",\"models_agreeing\":3}",
            message.Payload);
    }
}
