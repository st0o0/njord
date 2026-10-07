using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Enrichment;

namespace Njord.Mqtt.Presentation;

internal sealed class AlertPresenter : IEnrichmentPresenter
{
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.Alerts;
    public bool Enabled => _enabled;

    public AlertPresenter(IOptions<NjordOptions> options)
    {
        _enabled = options.Value.Enrichment.IsEnabled(TypeName);
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public string BuildDiscoveryPayload(DiscoveryContext ctx, string location)
    {
        var deviceId = DeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(ctx.Mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * ctx.PollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var alertType in Enum.GetValues<AlertType>())
        {
            var segment = alertType.ToTopicSegment();
            var topic = TopicScheme.EnrichmentSubTopic(ctx.Mqtt.BaseTopic, location, TypeName, segment);
            var uniqueId = $"{deviceId}_{segment.Replace('-', '_')}";

            components[segment.Replace('-', '_')] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = uniqueId,
                ["name"] = segment,
                ["state_topic"] = topic,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = "{{ value_json.severity }}",
                ["json_attributes_topic"] = topic,
                ["json_attributes_template"] = "{{ value_json | tojson }}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromAlerts((AlertResult)result, baseTopic);
}
