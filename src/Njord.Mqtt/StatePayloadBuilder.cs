using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Mqtt.Mapping;
using Njord.Mqtt.Payloads;
using Njord.Mqtt.Serialization;
using Njord.Mqtt.Topics;

namespace Njord.Mqtt;

public static class StatePayloadBuilder
{
    public static IReadOnlyList<MqttMessage> FromAlerts(AlertResult result, string baseTopic)
    {
        var messages = new List<MqttMessage>(result.Alerts.Count);
        foreach (var alert in result.Alerts)
        {
            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentNames.Alerts, alert.Type.ToTopicSegment());
            messages.Add(new MqttMessage(topic, Serialize(alert.ToPayload(), MqttPayloadJsonContext.Default.AlertPayload), true));
        }
        return messages;
    }

    public static MqttMessage FromAlert(AirQualityAlert alert, Location location, string baseTopic)
        => AlertMessage(baseTopic, location, alert.Type.ToWireName(), alert.ToPayload());

    public static MqttMessage FromAlert(FloodAlert alert, Location location, string baseTopic)
        => AlertMessage(baseTopic, location, alert.Type.ToWireName(), alert.ToPayload());

    private static MqttMessage AlertMessage(string baseTopic, Location location, string segment, AlertPayload payload)
        => new(
            TopicScheme.EnrichmentSubTopic(baseTopic, location.Name, EnrichmentNames.Alerts, segment),
            Serialize(payload, MqttPayloadJsonContext.Default.AlertPayload),
            true);

    private static string Serialize<T>(T payload, JsonTypeInfo<T> typeInfo)
        => JsonSerializer.Serialize(payload, typeInfo);
}
