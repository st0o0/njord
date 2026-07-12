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

    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;

    private static readonly ResolvedParameterSet SmallParams = new(
        [Temperature, CloudCover],
        [TempMax]);

    private static string Build(int[]? horizons = null, int forecastDays = 4, ResolvedParameterSet? parameters = null)
        => DiscoveryPayloadBuilder.Build(
            "home", IconD2, parameters ?? SmallParams,
            horizons ?? DefaultHorizons, forecastDays, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");

    [Fact(Timeout = 5000)]
    public void The_grid_yields_expected_component_count()
    {
        var json = JsonNode.Parse(Build())!;

        // 2 hourly × 6 horizons + 1 daily × 4 days = 16
        Assert.Equal(16, json["cmps"]!.AsObject().Count);
    }

    [Fact(Timeout = 5000)]
    public void Hourly_components_carry_grid_identity_and_expiry()
    {
        var json = JsonNode.Parse(Build())!;
        var component = json["cmps"]!["temperature_h24"]!;

        Assert.Equal("sensor", (string?)component["p"]);
        Assert.Equal("njord_home_icon_d2_temperature_h24", (string?)component["unique_id"]);
        Assert.Equal("temperature", (string?)component["device_class"]);
        Assert.Equal("°C", (string?)component["unit_of_measurement"]);
        Assert.Equal(7200, (int?)component["expire_after"]);
        Assert.Equal("{{ value_json.temperature }}", (string?)component["value_template"]);
    }

    [Fact(Timeout = 5000)]
    public void Daily_components_use_day_offset_naming()
    {
        var json = JsonNode.Parse(Build())!;
        var component = json["cmps"]!["temperature_max_d0"]!;

        Assert.Equal("sensor", (string?)component["p"]);
        Assert.Equal("njord_home_icon_d2_temperature_max_d0", (string?)component["unique_id"]);
        Assert.Equal("temperature", (string?)component["device_class"]);
        Assert.Equal("{{ value_json.temperature_max }}", (string?)component["value_template"]);
    }

    [Fact(Timeout = 5000)]
    public void Components_without_device_class_omit_the_field()
    {
        var json = JsonNode.Parse(Build([3]))!;
        var component = json["cmps"]!["cloud_cover_h3"]!;

        Assert.False(component.AsObject().ContainsKey("device_class"));
        Assert.Equal("%", (string?)component["unit_of_measurement"]);
    }

    [Fact(Timeout = 5000)]
    public void Components_are_unavailable_when_the_service_dies_or_the_value_is_null()
    {
        var json = JsonNode.Parse(Build([3]))!;
        var component = json["cmps"]!["cloud_cover_h3"]!;
        var availability = component["availability"]!.AsArray();

        Assert.Equal("all", (string?)component["availability_mode"]);
        Assert.Equal("njord/status", (string?)availability[0]!["topic"]);
        Assert.Equal("njord/home/icon_d2/h3", (string?)availability[1]!["topic"]);
        Assert.Contains("value_json.cloud_cover is not none", (string?)availability[1]!["value_template"]);
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
