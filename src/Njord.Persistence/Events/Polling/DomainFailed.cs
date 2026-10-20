namespace Njord.Persistence.Events.Polling;

public sealed record DomainFailed(int Domain, DateTimeOffset Utc, TimeSpan Delay);
