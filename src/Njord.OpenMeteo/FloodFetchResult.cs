using Njord.Model;

namespace Njord.OpenMeteo;

public abstract record FloodFetchResult
{
    public sealed record Success(DomainForecast<FloodPoint> Forecast) : FloodFetchResult;

    public sealed record Failure(PollFailureReason Reason, string? Detail) : FloodFetchResult;
}
