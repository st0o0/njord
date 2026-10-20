using System.Collections.Immutable;

namespace Njord.Persistence.Events.Polling;

public sealed record RoundCompleted(ImmutableArray<DomainPollResult> Results, DateTimeOffset Utc);

public sealed record DomainPollResult(int Domain, PersistedDomainPollOutcome Outcome, int? Hash, TimeSpan? Delay);
