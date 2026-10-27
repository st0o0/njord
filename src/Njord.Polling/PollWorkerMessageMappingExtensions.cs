using Njord.Core;
using Njord.Messages.Pipeline;

namespace Njord.Polling;

internal static class PollWorkerMessageMappingExtensions
{
    // Derives the PollStateEntry from the Forecast domain's state for backward compatibility with gRPC.
    public static PollStateEntry ToPollStateEntry(this PollWorkerState state)
    {
        var target = state.Target ?? throw new InvalidOperationException("A poll worker without a target has no poll state");
        var weather = state.GetDomain(DomainKind.Forecast);
        return new PollStateEntry(
            target.Location,
            weather.Phase,
            state.NextPollUtc,
            weather.LastChanged,
            weather.MissCount,
            weather.LearnedCycle is { } cycle ? (long)cycle.TotalSeconds : null);
    }
}
