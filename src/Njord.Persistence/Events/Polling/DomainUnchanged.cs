namespace Njord.Persistence.Events.Polling;

public sealed record DomainUnchanged(int Domain, DateTimeOffset Utc);
