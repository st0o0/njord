using Njord.Weather;

namespace Njord.Messages.Forecast;

public sealed record FetchForecastFailed(string Location, string ModelId, PollFailureReason Reason, string? Detail)
    : FetchForecastResponse;
