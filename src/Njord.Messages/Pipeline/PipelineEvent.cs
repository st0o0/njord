using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Messages.Pipeline;

public interface IEnrichmentEvent
{
    EnrichmentKind Kind { get; }

    Location Location { get; }
}

public abstract record PipelineEvent
{
    public sealed record WeatherDataReceived(
        Location Location,
        DomainForecast<WeatherPoint> Forecast) : PipelineEvent;

    public sealed record AirQualityDataReceived(
        Location Location,
        DomainForecast<AirQualityPoint> Forecast) : PipelineEvent;

    public sealed record FloodDataReceived(
        Location Location,
        DomainForecast<FloodPoint> Forecast) : PipelineEvent;

    public sealed record SolarDataReceived(
        Location Location,
        DomainForecast<SolarPoint> Forecast) : PipelineEvent;

    public sealed record MarineDataReceived(
        Location Location,
        DomainForecast<MarinePoint> Forecast) : PipelineEvent;

    public sealed record AlertRaised(
        Location Location,
        Alert Alert,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Alerts;

        Location IEnrichmentEvent.Location => Location;

        public AlertType Type => Alert.Type;

        public AlertSeverity Severity => Alert.Severity;

        public ConfidenceLevel Confidence => Alert.Level;

        public AlertDetails Details => Alert.Details;
    }

    public sealed record AlertComputed(
        AlertResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Alerts;

        public Location Location => Result.Location;
    }

}
