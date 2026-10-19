using Njord.Model;

namespace Njord.OpenMeteo;

public abstract record AirQualityFetchResult
{
    public sealed record Success(DomainForecast<AirQualityPoint> Forecast) : AirQualityFetchResult;

    public sealed record Failure(PollFailureReason Reason, string? Detail) : AirQualityFetchResult;
}
