using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Enrichment.Features;

internal sealed class AlertEnrichment : IStatelessEnrichment
{
    private readonly AlertThresholdOptions _alertOptions;
    private readonly TimeProvider _timeProvider;
    private readonly bool _enabled;

    public string TypeName => "alerts";
    public bool Enabled => _enabled;

    public AlertEnrichment(
        IOptions<EnrichmentOptions> enrichmentOptions,
        TimeProvider timeProvider)
    {
        _alertOptions = enrichmentOptions.Value.Alerts;
        _timeProvider = timeProvider;
        _enabled = enrichmentOptions.Value.Alerts.Enabled;
    }

    public string DeviceId(string location) =>
        TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)
    {
        foreach (var location in locations)
        {
            var result = AlertEvaluator.EvaluateAll(snapshot, location, _alertOptions, _timeProvider);
            yield return new EgressEvent.EnrichmentUpdate(location, TypeName, result);
        }
    }

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
