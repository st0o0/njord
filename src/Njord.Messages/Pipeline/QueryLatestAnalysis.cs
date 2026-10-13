using Njord.Weather;

namespace Njord.Messages.Pipeline;

public sealed record QueryLatestAnalysis(LocationId Location);

public abstract record LatestAnalysisResponse;
public sealed record LatestAnalysisResult(LocationId Location, PipelineEvent Analysis) : LatestAnalysisResponse;
public sealed record LatestAnalysisNotAvailable(LocationId Location) : LatestAnalysisResponse;
