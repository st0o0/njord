using Njord.Compute.Analysis;
using Njord.Weather;

namespace Njord.Core.Egress;

public interface IEnrichmentEvent
{
    EnrichmentKind Kind { get; }
}

public abstract record PipelineEvent
{
    public sealed record ForecastReceived(
        string Location,
        WeatherModel Model,
        Forecast Forecast) : PipelineEvent;

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
