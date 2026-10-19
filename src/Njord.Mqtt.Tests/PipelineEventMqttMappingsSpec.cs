using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Mqtt.Mapping;
using Njord.Mqtt.Topics;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class PipelineEventMqttMappingsSpec
{
    private const string BaseTopic = "njord";

    private static readonly AnalysisFixture Fixture = AnalysisFixture.Create();

    private static readonly MqttPublishContext Context = new(
        BaseTopic, AnalysisFixture.Parameters, AnalysisFixture.Horizons, AnalysisFixture.ForecastDays, AnalysisFixture.Now);

    private static LocationAnalysis Lucerne => Fixture.For(AnalysisFixture.Lucerne);

    [Fact]
    public void A_forecast_maps_to_one_retained_message_per_horizon()
    {
        var forecast = Fixture.Forecasts.First(f => f.Location == AnalysisFixture.Lucerne && f.Model == AnalysisFixture.IconD2);
        var pipelineEvent = new PipelineEvent.ForecastReceived(new LocationId(forecast.Location), forecast.Model, forecast);
        var maxHours = ModelCoverageRegistry.Get(forecast.Model.Id)?.MaxForecastHours;
        var expected = StatePayloadBuilder.BuildPerHorizon(
            forecast, AnalysisFixture.Parameters, AnalysisFixture.Horizons, AnalysisFixture.ForecastDays, AnalysisFixture.Now, maxHours);

        var messages = pipelineEvent.ToMqttMessages(Context).ToList();

        Assert.Equal(expected.Count, messages.Count);
        Assert.NotEmpty(messages);
        foreach (var (horizon, payload) in expected)
        {
            var topic = TopicScheme.HorizonTopic(BaseTopic, forecast.Location, forecast.Model, horizon.ToWireKey());
            var message = Assert.Single(messages, m => m.Topic == topic);
            Assert.Equal(payload, message.Payload);
            Assert.True(message.Retain);
        }
    }

    [Fact]
    public void A_consensus_event_maps_to_the_consensus_state_messages()
    {
        var pipelineEvent = new PipelineEvent.ConsensusComputed(Lucerne.Consensus, AnalysisFixture.Now);

        AssertSameMessages(StatePayloadBuilder.FromConsensus(Lucerne.Consensus, BaseTopic), pipelineEvent.ToMqttMessages(Context));
    }

    [Fact]
    public void An_alert_event_maps_to_the_alert_state_messages()
    {
        var pipelineEvent = new PipelineEvent.AlertComputed(Lucerne.Alerts, AnalysisFixture.Now);

        AssertSameMessages(StatePayloadBuilder.FromAlerts(Lucerne.Alerts, BaseTopic), pipelineEvent.ToMqttMessages(Context));
    }

    [Fact]
    public void A_derived_event_maps_to_the_derived_state_messages()
    {
        var pipelineEvent = new PipelineEvent.DerivedComputed(Lucerne.Derived, AnalysisFixture.Now);

        AssertSameMessages(StatePayloadBuilder.FromDerived(Lucerne.Derived, BaseTopic), pipelineEvent.ToMqttMessages(Context));
    }

    [Fact]
    public void A_trend_event_maps_to_the_trend_state_messages()
    {
        var pipelineEvent = new PipelineEvent.TrendComputed(Lucerne.Trends, AnalysisFixture.Now);

        AssertSameMessages(StatePayloadBuilder.FromTrends(Lucerne.Trends, BaseTopic), pipelineEvent.ToMqttMessages(Context));
    }

    [Fact]
    public void An_index_event_maps_to_the_index_state_messages()
    {
        var pipelineEvent = new PipelineEvent.IndexComputed(Lucerne.Indices, AnalysisFixture.Now);

        AssertSameMessages(StatePayloadBuilder.FromIndices(Lucerne.Indices, BaseTopic), pipelineEvent.ToMqttMessages(Context));
    }

    [Fact]
    public void Every_event_type_reports_its_location_name()
    {
        var forecast = Fixture.Forecasts.First(f => f.Location == AnalysisFixture.Lucerne);
        PipelineEvent[] events =
        [
            new PipelineEvent.ForecastReceived(new LocationId(forecast.Location), forecast.Model, forecast),
            new PipelineEvent.ConsensusComputed(Lucerne.Consensus, AnalysisFixture.Now),
            new PipelineEvent.AlertComputed(Lucerne.Alerts, AnalysisFixture.Now),
            new PipelineEvent.DerivedComputed(Lucerne.Derived, AnalysisFixture.Now),
            new PipelineEvent.TrendComputed(Lucerne.Trends, AnalysisFixture.Now),
            new PipelineEvent.IndexComputed(Lucerne.Indices, AnalysisFixture.Now),
        ];

        Assert.Equal(6, events.Length);
        Assert.All(events, e => Assert.Equal(AnalysisFixture.Lucerne, e.ToLocationName()));
    }

    [Fact]
    public void An_unknown_event_maps_to_no_messages_and_the_unknown_location()
    {
        var pipelineEvent = new UnknownEvent();

        Assert.Empty(pipelineEvent.ToMqttMessages(Context));
        Assert.Equal("unknown", pipelineEvent.ToLocationName());
    }

    private static void AssertSameMessages(IReadOnlyList<MqttMessage> expected, IEnumerable<MqttMessage> actual)
    {
        var mapped = actual.ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, mapped);
    }

    private sealed record UnknownEvent : PipelineEvent;
}
