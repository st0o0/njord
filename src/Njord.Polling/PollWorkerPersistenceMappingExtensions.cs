using System.Collections.Immutable;
using Njord.Core;
using Njord.Core.Polling;
using Njord.Messages.Polling;
using Njord.Persistence.Events.Polling;

namespace Njord.Polling;

internal static class PollWorkerPersistenceMappingExtensions
{
    public static PersistedPollWorkerState GetPersistenceState(this PollWorkerState state) => new(
        state.Target?.Location.Name,
        state.Active,
        state.Domains.IsEmpty
            ? null
            : [.. state.Domains.Select(kv => new PersistedDomainState(
                (int)kv.Key,
                kv.Value.LastHash,
                kv.Value.Phase.ToPersistence(),
                kv.Value.LastChanged,
                kv.Value.NextPollAt,
                kv.Value.ConsecutiveFailures,
                kv.Value.PollInterval))],
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
                    d.PollInterval ?? DomainDefaults.PollInterval((DomainKind)d.Domain),
                    d.NextPollAt,
                    d.ConsecutiveFailures))
            : [];

        if (enabledDomains is { Count: > 0 })
        {
            foreach (var dk in enabledDomains)
            {
                if (!domains.ContainsKey(dk))
                {
                    domains = domains.SetItem(dk, DomainPollState.Empty(dk, DomainDefaults.PollInterval(dk)));
                }
            }
        }

        return new PollWorkerState(
            dto is { Location: { } location } ? new PollTarget(new Location(location, dto.Latitude ?? 0, dto.Longitude ?? 0)) : null,
            dto.Active,
            domains);
    }

    public static PersistedPollPhase ToPersistence(this PollPhase phase) => phase switch
    {
        PollPhase.Active => PersistedPollPhase.Active,
        PollPhase.Suspended => PersistedPollPhase.Suspended,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown poll phase"),
    };

    public static PollPhase ToDomain(this PersistedPollPhase phase) => phase switch
    {
        PersistedPollPhase.Active => PollPhase.Active,
        (PersistedPollPhase)1 => PollPhase.Active, // legacy Steady maps to Active
        PersistedPollPhase.Suspended => PollPhase.Suspended,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown persisted poll phase"),
    };
}
