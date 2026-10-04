using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Analysis;
using Njord.Configuration;
using Njord.Enrichment;

namespace Njord.Mqtt.Presentation;

internal sealed class HistoryPresenter : IEnrichmentPresenter
{
    private readonly IReadOnlyList<string> _models;
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.History;
    public bool Enabled => _enabled;

    public HistoryPresenter(IOptions<NjordOptions> options)
    {
        _models = [.. options.Value.Models];
        _enabled = options.Value.Enrichment.IsEnabled(TypeName);
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);
        var modelIds = _models;

        var historyTopic = TopicScheme.EnrichmentTopic(ctx.Mqtt.BaseTopic, location, TypeName);

        var components = new JsonObject();

        foreach (var modelId in modelIds)
        {
            var slug = modelId.Replace('-', '_').ToLowerInvariant();

            foreach (var (prefix, label) in new[] { ("mae_7d", "MAE 7d"), ("mae_30d", "MAE 30d"), ("weight", "weight"), ("drift", "drift") })
            {
                var key = $"{prefix}_{slug}";
                components[key] = new JsonObject
                {
                    ["p"] = "sensor",
                    ["unique_id"] = $"{deviceId}_{key}",
                    ["name"] = $"{label} {modelId}",
                    ["state_topic"] = historyTopic,
                    ["expire_after"] = expireAfterSeconds,
                    ["value_template"] = $"{{{{ value_json.{key} }}}}",
                    ["availability"] = new JsonArray(
                        new JsonObject { ["topic"] = availabilityTopic }),
                    ["availability_mode"] = "all",
                };
            }
        }

        components["seasonal_best"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_seasonal_best",
            ["name"] = "seasonal best model",
            ["state_topic"] = historyTopic,
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.seasonal_best }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["anomaly"] = new JsonObject
        {
            ["p"] = "binary_sensor",
            ["unique_id"] = $"{deviceId}_anomaly",
            ["name"] = "anomaly",
            ["state_topic"] = historyTopic,
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{% if value_json.anomaly == true %}ON{% else %}OFF{% endif %}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["anomaly_deviation"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_anomaly_deviation",
            ["name"] = "anomaly deviation",
            ["state_topic"] = historyTopic,
            ["unit_of_measurement"] = "σ",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.anomaly_deviation }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["weighted_temperature"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_weighted_temperature",
            ["name"] = "weighted temperature",
            ["state_topic"] = historyTopic,
            ["unit_of_measurement"] = "°C",
            ["device_class"] = "temperature",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.weighted_temperature }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromHistory((HistoryResult)result, baseTopic);
}
