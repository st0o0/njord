namespace Njord.Persistence.Events.Polling;

public sealed record DomainChanged(int Domain, int Hash, DateTimeOffset Utc);
