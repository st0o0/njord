using Njord.Weather;

namespace Njord.Messages.Pipeline;

public sealed record FetchFailed(string Location, string ModelId, PollFailureReason Reason, string Detail);
