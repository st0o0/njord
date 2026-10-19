namespace Njord.Persistence.Events.Polling;

public sealed record PollingStarted(string Location, TimeSpan DiscoveryInterval, DateTimeOffset Utc);
