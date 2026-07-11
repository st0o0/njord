using Njord.Domain;
using Njord.Ingest;

namespace Njord.Pipeline;

/// <summary>One (location, model) pair a cycle is expected to fetch.</summary>
public sealed record FetchTarget(string Location, WeatherModel Model);

/// <summary>
/// The outcome of one poll cycle: everything that arrived within the
/// aggregation window plus the targets that never answered.
/// </summary>
public sealed record CycleResult(
    CycleId Cycle,
    IReadOnlyList<ModelForecast> Received,
    IReadOnlyList<FetchOutcome.Failure> Failed,
    IReadOnlyList<FetchTarget> Unanswered);
