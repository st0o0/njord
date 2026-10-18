using Njord.Messages.Pipeline;

namespace Njord.Pipeline;

internal static class PollWorkerMessageMappings
{
    public static PollStateEntry ToPollStateEntry(this PollWorkerState state)
    {
        var target = state.Target ?? throw new InvalidOperationException("A poll worker without a target has no poll state");
        return new PollStateEntry(
            target.Location,
            target.ModelId,
            state.Phase,
            state.NextPollUtc,
            state.LastChangeUtc,
            state.MissCount,
            state.Cycle is { } cycle ? (long)cycle.TotalSeconds : null);
    }
}
