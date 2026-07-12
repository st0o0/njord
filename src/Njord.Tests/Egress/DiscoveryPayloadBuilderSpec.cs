using System.Text.Json.Nodes;
using Njord.Configuration;
using Njord.Domain;
using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class DiscoveryPayloadBuilderSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly MqttOptions Mqtt = new() { Host = "broker.local" };
    private static readonly int[] DefaultHorizons = [3, 6, 12, 24, 48, 72];

    private static string Build(int[]? horizons = null) => DiscoveryPayloadBuilder.Build(
        "home", IconD2, horizons ?? DefaultHorizons, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");

    [Fact(Timeout = 5000)]
    public Task The_device_payload_matches_the_approved_snapshot()
    {
        return VerifyJson(Build()).UseDirectory("Snapshots");
    }

    [Fact(Timeout = 5000)]
    public void The_full_grid_yields_54_components()
    {
        var json = JsonNode.Parse(Build())!;

        Assert.Equal(9 * 6, json["cmps"]!.AsObject().Count);
    }

    [Fact(Timeout = 5000)]
    public void Components_carry_grid_identity_and_expiry()
    {
        var json = JsonNode.Parse(Build())!;
        var component = json["cmps"]!["temperature_h24"]!;

        Assert.Equal("sensor", (string?)component["p"]);
        Assert.Equal("njord_home_icon_d2_temperature_h24", (string?)component["unique_id"]);
        Assert.Equal("temperature", (string?)component["device_class"]);
        Assert.Equal("°C", (string?)component["unit_of_measurement"]);
        // 2 × the 60-minute poll interval: a silently missing model expires to unavailable.
        Assert.Equal(7200, (int?)component["expire_after"]);
        Assert.Equal("{{ value_json.h24.temperature }}", (string?)component["value_template"]);
    }

    [Fact(Timeout = 5000)]
    public void Components_are_unavailable_when_the_service_dies_or_the_value_is_null()
    {
        var json = JsonNode.Parse(Build([3]))!;
        var component = json["cmps"]!["cloud_cover_h3"]!;
        var availability = component["availability"]!.AsArray();

        Assert.Equal("all", (string?)component["availability_mode"]);
        Assert.Equal("njord/status", (string?)availability[0]!["topic"]);
        Assert.Equal("njord/home/icon_d2/state", (string?)availability[1]!["topic"]);
        Assert.Contains("value_json.h3.cloud_cover is not none", (string?)availability[1]!["value_template"]);
    }

    [Fact(Timeout = 5000)]
    public void Forecasts_are_not_measurements()
    {
        var json = JsonNode.Parse(Build())!;

        foreach (var (_, component) in json["cmps"]!.AsObject())
        {
            Assert.False(component!.AsObject().ContainsKey("state_class"));
        }
    }
}
