namespace Njord.OpenMeteo;

public interface IAirQualityClient
{
    Task<AirQualityResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken);
}
