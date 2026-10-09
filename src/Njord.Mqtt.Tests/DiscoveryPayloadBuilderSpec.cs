using System.Text.Json.Nodes;
using Njord.Core.Configuration;
using Njord.Weather;

namespace Njord.Mqtt.Tests;

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

    private static string Build(int[]? horizons = null, int[]? dayOffsets = null, ResolvedParameterSet? parameters = null, IReadOnlySet<ParameterDef>? supported = null)
    {
        var parms = parameters ?? SmallParams;
        var allParams = supported ?? parms.Hourly.Concat(parms.Daily).ToHashSet();
        return DiscoveryPayloadBuilder.Build(
            "home", IconD2, parms,
            horizons ?? DefaultHorizons,
            dayOffsets ?? [0, 1, 2, 3],
            allParams, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
    }

    [Fact]
    public void The_grid_yields_expected_component_count()
    {
        var json = JsonNode.Parse(Build())!;

        // 2 hourly × 6 horizons + 1 daily × 4 days = 16
        Assert.Equal(16, json["cmps"]!.AsObject().Count);
    }

    [Fact]
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

    [Fact]
    public void Daily_components_use_day_offset_naming()
    {
        var json = JsonNode.Parse(Build())!;
        var component = json["cmps"]!["temperature_max_d0"]!;

        Assert.Equal("sensor", (string?)component["p"]);
        Assert.Equal("njord_home_icon_d2_temperature_max_d0", (string?)component["unique_id"]);
        Assert.Equal("temperature", (string?)component["device_class"]);
        Assert.Equal("{{ value_json.temperature_max }}", (string?)component["value_template"]);
    }

    [Fact]
    public void Components_without_device_class_omit_the_field()
    {
        var json = JsonNode.Parse(Build([3]))!;
        var component = json["cmps"]!["cloud_cover_h3"]!;

        Assert.False(component.AsObject().ContainsKey("device_class"));
        Assert.Equal("%", (string?)component["unit_of_measurement"]);
    }

    [Fact]
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

    [Fact]
    public void Unsupported_parameter_is_excluded_from_components()
    {
        var json = JsonNode.Parse(Build(supported: new HashSet<ParameterDef> { Temperature, TempMax }))!;
        var cmps = json["cmps"]!.AsObject();

        Assert.True(cmps.ContainsKey("temperature_h3"));
        Assert.False(cmps.ContainsKey("cloud_cover_h3"));
    }

    [Fact]
    public void Out_of_range_horizon_is_excluded()
    {
        var json = JsonNode.Parse(Build(horizons: [3, 6]))!;
        var cmps = json["cmps"]!.AsObject();

        Assert.True(cmps.ContainsKey("temperature_h3"));
        Assert.True(cmps.ContainsKey("temperature_h6"));
        Assert.False(cmps.ContainsKey("temperature_h72"));
    }

    [Fact]
    public void Empty_supported_set_produces_device_with_no_components()
    {
        var json = JsonNode.Parse(Build(supported: new HashSet<ParameterDef>()))!;
        var cmps = json["cmps"]!.AsObject();

        Assert.Empty(cmps);
    }

    [Fact]
    public void Forecasts_are_not_measurements()
    {
        var json = JsonNode.Parse(Build())!;

        foreach (var (_, component) in json["cmps"]!.AsObject())
        {
            Assert.False(component!.AsObject().ContainsKey("state_class"));
        }
    }

    [Fact]
    public void Consensus_device_id_and_model_name()
    {
        var payload = DiscoveryPayloadBuilder.BuildConsensus(
            "lucerne", SmallParams, 5, 4, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("njord_lucerne_consensus", (string?)json["dev"]!["ids"]![0]);
        Assert.Equal("njord lucerne consensus", (string?)json["dev"]!["name"]);
        Assert.Equal("consensus", (string?)json["dev"]!["mdl"]);
    }

    [Fact]
    public void Consensus_components_use_consensus_topic()
    {
        var payload = DiscoveryPayloadBuilder.BuildConsensus(
            "lucerne", new ResolvedParameterSet([Temperature], []),
            3, 4, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;
        var component = json["cmps"]!["temperature_h3"]!;

        Assert.Equal("njord_lucerne_consensus_temperature_h3", (string?)component["unique_id"]);

        var availability = component["availability"]!.AsArray();
        Assert.Equal("njord/lucerne/consensus/h3", (string?)availability[1]!["topic"]);
    }

    [Fact]
    public void Consensus_includes_hourly_and_daily_parameters()
    {
        var payload = DiscoveryPayloadBuilder.BuildConsensus(
            "lucerne", SmallParams, 5, 4, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        // 2 hourly × 6 slots (h0-h5) + 1 daily × 4 days = 16
        Assert.Equal(16, json["cmps"]!.AsObject().Count);
    }

    [Fact]
    public void ToComponent_sensor_with_unit_and_device_class_emits_fields_in_wire_order()
    {
        var spec = new SensorComponentSpec(
            "wind_chill", "dev_wind_chill", "wind chill", "njord/state", "{{ value_json.wind_chill }}",
            Unit: "°C", DeviceClass: "temperature");

        var component = DiscoveryPayloadBuilder.ToComponent(spec, "njord/status", 7200);

        Assert.Equal(
            ["p", "unique_id", "name", "state_topic", "unit_of_measurement", "device_class", "expire_after", "value_template", "availability", "availability_mode"],
            component.Select(kv => kv.Key).ToArray());
        Assert.Equal("sensor", (string?)component["p"]);
        Assert.Equal("°C", (string?)component["unit_of_measurement"]);
        Assert.Equal("temperature", (string?)component["device_class"]);
        Assert.Equal(7200, (int?)component["expire_after"]);
        Assert.Equal("all", (string?)component["availability_mode"]);
    }

    [Fact]
    public void ToComponent_measurement_last_places_unit_and_device_class_after_availability_mode()
    {
        var spec = new SensorComponentSpec(
            "wind_chill", "dev_wind_chill", "wind chill", "njord/state", "{{ value_json.wind_chill }}",
            Unit: "°C", DeviceClass: "temperature", MeasurementLast: true);

        var component = DiscoveryPayloadBuilder.ToComponent(spec, "njord/status", 7200);

        Assert.Equal(
            ["p", "unique_id", "name", "state_topic", "expire_after", "value_template", "availability", "availability_mode", "unit_of_measurement", "device_class"],
            component.Select(kv => kv.Key).ToArray());
    }

    [Fact]
    public void ToComponent_binary_sensor_omits_unit_and_device_class()
    {
        var spec = new SensorComponentSpec(
            "inversion", "dev_inversion", "inversion", "njord/state",
            "{% if value_json.inversion == true %}ON{% else %}OFF{% endif %}",
            Platform: "binary_sensor");

        var component = DiscoveryPayloadBuilder.ToComponent(spec, "njord/status", 3600);

        Assert.Equal("binary_sensor", (string?)component["p"]);
        Assert.False(component.ContainsKey("unit_of_measurement"));
        Assert.False(component.ContainsKey("device_class"));
        Assert.False(component.ContainsKey("json_attributes_topic"));
    }

    [Fact]
    public void ToComponent_single_availability_has_only_the_service_topic()
    {
        var spec = new SensorComponentSpec("k", "id", "n", "njord/state", "{{ value_json.k }}");

        var availability = DiscoveryPayloadBuilder.ToComponent(spec, "njord/status", 60)["availability"]!.AsArray();

        var entry = Assert.Single(availability);
        Assert.NotNull(entry);
        Assert.Equal("njord/status", (string?)entry["topic"]);
        Assert.False(entry.AsObject().ContainsKey("value_template"));
    }

    [Fact]
    public void ToComponent_multi_entry_availability_appends_extra_entries_with_templates()
    {
        var spec = new SensorComponentSpec(
            "k", "id", "n", "njord/state", "{{ value_json.k }}",
            ExtraAvailability: [("njord/state", "{% if value_json.k is not none %}online{% else %}offline{% endif %}")]);

        var availability = DiscoveryPayloadBuilder.ToComponent(spec, "njord/status", 60)["availability"]!.AsArray();

        Assert.Equal(2, availability.Count);
        Assert.Equal("njord/status", (string?)availability[0]!["topic"]);
        Assert.Equal("njord/state", (string?)availability[1]!["topic"]);
        Assert.Equal(
            "{% if value_json.k is not none %}online{% else %}offline{% endif %}",
            (string?)availability[1]!["value_template"]);
    }

    [Fact]
    public void ToComponent_json_attributes_follow_the_value_template()
    {
        var spec = new SensorComponentSpec(
            "frost", "id", "frost", "njord/alerts/frost", "{{ value_json.severity }}",
            JsonAttributesTopic: "njord/alerts/frost",
            JsonAttributesTemplate: "{{ value_json | tojson }}");

        var component = DiscoveryPayloadBuilder.ToComponent(spec, "njord/status", 60);

        Assert.Equal("njord/alerts/frost", (string?)component["json_attributes_topic"]);
        Assert.Equal("{{ value_json | tojson }}", (string?)component["json_attributes_template"]);
    }
}
