using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Messages.Pipeline;

public interface IEnrichmentEvent
{
    EnrichmentKind Kind { get; }
}

public abstract record PipelineEvent
{
    public sealed record ForecastReceived(
        LocationId Location,
        WeatherModel Model,
        WeatherForecast Forecast) : PipelineEvent;

    public sealed record ConsensusComputed(
        ConsensusSnapshot Consensus,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Consensus;
    }

    public sealed record AlertComputed(
        AlertResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Alerts;
    }

    public sealed record DerivedComputed(
        DerivedResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Derived;
    }

    public sealed record TrendComputed(
        TrendResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Trends;
    }

    public sealed record IndexComputed(
        IndexResult Result,
        DateTimeOffset Timestamp) : PipelineEvent, IEnrichmentEvent
    {
        public EnrichmentKind Kind => EnrichmentKind.Indices;
    }
}
