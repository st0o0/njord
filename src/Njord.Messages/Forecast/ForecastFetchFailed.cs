using Njord.Weather;

namespace Njord.Messages.Forecast;

public sealed record ForecastFetchFailed(string Location, string ModelId, PollFailureReason Reason, string? Detail);
