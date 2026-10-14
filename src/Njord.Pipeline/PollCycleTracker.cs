namespace Njord.Pipeline;

internal enum PollCycleOutcome
{
    Changed,
    Unchanged,
    Failed,
}

internal sealed record PollCycleSummary(string Location, int Total, int Changed, int Failed, TimeSpan Duration);

// One open cycle per location: the latest outcome of every resolved model since the cycle opened.
// A failure is final for the cycle (retries are unbounded); a later outcome of the same model replaces it.
internal sealed class PollCycleTracker
{
    private sealed record OpenCycle(DateTimeOffset Start, Dictionary<string, PollCycleOutcome> Outcomes);

    private readonly Dictionary<string, OpenCycle> _open = new(StringComparer.OrdinalIgnoreCase);

    public int OpenCycles => _open.Count;

    public PollCycleSummary? Record(
        string location, string modelId, IReadOnlyCollection<string> resolvedModels, PollCycleOutcome outcome, DateTimeOffset now)
    {
        if (!resolvedModels.Contains(modelId, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!_open.TryGetValue(location, out var cycle))
        {
            cycle = new OpenCycle(now, new Dictionary<string, PollCycleOutcome>(StringComparer.OrdinalIgnoreCase));
            _open[location] = cycle;
        }

        cycle.Outcomes[modelId] = outcome;
        if (!resolvedModels.All(cycle.Outcomes.ContainsKey))
        {
            return null;
        }

        _open.Remove(location);
        return new PollCycleSummary(
            location,
            cycle.Outcomes.Count,
            cycle.Outcomes.Values.Count(o => o == PollCycleOutcome.Changed),
            cycle.Outcomes.Values.Count(o => o == PollCycleOutcome.Failed),
            now - cycle.Start);
    }
}
