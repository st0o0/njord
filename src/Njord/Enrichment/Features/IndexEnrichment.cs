using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Enrichment.Features;

internal sealed class IndexEnrichment : IStatelessEnrichment<IndexResult>
{
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly IndexOptions _indexOptions;
    private readonly bool _enabled;

    public string TypeName => "indices";
    public bool Enabled => _enabled;

    public IndexEnrichment(
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        _parameters = parameters;
        _timeProvider = timeProvider;
        _indexOptions = enrichmentOptions.Value.Indices;
        _enabled = enrichmentOptions.Value.Indices.Enabled;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)
    {
        foreach (var location in locations)
        {
            var result = IndexResult.Compute(
                snapshot, location, _parameters, _timeProvider, _indexOptions);
            yield return new EgressEvent.EnrichmentUpdate(location, TypeName, result);
        }
    }

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);

        var indexTopic = TopicScheme.EnrichmentTopic(ctx.Mqtt.BaseTopic, location, TypeName);

        var components = new JsonObject();

        var scoreSensors = new[] { "laundry", "outdoor", "running", "cycling", "bbq", "irrigation", "solar", "ventilation" };

        foreach (var key in scoreSensors)
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = key.Replace('_', ' '),
                ["state_topic"] = indexTopic,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        foreach (var (key, name) in new (string, string)[] { ("hdd", "heating degree days"), ("cdd", "cooling degree days") })
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["state_topic"] = indexTopic,
                ["unit_of_measurement"] = "°Cd",
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        foreach (var (key, name, unit) in new (string, string, string)[]
                 {
                     ("frost_hours", "frost in", "h"),
                     ("frost_confidence", "frost confidence", ""),
                     ("vpd_kpa", "VPD", "kPa"),
                 })
        {
            var comp = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["state_topic"] = indexTopic,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
            if (!string.IsNullOrEmpty(unit))
                comp["unit_of_measurement"] = unit;
            components[key] = comp;
        }

        components["vpd_category"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_vpd_category",
            ["name"] = "VPD category",
            ["state_topic"] = indexTopic,
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.vpd_category }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromIndices((IndexResult)result, baseTopic);
}
