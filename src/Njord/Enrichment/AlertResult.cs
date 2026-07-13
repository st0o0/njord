using System.Text.Json.Nodes;
using Njord.Egress;

namespace Njord.Enrichment;

public sealed record AlertResult(string Location, IReadOnlyList<Alert> Alerts)
{
    public IReadOnlyList<MqttMessage> ToMqttMessages(string baseTopic)
    {
        var messages = new List<MqttMessage>(Alerts.Count);
        foreach (var alert in Alerts)
        {
            var payload = new JsonObject
            {
                ["severity"] = alert.Severity.ToString().ToLowerInvariant(),
                ["confidence"] = alert.Confidence,
            };

            foreach (var (attrKey, attrValue) in alert.Attributes)
            {
                payload[attrKey] = attrValue switch
                {
                    double d => JsonValue.Create(d),
                    int i => JsonValue.Create(i),
                    string s => JsonValue.Create(s),
                    null => null,
                    _ => JsonValue.Create(attrValue.ToString()),
                };
            }

            var topic = TopicScheme.AlertTopic(baseTopic, Location, alert.Type.ToTopicSegment());
            messages.Add(new MqttMessage(topic, payload.ToJsonString(), true));
        }
        return messages;
    }
}
