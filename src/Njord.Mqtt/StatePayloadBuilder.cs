using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Njord.Consensus;
using Njord.Messages.Alerts;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Messages.Pipeline;
using Njord.Messages.Trends;
using Njord.Mqtt.Mapping;
using Njord.Mqtt.Serialization;
using Njord.Mqtt.Topics;
using Njord.Weather;

namespace Njord.Mqtt;

public static class StatePayloadBuilder
{
    public static Dictionary<Horizon, string> BuildPerHorizon(
        Forecast forecast,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> horizons,
        int forecastDays,
        DateTimeOffset anchorTime,
        int? maxForecastHours = null)
    {
        var projected = HorizonProjection.BuildPerHorizon(
            forecast, parameters, horizons, forecastDays, anchorTime, maxForecastHours);

        var result = new Dictionary<Horizon, string>(projected.Count);
        foreach (var (horizon, values) in projected)
        {
            result[horizon] = Serialize(values.ToPayload(), MqttPayloadJsonContext.Default.ForecastHorizonPayload);
        }

        return result;
    }

    public static IReadOnlyList<MqttMessage> FromConsensus(
        ConsensusResult consensus, string baseTopic, string location)
    {
        var messages = new List<MqttMessage>();

        EmitConsensusMessages(consensus.Parameters, baseTopic, location, messages);
        EmitConsensusMessages(consensus.DailyParameters, baseTopic, location, messages);

        return messages;
    }

    public static IReadOnlyList<MqttMessage> FromConsensus(
        LocationConsensus consensus, string baseTopic)
        => FromConsensus(
            new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters, consensus.ComputedAt),
            baseTopic, consensus.Location.Name);

    private static void EmitConsensusMessages(
        IReadOnlyList<ParameterConsensus> parameters,
        string baseTopic, string location,
        List<MqttMessage> messages)
    {
        foreach (var (horizon, payload) in parameters.ToPayload())
        {
            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, location, EnrichmentKind.Consensus.ToWireName(), horizon.ToWireKey());
            messages.Add(new MqttMessage(topic, Serialize(payload, MqttPayloadJsonContext.Default.ConsensusHorizonPayload), true));
        }
    }

    public static IReadOnlyList<MqttMessage> FromAlerts(AlertResult result, string baseTopic)
    {
        var messages = new List<MqttMessage>(result.Alerts.Count);
        foreach (var alert in result.Alerts)
        {
            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentKind.Alerts.ToWireName(), alert.Type.ToTopicSegment());
            messages.Add(new MqttMessage(topic, Serialize(alert.ToPayload(), MqttPayloadJsonContext.Default.AlertPayload), true));
        }
        return messages;
    }

    public static IReadOnlyList<MqttMessage> FromDerived(DerivedResult result, string baseTopic)
    {
        var messages = new List<MqttMessage>();

        foreach (var (horizon, derived) in result.ByHorizon)
        {
            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentKind.Derived.ToWireName(), horizon.ToWireKey());
            messages.Add(new MqttMessage(topic, Serialize(derived.ToPayload(), MqttPayloadJsonContext.Default.DerivedHorizonPayload), true));
        }

        var metaTopic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentKind.Derived.ToWireName(), "meta");
        messages.Add(new MqttMessage(metaTopic, Serialize(result.Scalars.ToPayload(), MqttPayloadJsonContext.Default.DerivedMetaPayload), true));

        return messages;
    }

    public static IReadOnlyList<MqttMessage> FromTrends(TrendResult result, string baseTopic)
    {
        var topic = TopicScheme.EnrichmentTopic(baseTopic, result.Location.Name, EnrichmentKind.Trends.ToWireName());
        return [new MqttMessage(topic, Serialize(result.ToPayload(), MqttPayloadJsonContext.Default.TrendPayload), true)];
    }

    public static IReadOnlyList<MqttMessage> FromIndices(IndexResult result, string baseTopic)
    {
        var messages = new List<MqttMessage>();

        foreach (var day in result.Days ?? [])
        {
            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentKind.Indices.ToWireName(), Horizon.Days(day.DayOffset).ToWireKey());
            messages.Add(new MqttMessage(topic, Serialize(day.ToPayload(result), MqttPayloadJsonContext.Default.IndexDayPayload), true));
        }

        return messages;
    }

    private static string Serialize<T>(T payload, JsonTypeInfo<T> typeInfo)
        => JsonSerializer.Serialize(payload, typeInfo);
}
