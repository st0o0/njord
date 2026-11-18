namespace Njord.Persistence.Events.Polling;

public sealed record PollingStarted(
    string Location,
    int[] Domains,
    Dictionary<int, TimeSpan> DomainIntervals,
    DateTimeOffset Utc,
    double? Latitude = null,
    double? Longitude = null);
