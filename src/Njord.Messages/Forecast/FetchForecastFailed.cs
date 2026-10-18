using Njord.Forecast;
using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record FetchForecastFailed(LocationId Location, string ModelId, PollFailureReason Reason, string? Detail)
    : FetchForecastResponse;
