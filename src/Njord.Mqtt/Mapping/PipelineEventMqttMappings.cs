using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
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
            PipelineEvent.WeatherDataReceived e => e.Forecast.ToMessages(e.Location, context.BaseTopic),
            PipelineEvent.AirQualityDataReceived e => e.Forecast.ToMessages(e.Location, context.BaseTopic),
            PipelineEvent.FloodDataReceived e => e.Forecast.ToMessages(e.Location, context.BaseTopic),
            PipelineEvent.SolarDataReceived e => e.Forecast.ToMessages(e.Location, context.BaseTopic),
            PipelineEvent.MarineDataReceived e => e.Forecast.ToMessages(e.Location, context.BaseTopic),
            PipelineEvent.AlertRaised r => StatePayloadBuilder.FromAlerts(
                new AlertResult(new LocationId(r.Location.Name), [r.Alert]), context.BaseTopic),
            _ => [],
        };

    public static string ToLocationName(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.WeatherDataReceived e => e.Location.Name,
        PipelineEvent.AirQualityDataReceived e => e.Location.Name,
        PipelineEvent.FloodDataReceived e => e.Location.Name,
        PipelineEvent.SolarDataReceived e => e.Location.Name,
        PipelineEvent.MarineDataReceived e => e.Location.Name,
        PipelineEvent.AlertRaised r => r.Location.Name,
        _ => UnknownLocation,
    };
}
