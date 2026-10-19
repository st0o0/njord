using System.Text.Json.Nodes;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Mqtt.Tests;

public sealed class StatePayloadBuilderSpec
{
    [Fact]
    public void FromAlerts_produces_one_message_per_alert()
    {
        var alerts = new List<Alert>
        {
            new(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, Horizon.Hours(4), 3)),
            Alert.None(AlertType.Heat),
        };
        var result = new AlertResult(new Location("lucerne", 0, 0), alerts);

        var messages = StatePayloadBuilder.FromAlerts(result, "njord");

        Assert.Equal(2, messages.Count);
        Assert.Equal("njord/lucerne/alerts/frost", messages[0].Topic);
        Assert.Equal("njord/lucerne/alerts/heat", messages[1].Topic);
        Assert.True(messages[0].Retain);
        var payload = JsonNode.Parse(messages[0].Payload)!;
        Assert.Equal("yellow", (string?)payload["severity"]);
        Assert.Equal(0.75, (double?)payload["confidence"]);
    }

    [Fact]
    public void FromAlerts_writes_frost_details_as_snake_case_attributes()
    {
        var alert = new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, Horizon.Hours(4), 3));

        var message = Assert.Single(StatePayloadBuilder.FromAlerts(
            new AlertResult(new Location("lucerne", 0, 0), [alert]), "njord"));

        Assert.Equal(
            "{\"severity\":\"yellow\",\"confidence\":0.75,\"trigger_value\":0,\"threshold\":0,"
            + "\"peak_value\":null,\"hours_until\":null,\"duration_hours\":null,"
            + "\"expected_low\":-2.1,\"earliest_frost\":\"\\u002B4h\",\"models_agreeing\":3}",
            message.Payload);
    }
}
