using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using Njord.Weather;

namespace Njord.Pipeline;

internal static class PollWorkerPersistenceMappings
{
    public static PersistedPollWorkerState GetPersistenceState(this PollWorkerState state) => new(
        state.Target?.Location.Name,
        state.Target?.ModelId,
        state.Active,
        state.DiscoveryInterval,
        state.LastHash,
        state.LastChangeUtc,
        state.PrevChangeUtc,
        state.NextPollUtc,
        state.MissCount,
        state.Phase.ToPersistence(),
        state.Cycle,
        state.TransientFailureCount);

    public static PollWorkerState FromPersistence(this PersistedPollWorkerState dto) => new(
        dto is { Location: { } location, ModelId: { } model } ? new PollTarget(new LocationId(location), model) : null,
        dto.Active,
        dto.DiscoveryInterval,
        dto.LastHash,
        dto.LastChangeUtc,
        dto.PrevChangeUtc,
        dto.NextPollUtc,
        dto.MissCount,
        dto.Phase.ToDomain(),
        dto.Cycle,
        dto.TransientFailureCount);

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
