namespace Njord.Messages.Fetch;

public sealed record FetchError(PollFailureReason Reason, string Message) : FetchResult;
