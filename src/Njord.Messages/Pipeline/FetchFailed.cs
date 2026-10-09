using Njord.Weather;

namespace Njord.Messages.Pipeline;

public sealed record FetchFailed(string Location, string ModelId, FetchFailureReason Reason, string Detail);
