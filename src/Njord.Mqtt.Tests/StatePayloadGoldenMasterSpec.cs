using System.Text.Json.Nodes;
using Njord.Messages.Alerts;
using Njord.Mqtt.Topics;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class StatePayloadGoldenMasterSpec
{
    private const string BaseTopic = "njord";

    private static readonly AnalysisFixture Fixture = AnalysisFixture.Create();

    [Fact]
    public void Alert_messages_carry_severity_and_confidence()
    {
        var messages = Fixture.Analyses
            .SelectMany(a => StatePayloadBuilder.FromAlerts(a.Alerts, BaseTopic))
            .ToList();

        Assert.NotEmpty(messages);
        Assert.All(messages, m =>
        {
            Assert.True(m.Retain);
            Assert.Contains("/alerts/", m.Topic);
            var payload = JsonNode.Parse(m.Payload);
            Assert.NotNull(payload);
            var obj = payload.AsObject();
            Assert.NotNull(payload["severity"]);
            Assert.NotNull(payload["confidence"]);
            Assert.True(obj.ContainsKey("peak_value"));
            Assert.True(obj.ContainsKey("hours_until"));
            Assert.True(obj.ContainsKey("duration_hours"));
        });
    }

    [Fact]
    public void Per_model_horizon_payloads_are_valid_json_with_topic_structure()
    {
        foreach (var forecast in Fixture.Forecasts)
        {
            var perHorizon = StatePayloadBuilder.BuildPerHorizon(
                forecast, AnalysisFixture.Parameters, AnalysisFixture.Horizons,
                AnalysisFixture.ForecastDays, AnalysisFixture.Now);

            Assert.NotEmpty(perHorizon);
            foreach (var (horizon, payloadStr) in perHorizon)
            {
                var topic = TopicScheme.HorizonTopic(BaseTopic, forecast.Location, forecast.Model, horizon.ToWireKey());
                Assert.StartsWith("njord/", topic);
                var payload = JsonNode.Parse(payloadStr);
                Assert.NotNull(payload);
            }
        }
    }

    [Fact]
    public void Fixture_triggers_every_alert_type_in_the_published_messages()
    {
        var lucerne = Fixture.For(AnalysisFixture.Lucerne);
        var messages = StatePayloadBuilder.FromAlerts(lucerne.Alerts, BaseTopic);

        foreach (var type in Enum.GetValues<AlertType>())
        {
            var topic = $"{BaseTopic}/lucerne/alerts/{type.ToTopicSegment()}";
            var message = Assert.Single(messages, m => m.Topic == topic);
            var payload = JsonNode.Parse(message.Payload);
            Assert.NotNull(payload);
            Assert.NotEqual("none", payload["severity"]!.GetValue<string>());
        }
    }

    [Fact]
    public void Fixture_publishes_null_values_in_alert_payloads()
    {
        var payloads = Fixture.Analyses
            .SelectMany(a => StatePayloadBuilder.FromAlerts(a.Alerts, BaseTopic))
            .Select(m => m.Payload)
            .ToList();

        Assert.Contains(payloads, p => p.Contains(":null", StringComparison.Ordinal));
    }
}
