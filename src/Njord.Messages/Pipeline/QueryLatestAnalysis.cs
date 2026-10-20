using Njord.Forecast;
using Njord.Messages.Alerts;

namespace Njord.Messages.Pipeline;

public sealed record QueryLatestAnalysis(Location Location);

public abstract record LatestAnalysisResponse;

// The latest alerts of the location across all domains (null until the first alert arrived) and
// the latest lifestyle advisory per kind.
public sealed record LatestAnalysisResult(
    Location Location,
    AlertResult? Alerts,
    IReadOnlyList<LifestyleUpdate>? Lifestyle = null) : LatestAnalysisResponse;

public sealed record LatestAnalysisNotAvailable(Location Location) : LatestAnalysisResponse;
