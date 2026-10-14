using Njord.Weather;

namespace Njord.Messages.Pipeline;

public sealed record QueryLatestAnalysis(LocationId Location);

public abstract record LatestAnalysisResponse;

// The latest event of each analysis kind for the location; a kind that has not been computed yet is null.
public sealed record LatestAnalysisResult(
    LocationId Location,
    PipelineEvent.ConsensusComputed? Consensus,
    PipelineEvent.AlertComputed? Alerts,
    PipelineEvent.DerivedComputed? Derived,
    PipelineEvent.TrendComputed? Trends,
    PipelineEvent.IndexComputed? Indices) : LatestAnalysisResponse;

public sealed record LatestAnalysisNotAvailable(LocationId Location) : LatestAnalysisResponse;
