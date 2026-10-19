using Njord.Messages.Forecast;
using Njord.Model;

namespace Njord.OpenMeteo;

public abstract record FloodFetchResult : IFetchOutcome
{
    IDomainForecast? IFetchOutcome.Forecast => (this as Success)?.Forecast;

    PollFailureReason IFetchOutcome.Reason => (this as Failure)?.Reason ?? default;

    string? IFetchOutcome.Detail => (this as Failure)?.Detail;

    public sealed record Success(DomainForecast<FloodPoint> Forecast) : FloodFetchResult;

    public sealed record Failure(PollFailureReason Reason, string? Detail) : FloodFetchResult;
}
