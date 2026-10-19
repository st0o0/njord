using Njord.Messages.Forecast;
using Njord.Model;

namespace Njord.OpenMeteo;

public abstract record EnsembleFetchResult : IFetchOutcome
{
    IDomainForecast? IFetchOutcome.Forecast => (this as Success)?.Forecast;

    PollFailureReason IFetchOutcome.Reason => (this as Failure)?.Reason ?? default;

    string? IFetchOutcome.Detail => (this as Failure)?.Detail;

    public sealed record Success(DomainForecast<WeatherPoint> Forecast) : EnsembleFetchResult;

    public sealed record Failure(PollFailureReason Reason, string? Detail) : EnsembleFetchResult;
}
