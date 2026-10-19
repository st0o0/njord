using Njord.Model;

namespace Njord.Messages.Pipeline;

public sealed record QueryLatestAnalysis(Location Location);

public abstract record LatestAnalysisResponse;

// The latest alerts of the location across all domains; null until the first alert arrived.
public sealed record LatestAnalysisResult(
    Location Location,
    PipelineEvent.AlertComputed? Alerts) : LatestAnalysisResponse;

public sealed record LatestAnalysisNotAvailable(Location Location) : LatestAnalysisResponse;
