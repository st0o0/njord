namespace Njord.Messages.Pipeline;

public sealed record QueryPollHealth;

public sealed record PollHealthResult(DateTimeOffset StartedUtc, DateTimeOffset? LastSuccessfulPollUtc);
