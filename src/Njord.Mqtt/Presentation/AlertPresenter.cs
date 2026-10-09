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

        var specs = new List<SensorComponentSpec>();

        foreach (var alertType in Enum.GetValues<AlertType>())
        {
            var segment = alertType.ToTopicSegment();
            var topic = TopicScheme.EnrichmentSubTopic(ctx.Mqtt.BaseTopic, location, TypeName, segment);
            var uniqueId = $"{deviceId}_{segment.Replace('-', '_')}";

            specs.Add(new SensorComponentSpec(
                segment.Replace('-', '_'),
                uniqueId,
                segment,
                topic,
                "{{ value_json.severity }}",
                JsonAttributesTopic: topic,
                JsonAttributesTemplate: "{{ value_json | tojson }}"));
        }

        var components = new JsonObject();
        DiscoveryPayloadBuilder.AddComponents(components, specs, availabilityTopic, expireAfterSeconds);

        return DiscoveryPayloadBuilder.BuildDeviceEnvelope(
            deviceId, location, TypeName, ctx.Version, components);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromAlerts((AlertResult)result, baseTopic);
}
