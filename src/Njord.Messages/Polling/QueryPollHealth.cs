namespace Njord.Messages.Polling;

public sealed record QueryPollHealth;

public sealed record PollHealthResult(DateTimeOffset StartedUtc, DateTimeOffset? LastSuccessfulPollUtc);
