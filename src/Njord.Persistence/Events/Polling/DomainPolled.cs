namespace Njord.Persistence.Events.Polling;

// Outcome: Changed=0, Unchanged=1, Failed=2
public sealed record DomainPolled(int Domain, int Outcome, int? Hash, DateTimeOffset Utc, TimeSpan? Delay);
