using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Mqtt.Mapping;
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

    private static string Serialize<T>(T payload, JsonTypeInfo<T> typeInfo)
        => JsonSerializer.Serialize(payload, typeInfo);
}
