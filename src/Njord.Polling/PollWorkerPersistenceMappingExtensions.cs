using System.Collections.Immutable;
using Njord.Core;
using Njord.Messages.Polling;
using Njord.Persistence.Events.Polling;

namespace Njord.Polling;

internal static class PollWorkerPersistenceMappingExtensions
{
    public static PersistedPollWorkerState GetPersistenceState(this PollWorkerState state) => new(
        state.Target?.Location.Name,
        state.Active,
        state.DiscoveryInterval,
        state.Domains.IsEmpty
            ? null
            : [.. state.Domains.Select(kv => new PersistedDomainState(
                (int)kv.Key,
                kv.Value.LastHash,
                kv.Value.Phase.ToPersistence(),
                kv.Value.LearnedCycle,
                kv.Value.MissCount,
                kv.Value.LastChanged,
                kv.Value.CandidateCycle,
                kv.Value.NextPollAt,
                kv.Value.ConsecutiveFailures))],
        state.Target?.Location.Latitude,
        state.Target?.Location.Longitude);

    public static PollWorkerState FromPersistence(this PersistedPollWorkerState dto, IReadOnlyList<DomainKind>? enabledDomains = null)
    {
        var domains = dto.DomainStates is { Count: > 0 }
            ? dto.DomainStates.ToImmutableDictionary(
                d => (DomainKind)d.Domain,
                d => new DomainPollState(
                    (DomainKind)d.Domain,
                    d.Phase.ToDomain(),
                    d.LastHash,
                    d.LastChanged,
                    d.CandidateCycle,
                    d.LearnedCycle,
                    d.MissCount,
                    d.NextPollAt,
                    d.ConsecutiveFailures))
            : ImmutableDictionary<DomainKind, DomainPollState>.Empty;

        // Ensure enabled domains have entries
        if (enabledDomains is { Count: > 0 })
        {
            foreach (var dk in enabledDomains)
            {
                if (!domains.ContainsKey(dk))
                {
                    domains = domains.SetItem(dk, DomainPollState.Empty(dk));
                }
            }
        }

        return new PollWorkerState(
            dto is { Location: { } location } ? new PollTarget(new Location(location, dto.Latitude ?? 0, dto.Longitude ?? 0)) : null,
            dto.Active,
            dto.DiscoveryInterval,
            domains);
    }

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
