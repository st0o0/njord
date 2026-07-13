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

    [Fact(Timeout = 5000)]
    public void Consensus_device_id_and_model_name()
    {
        var payload = DiscoveryPayloadBuilder.BuildConsensus(
            "lucerne", SmallParams, DefaultHorizons, 4, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("njord_lucerne_consensus", (string?)json["dev"]!["ids"]![0]);
        Assert.Equal("njord lucerne consensus", (string?)json["dev"]!["name"]);
        Assert.Equal("consensus", (string?)json["dev"]!["mdl"]);
    }

    [Fact(Timeout = 5000)]
    public void Consensus_components_use_consensus_topic()
    {
        var payload = DiscoveryPayloadBuilder.BuildConsensus(
            "lucerne", new ResolvedParameterSet([Temperature], []),
            [3], 4, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;
        var component = json["cmps"]!["temperature_h3"]!;

        Assert.Equal("njord_lucerne_consensus_temperature_h3", (string?)component["unique_id"]);

        var availability = component["availability"]!.AsArray();
        Assert.Equal("njord/lucerne/consensus/h3", (string?)availability[1]!["topic"]);
    }

    [Fact(Timeout = 5000)]
    public void Consensus_only_includes_hourly_parameters()
    {
        var payload = DiscoveryPayloadBuilder.BuildConsensus(
            "lucerne", SmallParams, DefaultHorizons, 4, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        // 2 hourly × 6 horizons = 12 (no daily components)
        Assert.Equal(12, json["cmps"]!.AsObject().Count);
    }

    [Fact(Timeout = 5000)]
    public void Alert_device_has_9_binary_sensor_components()
    {
        var payload = DiscoveryPayloadBuilder.BuildAlerts(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("njord_lucerne_alerts", (string?)json["dev"]!["ids"]![0]);
        Assert.Equal("alerts", (string?)json["dev"]!["mdl"]);
        Assert.Equal(9, json["cmps"]!.AsObject().Count);
    }

    [Fact(Timeout = 5000)]
    public void Alert_components_are_binary_sensors_with_value_template()
    {
        var payload = DiscoveryPayloadBuilder.BuildAlerts(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;
        var frost = json["cmps"]!["frost"]!;

        Assert.Equal("binary_sensor", (string?)frost["p"]);
        Assert.Contains("severity", (string?)frost["value_template"]);
        Assert.Equal("njord_lucerne_alerts_frost", (string?)frost["unique_id"]);
    }

    // --- Derived device ---

    [Fact(Timeout = 5000)]
    public void Derived_device_id_and_model_name()
    {
        var payload = DiscoveryPayloadBuilder.BuildDerived(
            "lucerne", DefaultHorizons, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("njord_lucerne_derived", (string?)json["dev"]!["ids"]![0]);
        Assert.Equal("njord lucerne derived", (string?)json["dev"]!["name"]);
        Assert.Equal("derived", (string?)json["dev"]!["mdl"]);
    }

    [Fact(Timeout = 5000)]
    public void Derived_device_has_expected_component_count()
    {
        var payload = DiscoveryPayloadBuilder.BuildDerived(
            "lucerne", DefaultHorizons, Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        // 4 horizon params × 6 horizons + 3 scalar sensors = 27
        Assert.Equal(27, json["cmps"]!.AsObject().Count);
    }

    [Fact(Timeout = 5000)]
    public void Derived_beaufort_component_per_horizon()
    {
        var payload = DiscoveryPayloadBuilder.BuildDerived(
            "lucerne", [3], Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;
        var component = json["cmps"]!["beaufort_h3"]!;

        Assert.Equal("sensor", (string?)component["p"]);
        Assert.Equal("njord_lucerne_derived_beaufort_h3", (string?)component["unique_id"]);
        Assert.False(component.AsObject().ContainsKey("unit_of_measurement"));
        Assert.False(component.AsObject().ContainsKey("device_class"));
    }

    [Fact(Timeout = 5000)]
    public void Derived_wind_chill_has_temperature_device_class()
    {
        var payload = DiscoveryPayloadBuilder.BuildDerived(
            "lucerne", [3], Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;
        var component = json["cmps"]!["wind_chill_h3"]!;

        Assert.Equal("°C", (string?)component["unit_of_measurement"]);
        Assert.Equal("temperature", (string?)component["device_class"]);
    }

    [Fact(Timeout = 5000)]
    public void Derived_scalar_inversion_is_binary_sensor()
    {
        var payload = DiscoveryPayloadBuilder.BuildDerived(
            "lucerne", [3], Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;
        var component = json["cmps"]!["inversion"]!;

        Assert.Equal("binary_sensor", (string?)component["p"]);
        Assert.Equal("njord_lucerne_derived_inversion", (string?)component["unique_id"]);
    }

    [Fact(Timeout = 5000)]
    public void Derived_scalar_sunshine_has_percent_unit()
    {
        var payload = DiscoveryPayloadBuilder.BuildDerived(
            "lucerne", [3], Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;
        var component = json["cmps"]!["sunshine_pct"]!;

        Assert.Equal("sensor", (string?)component["p"]);
        Assert.Equal("%", (string?)component["unit_of_measurement"]);
    }

    // --- Trend device ---

    [Fact(Timeout = 5000)]
    public void Trend_device_id_and_model_name()
    {
        var payload = DiscoveryPayloadBuilder.BuildTrends(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("njord_lucerne_trends", (string?)json["dev"]!["ids"]![0]);
        Assert.Equal("njord lucerne trends", (string?)json["dev"]!["name"]);
        Assert.Equal("trends", (string?)json["dev"]!["mdl"]);
    }

    [Fact(Timeout = 5000)]
    public void Trend_device_has_expected_component_count()
    {
        var payload = DiscoveryPayloadBuilder.BuildTrends(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        // 6 text sensors + 6 numeric sensors = 12
        Assert.Equal(12, json["cmps"]!.AsObject().Count);
    }

    [Fact(Timeout = 5000)]
    public void Trend_direction_sensors_are_text()
    {
        var payload = DiscoveryPayloadBuilder.BuildTrends(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;
        var component = json["cmps"]!["trend_temperature_dir"]!;

        Assert.Equal("sensor", (string?)component["p"]);
        Assert.False(component.AsObject().ContainsKey("unit_of_measurement"));
    }

    [Fact(Timeout = 5000)]
    public void Trend_timing_sensors_have_hour_unit()
    {
        var payload = DiscoveryPayloadBuilder.BuildTrends(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("h", (string?)json["cmps"]!["precip_starts"]!["unit_of_measurement"]);
        Assert.Equal("h", (string?)json["cmps"]!["temp_max_in"]!["unit_of_measurement"]);
        Assert.Equal("h", (string?)json["cmps"]!["reliable_hours"]!["unit_of_measurement"]);
    }

    [Fact(Timeout = 5000)]
    public void Trend_decay_rate_has_correct_unit()
    {
        var payload = DiscoveryPayloadBuilder.BuildTrends(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("°C/h", (string?)json["cmps"]!["decay_rate"]!["unit_of_measurement"]);
    }

    // --- Index device ---

    [Fact(Timeout = 5000)]
    public void Index_device_id_and_model_name()
    {
        var payload = DiscoveryPayloadBuilder.BuildIndices(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("njord_lucerne_indices", (string?)json["dev"]!["ids"]![0]);
        Assert.Equal("njord lucerne indices", (string?)json["dev"]!["name"]);
        Assert.Equal("indices", (string?)json["dev"]!["mdl"]);
    }

    [Fact(Timeout = 5000)]
    public void Index_device_has_expected_component_count()
    {
        var payload = DiscoveryPayloadBuilder.BuildIndices(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        // 8 scores + 2 degree days + 3 numeric (frost_hours, frost_confidence, vpd_kpa) + 1 text (vpd_category) = 14
        Assert.Equal(14, json["cmps"]!.AsObject().Count);
    }

    [Fact(Timeout = 5000)]
    public void Index_score_sensors_have_no_unit()
    {
        var payload = DiscoveryPayloadBuilder.BuildIndices(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.False(json["cmps"]!["laundry"]!.AsObject().ContainsKey("unit_of_measurement"));
        Assert.False(json["cmps"]!["outdoor"]!.AsObject().ContainsKey("unit_of_measurement"));
    }

    [Fact(Timeout = 5000)]
    public void Index_degree_day_sensors_have_unit()
    {
        var payload = DiscoveryPayloadBuilder.BuildIndices(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("°Cd", (string?)json["cmps"]!["hdd"]!["unit_of_measurement"]);
        Assert.Equal("°Cd", (string?)json["cmps"]!["cdd"]!["unit_of_measurement"]);
    }

    [Fact(Timeout = 5000)]
    public void Index_vpd_category_is_text_sensor()
    {
        var payload = DiscoveryPayloadBuilder.BuildIndices(
            "lucerne", Mqtt, TimeSpan.FromMinutes(60), "1.2.3-test");
        var json = JsonNode.Parse(payload)!;

        Assert.Equal("sensor", (string?)json["cmps"]!["vpd_category"]!["p"]);
        Assert.False(json["cmps"]!["vpd_category"]!.AsObject().ContainsKey("unit_of_measurement"));
    }
}
