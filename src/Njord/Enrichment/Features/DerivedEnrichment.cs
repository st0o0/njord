using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Enrichment.Features;

internal sealed class DerivedEnrichment : IStatelessEnrichment<DerivedResult>
{
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly TimeProvider _timeProvider;
    private readonly bool _enabled;

    public string TypeName => "derived";
    public bool Enabled => _enabled;

    public DerivedEnrichment(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        _parameters = parameters;
        _horizons = [.. options.Value.Horizons];
        _timeProvider = timeProvider;
        _enabled = enrichmentOptions.Value.Derived.Enabled;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)
    {
        foreach (var location in locations)
        {
            var result = DerivedResult.Compute(
                snapshot, location, _horizons, _parameters, _timeProvider);
            yield return new EgressEvent.EnrichmentUpdate(location, TypeName, result);
        }
    }

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);

        var components = new JsonObject();

        var horizonParams = new (string Key, string Name, string? Unit, string? DeviceClass, string Platform)[]
        {
            ("beaufort", "beaufort", null, null, "sensor"),
            ("wind_chill", "wind chill", "°C", "temperature", "sensor"),
            ("dewpoint_comfort", "dew-point comfort", null, null, "sensor"),
            ("wmo_description", "weather", null, null, "sensor"),
        };

        foreach (var hours in _horizons)
        {
            var horizonTopic = TopicScheme.EnrichmentSubTopic(
                ctx.Mqtt.BaseTopic, location, TypeName, $"h{hours}");

            foreach (var (key, name, unit, deviceClass, platform) in horizonParams)
            {
                var uniqueId = $"{deviceId}_{key}_h{hours}";
                var component = new JsonObject
                {
                    ["p"] = platform,
                    ["unique_id"] = uniqueId,
                    ["name"] = $"{name} +{hours}h",
                    ["state_topic"] = horizonTopic,
                    ["expire_after"] = expireAfterSeconds,
                    ["value_template"] = $"{{{{ value_json.{key} }}}}",
                    ["availability"] = new JsonArray(
                        new JsonObject { ["topic"] = availabilityTopic },
                        new JsonObject
                        {
                            ["topic"] = horizonTopic,
                            ["value_template"] = $"{{% if value_json.{key} is not none %}}online{{% else %}}offline{{% endif %}}",
                        }),
                    ["availability_mode"] = "all",
                };

                if (unit is not null)
                    component["unit_of_measurement"] = unit;
                if (deviceClass is not null)
                    component["device_class"] = deviceClass;

                components[$"{key}_h{hours}"] = component;
            }
        }

        var metaTopic = TopicScheme.EnrichmentSubTopic(ctx.Mqtt.BaseTopic, location, TypeName, "meta");

        components["diurnal_amplitude"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_diurnal_amplitude",
            ["name"] = "diurnal amplitude",
            ["state_topic"] = metaTopic,
            ["unit_of_measurement"] = "°C",
            ["device_class"] = "temperature",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.diurnal_amplitude }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["sunshine_pct"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_sunshine_pct",
            ["name"] = "sunshine",
            ["state_topic"] = metaTopic,
            ["unit_of_measurement"] = "%",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.sunshine_pct }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["inversion"] = new JsonObject
        {
            ["p"] = "binary_sensor",
            ["unique_id"] = $"{deviceId}_inversion",
            ["name"] = "inversion",
            ["state_topic"] = metaTopic,
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{% if value_json.inversion == true %}ON{% else %}OFF{% endif %}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromDerived((DerivedResult)result, baseTopic);
}
