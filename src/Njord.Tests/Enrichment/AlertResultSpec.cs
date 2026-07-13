using System.Text.Json.Nodes;
using Njord.Enrichment;

namespace Njord.Tests.Enrichment;

public sealed class AlertResultSpec
{
    [Fact(Timeout = 5000)]
    public void Alert_none_has_zero_confidence_and_none_severity()
    {
        var alert = Alert.None(AlertType.Frost);
        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.Confidence);
        Assert.Empty(alert.Attributes);
    }

    [Fact(Timeout = 5000)]
    public void ToMqttMessages_produces_one_message_per_alert()
    {
        var alerts = new List<Alert>
        {
            new(AlertType.Frost, AlertSeverity.Yellow, 0.75, new Dictionary<string, object?> { ["expected_low"] = -2.1 }),
            Alert.None(AlertType.Heat),
        };
        var result = new AlertResult("lucerne", alerts);
        var messages = result.ToMqttMessages("njord");

        Assert.Equal(2, messages.Count);
        Assert.Equal("njord/lucerne/alerts/frost", messages[0].Topic);
        Assert.Equal("njord/lucerne/alerts/heat", messages[1].Topic);
        Assert.True(messages[0].Retain);
    }

    [Fact(Timeout = 5000)]
    public void Frost_message_payload_contains_severity_and_confidence()
    {
        var alert = new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75,
            new Dictionary<string, object?> { ["expected_low"] = -2.1 });
        var result = new AlertResult("lucerne", [alert]);
        var messages = result.ToMqttMessages("njord");

        var payload = JsonNode.Parse(messages[0].Payload)!;
        Assert.Equal("yellow", (string?)payload["severity"]);
        Assert.Equal(0.75, (double?)payload["confidence"]);
        Assert.Equal(-2.1, (double?)payload["expected_low"]);
    }

    [Fact(Timeout = 5000)]
    public void None_severity_still_publishes()
    {
        var alert = Alert.None(AlertType.Frost);
        var result = new AlertResult("lucerne", [alert]);
        var messages = result.ToMqttMessages("njord");

        Assert.Single(messages);
        var payload = JsonNode.Parse(messages[0].Payload)!;
        Assert.Equal("none", (string?)payload["severity"]);
        Assert.Equal(0.0, (double?)payload["confidence"]);
    }

    [Fact(Timeout = 5000)]
    public void All_alert_type_topic_segments()
    {
        Assert.Equal("frost", AlertType.Frost.ToTopicSegment());
        Assert.Equal("heat", AlertType.Heat.ToTopicSegment());
        Assert.Equal("storm", AlertType.Storm.ToTopicSegment());
        Assert.Equal("heavy-rain", AlertType.HeavyRain.ToTopicSegment());
        Assert.Equal("uv", AlertType.Uv.ToTopicSegment());
        Assert.Equal("fog", AlertType.Fog.ToTopicSegment());
        Assert.Equal("snow", AlertType.Snow.ToTopicSegment());
        Assert.Equal("pressure-drop", AlertType.PressureDrop.ToTopicSegment());
        Assert.Equal("thunderstorm", AlertType.Thunderstorm.ToTopicSegment());
    }
}
