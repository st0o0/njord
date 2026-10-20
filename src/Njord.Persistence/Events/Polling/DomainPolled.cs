namespace Njord.Persistence.Events.Polling;

public sealed record DomainPolled(int Domain, PersistedDomainPollOutcome Outcome, int? Hash, DateTimeOffset Utc, TimeSpan? Delay);
