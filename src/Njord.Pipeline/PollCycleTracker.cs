namespace Njord.Pipeline;

// Poll cycle tracking is now per-location (one worker per location). The tracker records the
// duration of each location's poll cycle from the PollManager's perspective. Since there is only
// one worker per location, a cycle completes when the worker reports its outcome.
internal sealed record PollCycleSummary(string Location, TimeSpan Duration);
