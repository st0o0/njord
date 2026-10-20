using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Pipeline;

internal static class PollWorkerMessageMappings
{
    // Derives the PollStateEntry from the Weather domain's state for backward compatibility with gRPC/MQTT.
    public static PollStateEntry ToPollStateEntry(this PollWorkerState state)
    {
        var target = state.Target ?? throw new InvalidOperationException("A poll worker without a target has no poll state");
        var weather = state.GetDomain(DomainKind.Weather);
        return new PollStateEntry(
            target.Location,
            weather.Phase,
            state.NextPollUtc,
            weather.LastChanged,
            weather.MissCount,
            weather.LearnedCycle is { } cycle ? (long)cycle.TotalSeconds : null);
    }
}
