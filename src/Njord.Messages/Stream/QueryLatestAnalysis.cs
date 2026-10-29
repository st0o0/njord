using Njord.Core;
using Njord.Core.Domain;
using Njord.Messages.Results;

namespace Njord.Messages.Stream;

public sealed record QueryLatestAnalysis(Location Location);

public abstract record LatestAnalysisResponse;

// The latest result per feature of the location (alerts, scores, conditions, trends, forecasts).
public sealed record LatestAnalysisResult(
    Location Location,
    IReadOnlyList<FeatureResult> Results) : LatestAnalysisResponse;

public sealed record LatestAnalysisNotAvailable(Location Location) : LatestAnalysisResponse;
