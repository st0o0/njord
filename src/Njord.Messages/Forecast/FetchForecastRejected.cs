using Njord.Forecast;
using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record FetchForecastRejected(LocationId Location, string ModelId, FetchRejectionReason Reason)
    : FetchForecastResponse;
