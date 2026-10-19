using Njord.Consensus;
using Njord.Messages.Alerts;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Messages.Trends;
using Njord.Model;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Messages.Pipeline;

public interface IEnrichmentEvent
{
    EnrichmentKind Kind { get; }

    LocationId Location { get; }
}

public abstract record PipelineEvent
{
    public sealed record ForecastReceived(
        LocationId Location,
        WeatherModel Model,
        WeatherForecast Forecast) : PipelineEvent;

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

        LocationId IEnrichmentEvent.Location => new(Location.Name);

        public AlertType Type => Alert.Type;

        public AlertSeverity Severity => Alert.Severity;

        public ConfidenceLevel Confidence => Alert.Level;

        public AlertDetails Details => Alert.Details;
    }

    public sealed record ConsensusComputed(
        LocationConsensus Consensus,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Consensus;

        public LocationId Location => Consensus.Location;
    }

    public sealed record AlertComputed(
        AlertResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Alerts;

        public LocationId Location => Result.Location;
    }

    public sealed record DerivedComputed(
        DerivedResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Derived;

        public LocationId Location => Result.Location;
    }

    public sealed record TrendComputed(
        TrendResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Trends;

        public LocationId Location => Result.Location;
    }

    public sealed record IndexComputed(
        IndexResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Indices;

        public LocationId Location => Result.Location;
    }
}
