using System.Collections.Immutable;

namespace Njord.Pipeline;

internal enum PollCycleOutcome
{
    Changed,
    Unchanged,
    Failed,
}

internal sealed record PollCycleSummary(string Location, int Total, int Changed, int Failed, TimeSpan Duration);

internal sealed record OpenCycle(DateTimeOffset Start, ImmutableDictionary<string, PollCycleOutcome> Outcomes)
{
    public static readonly OpenCycle Empty = new(default, ImmutableDictionary<string, PollCycleOutcome>.Empty);
}

internal sealed record PollCycleTracker(ImmutableDictionary<string, OpenCycle> Open)
{
    public static readonly PollCycleTracker Empty =
        new(ImmutableDictionary.Create<string, OpenCycle>(StringComparer.OrdinalIgnoreCase));

    public int OpenCycles => Open.Count;

    public (PollCycleTracker Tracker, PollCycleSummary? Summary) Record(
        string location, string modelId, IReadOnlyCollection<string> resolvedModels,
        PollCycleOutcome outcome, DateTimeOffset now)
    {
        if (!resolvedModels.Contains(modelId, StringComparer.OrdinalIgnoreCase))
        {
            return (this, null);
        }

        var cycle = Open.TryGetValue(location, out var existing)
            ? existing
            : new OpenCycle(now, ImmutableDictionary.Create<string, PollCycleOutcome>(StringComparer.OrdinalIgnoreCase));

        var outcomes = cycle.Outcomes.SetItem(modelId, outcome);
        if (!resolvedModels.All(outcomes.ContainsKey))
        {
            return (this with { Open = Open.SetItem(location, cycle with { Outcomes = outcomes }) }, null);
        }

        var summary = new PollCycleSummary(
            location,
            outcomes.Count,
            outcomes.Values.Count(o => o == PollCycleOutcome.Changed),
            outcomes.Values.Count(o => o == PollCycleOutcome.Failed),
            now - cycle.Start);
        return (this with { Open = Open.Remove(location) }, summary);
    }
}
