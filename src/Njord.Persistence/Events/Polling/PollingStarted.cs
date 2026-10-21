namespace Njord.Persistence.Events.Polling;

public sealed record PollingStarted(
    string Location,
    int[] Domains,
    TimeSpan DiscoveryInterval,
    DateTimeOffset Utc,
    double? Latitude = null,
    double? Longitude = null);
