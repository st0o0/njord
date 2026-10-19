using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Njord.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Mqtt.Mapping;
using Njord.Mqtt.Serialization;
using Njord.Mqtt.Topics;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Mqtt;

public static class StatePayloadBuilder
{
    public static Dictionary<Horizon, string> BuildPerHorizon(
        WeatherForecast forecast,
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

    private static string Serialize<T>(T payload, JsonTypeInfo<T> typeInfo)
        => JsonSerializer.Serialize(payload, typeInfo);
}
