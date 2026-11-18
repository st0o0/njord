using Njord.Core;
using Njord.Messages.Polling;

namespace Njord.Polling;

internal static class PollWorkerMessageMappingExtensions
{
    public static PollStateEntry ToPollStateEntry(this PollWorkerState state)
    {
        var target = state.Target ?? throw new InvalidOperationException("A poll worker without a target has no poll state");
        var weather = state.GetDomain(DomainKind.Forecast);
        var domainStates = state.Domains.Values
            .Select(d => new DomainPollStateEntry(
                d.Domain.ToString(),
                d.Phase,
                (long)d.EffectiveInterval.TotalSeconds,
                d.NextPollAt))
            .ToList();
        return new PollStateEntry(
            target.Location,
            weather.Phase,
            state.NextPollUtc,
            weather.LastChanged,
            domainStates);
    }
}
