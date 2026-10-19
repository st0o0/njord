using Njord.Alerts;
using Njord.Alerts.AirQuality;
using Njord.Alerts.Flood;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Pipeline;

internal static class PipelineEventMappings
{
    public static PipelineEvent ToPipelineEvent(this DomainDataReceived received) => received.Domain switch
    {
        DomainKind.Weather => new PipelineEvent.WeatherDataReceived(received.Location, Typed<WeatherPoint>(received)),
        DomainKind.AirQuality => new PipelineEvent.AirQualityDataReceived(received.Location, Typed<AirQualityPoint>(received)),
        DomainKind.Flood => new PipelineEvent.FloodDataReceived(received.Location, Typed<FloodPoint>(received)),
        DomainKind.Solar => new PipelineEvent.SolarDataReceived(received.Location, Typed<SolarPoint>(received)),
        DomainKind.Marine => new PipelineEvent.MarineDataReceived(received.Location, Typed<MarinePoint>(received)),
        _ => throw new ArgumentOutOfRangeException(nameof(received), received.Domain, null),
    };

    public static PipelineEvent ToPipelineEvent(this EnrichmentEvent enrichmentEvent) => enrichmentEvent switch
    {
        AlertRaisedEvent raised => new PipelineEvent.AlertRaised(raised.Location, raised.Alert, raised.Timestamp),
        _ => throw new ArgumentOutOfRangeException(
            nameof(enrichmentEvent), enrichmentEvent.GetType().Name, "No pipeline event for this enrichment event"),
    };

    private static DomainForecast<T> Typed<T>(DomainDataReceived received)
        where T : IHasValidTime =>
        received.Data as DomainForecast<T>
        ?? throw new ArgumentException(
            $"Data of type {received.Data.GetType().Name} does not match domain {received.Domain}.", nameof(received));
}
