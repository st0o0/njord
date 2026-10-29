namespace Njord.Core.Domain;

public sealed record FetchError(PollFailureReason Reason, string Message) : FetchResult;
