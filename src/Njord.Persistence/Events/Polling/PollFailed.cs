namespace Njord.Persistence.Events.Polling;

public sealed record PollFailed(DateTimeOffset Utc, TimeSpan MinimumDelay);
