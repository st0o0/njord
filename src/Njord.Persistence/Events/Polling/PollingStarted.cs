namespace Njord.Persistence.Events.Polling;

public sealed record PollingStarted(string Location, string ModelId, TimeSpan DiscoveryInterval, DateTimeOffset Utc);
