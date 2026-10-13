using System.Text;
using System.Text.Json.Nodes;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Tests.Shared;
using static VerifyXunit.Verifier;

namespace Njord.Mqtt.Tests;

public sealed class StatePayloadGoldenMasterSpec
{
    private const string BaseTopic = "njord";

    private static readonly AnalysisFixture Fixture = AnalysisFixture.Create();

    private static readonly string[] IndexKeys =
        ["laundry", "outdoor", "running", "cycling", "bbq", "irrigation", "solar", "night_ventilation"];

    [Fact]
    public Task Consensus_state_messages_match_golden_master() =>
        VerifyMessages(a => StatePayloadBuilder.FromConsensus(a.Consensus, BaseTopic));

    [Fact]
    public Task Alert_state_messages_match_golden_master() =>
        VerifyMessages(a => StatePayloadBuilder.FromAlerts(a.Alerts, BaseTopic));

    [Fact]
    public Task Derived_state_messages_match_golden_master() =>
        VerifyMessages(a => StatePayloadBuilder.FromDerived(a.Derived, BaseTopic));

    [Fact]
    public Task Trend_state_messages_match_golden_master() =>
        VerifyMessages(a => StatePayloadBuilder.FromTrends(a.Trends, BaseTopic));

    [Fact]
    public Task Index_state_messages_match_golden_master() =>
        VerifyMessages(a => StatePayloadBuilder.FromIndices(a.Indices, BaseTopic));

    [Fact]
    public Task Per_model_horizon_payloads_match_golden_master()
    {
        var text = new StringBuilder();
        foreach (var forecast in Fixture.Forecasts)
        {
            text.AppendLine($"## {forecast.Location} / {forecast.Model.Id}");
            var perHorizon = StatePayloadBuilder.BuildPerHorizon(
                forecast, AnalysisFixture.Parameters, AnalysisFixture.Horizons,
                AnalysisFixture.ForecastDays, AnalysisFixture.Now);
            foreach (var (horizon, payload) in perHorizon)
            {
                text.AppendLine(TopicScheme.HorizonTopic(BaseTopic, forecast.Location, forecast.Model, horizon.ToWireKey()));
                text.AppendLine(payload);
            }
        }

        return Verify(text.ToString());
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
    public void Fixture_publishes_every_index_kind_with_an_envelope()
    {
        var messages = Fixture.Analyses
            .SelectMany(a => StatePayloadBuilder.FromIndices(a.Indices, BaseTopic))
            .ToList();

        Assert.NotEmpty(messages);
        foreach (var message in messages)
        {
            var payload = JsonNode.Parse(message.Payload);
            Assert.NotNull(payload);
            foreach (var key in IndexKeys)
            {
                Assert.NotNull(payload[key]);
                Assert.NotNull(payload[$"{key}_confidence"]);
            }
        }
    }

    [Fact]
    public void Fixture_publishes_hourly_and_daily_horizons_and_null_values()
    {
        var consensusTopics = Fixture.Analyses
            .SelectMany(a => StatePayloadBuilder.FromConsensus(a.Consensus, BaseTopic))
            .Select(m => m.Topic)
            .ToList();
        var allPayloads = Fixture.Analyses
            .SelectMany(a => StatePayloadBuilder.FromDerived(a.Derived, BaseTopic)
                .Concat(StatePayloadBuilder.FromTrends(a.Trends, BaseTopic))
                .Concat(StatePayloadBuilder.FromAlerts(a.Alerts, BaseTopic)))
            .Select(m => m.Payload)
            .ToList();

        Assert.Contains(consensusTopics, t => t.EndsWith("/h3", StringComparison.Ordinal));
        Assert.Contains(consensusTopics, t => t.EndsWith("/d1", StringComparison.Ordinal));
        Assert.Contains(allPayloads, p => p.Contains(":null", StringComparison.Ordinal));
    }

    private static Task VerifyMessages(Func<LocationAnalysis, IReadOnlyList<MqttMessage>> build)
    {
        var text = new StringBuilder();
        foreach (var analysis in Fixture.Analyses)
        {
            foreach (var message in build(analysis))
            {
                text.AppendLine($"{message.Topic} retain={message.Retain}");
                text.AppendLine(message.Payload);
            }
        }

        return Verify(text.ToString());
    }
}
