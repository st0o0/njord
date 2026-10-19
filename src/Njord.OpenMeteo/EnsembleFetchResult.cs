using Njord.Messages.Forecast;
using Njord.Model;

namespace Njord.OpenMeteo;

public abstract record EnsembleFetchResult
{
    public sealed record Success(DomainForecast<WeatherPoint> Forecast) : EnsembleFetchResult;

    public sealed record Failure(PollFailureReason Reason, string? Detail) : EnsembleFetchResult;
}
