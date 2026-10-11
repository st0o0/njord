using Njord.Compute.Analysis;
using Njord.Weather;

namespace Njord.Core.Egress;

public abstract record PipelineEvent
{
    public sealed record ForecastReceived(
        string Location,
        WeatherModel Model,
        Forecast Forecast) : PipelineEvent;

    public sealed record ConsensusComputed(
        ConsensusSnapshot Consensus,
        DateTimeOffset Timestamp) : PipelineEvent;

    public sealed record AlertComputed(
        AlertResult Result,
        DateTimeOffset Timestamp) : PipelineEvent;

    public sealed record DerivedComputed(
        DerivedResult Result,
        DateTimeOffset Timestamp) : PipelineEvent;

    public sealed record TrendComputed(
        TrendResult Result,
        DateTimeOffset Timestamp) : PipelineEvent;

    public sealed record IndexComputed(
        IndexResult Result,
        DateTimeOffset Timestamp) : PipelineEvent;
}
