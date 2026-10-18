using Njord.Messages.Pipeline;
using Njord.Mqtt.Topics;
using Njord.Forecast;
using Njord.Model;

namespace Njord.Mqtt.Mapping;

internal sealed record MqttPublishContext(
    string BaseTopic,
    ResolvedParameterSet Parameters,
    IReadOnlyList<int> Horizons,
    int ForecastDays,
    DateTimeOffset Now);

internal static class PipelineEventMqttMappings
{
    public const string UnknownLocation = "unknown";

    public static IEnumerable<MqttMessage> ToMqttMessages(this PipelineEvent pipelineEvent, MqttPublishContext context)
        => pipelineEvent switch
        {
            PipelineEvent.ForecastReceived e => e.ToHorizonMessages(context),
            PipelineEvent.ConsensusComputed c => StatePayloadBuilder.FromConsensus(c.Consensus, context.BaseTopic),
            PipelineEvent.AlertComputed a => StatePayloadBuilder.FromAlerts(a.Result, context.BaseTopic),
            PipelineEvent.DerivedComputed d => StatePayloadBuilder.FromDerived(d.Result, context.BaseTopic),
            PipelineEvent.TrendComputed t => StatePayloadBuilder.FromTrends(t.Result, context.BaseTopic),
            PipelineEvent.IndexComputed i => StatePayloadBuilder.FromIndices(i.Result, context.BaseTopic),
            _ => [],
        };

    public static string ToLocationName(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.ForecastReceived u => u.Location.Name,
        PipelineEvent.ConsensusComputed c => c.Consensus.Location.Name,
        PipelineEvent.AlertComputed a => a.Result.Location.Name,
        PipelineEvent.DerivedComputed d => d.Result.Location.Name,
        PipelineEvent.TrendComputed t => t.Result.Location.Name,
        PipelineEvent.IndexComputed i => i.Result.Location.Name,
        _ => UnknownLocation,
    };

    private static List<MqttMessage> ToHorizonMessages(this PipelineEvent.ForecastReceived e, MqttPublishContext context)
    {
        var maxHours = ModelCoverageRegistry.Get(e.Model.Id)?.MaxForecastHours;
        var perHorizon = StatePayloadBuilder.BuildPerHorizon(
            e.Forecast, context.Parameters, context.Horizons, context.ForecastDays, context.Now, maxHours);

        var messages = new List<MqttMessage>(perHorizon.Count);
        foreach (var (horizon, payload) in perHorizon)
        {
            var topic = TopicScheme.HorizonTopic(context.BaseTopic, e.Location.Name, e.Model, horizon.ToWireKey());
            messages.Add(new MqttMessage(topic, payload, true));
        }

        return messages;
    }
}
