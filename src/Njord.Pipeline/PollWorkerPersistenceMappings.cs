using System.Collections.Immutable;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;

namespace Njord.Pipeline;

internal static class PollWorkerPersistenceMappings
{
    public static PersistedPollWorkerState GetPersistenceState(this PollWorkerState state) => new(
        state.Target?.Location.Name,
        state.Active,
        state.DiscoveryInterval,
        state.LastHash,
        state.LastChangeUtc,
        state.PrevChangeUtc,
        state.NextPollUtc,
        state.MissCount,
        state.Phase.ToPersistence(),
        state.Cycle,
        state.TransientFailureCount,
        state.DomainStates.IsEmpty
            ? null
            : [.. state.DomainStates.Select(kv => new PersistedDomainState(
                (int)kv.Key,
                kv.Value.LastHash,
                kv.Value.Phase.ToPersistence(),
                kv.Value.LearnedCycle,
                kv.Value.MissCount,
                kv.Value.LastChanged,
                kv.Value.CandidateCycle,
                kv.Value.NextPollAt))]);

    public static PollWorkerState FromPersistence(this PersistedPollWorkerState dto) => new(
        dto is { Location: { } location } ? new PollTarget(new Location(location, 0, 0)) : null,
        dto.Active,
        dto.DiscoveryInterval,
        dto.LastHash,
        dto.LastChangeUtc,
        dto.PrevChangeUtc,
        dto.NextPollUtc,
        dto.MissCount,
        dto.Phase.ToDomain(),
        dto.Cycle,
        dto.TransientFailureCount,
        dto.DomainStates is { Count: > 0 }
            ? dto.DomainStates.ToImmutableDictionary(
                d => (DomainKind)d.Domain,
                d => new DomainPollState(
                    d.LastHash,
                    d.LastChanged,
                    d.CandidateCycle,
                    d.LearnedCycle,
                    d.Phase.ToDomain(),
                    d.MissCount,
                    d.NextPollAt ?? DateTimeOffset.MinValue))
            : ImmutableDictionary<DomainKind, DomainPollState>.Empty);

    public static PersistedPollPhase ToPersistence(this PollPhase phase) => phase switch
    {
        PollPhase.Discovery => PersistedPollPhase.Discovery,
        PollPhase.Steady => PersistedPollPhase.Steady,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown poll phase"),
    };

    public static PollPhase ToDomain(this PersistedPollPhase phase) => phase switch
    {
        PersistedPollPhase.Discovery => PollPhase.Discovery,
        PersistedPollPhase.Steady => PollPhase.Steady,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown persisted poll phase"),
    };
}
