namespace Njord.Persistence.Events.Polling;

public sealed record PollDeferred(DateTimeOffset Utc, TimeSpan Delay);
