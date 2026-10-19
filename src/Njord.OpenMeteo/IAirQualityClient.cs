namespace Njord.OpenMeteo;

public interface IAirQualityClient
{
    Task<AirQualityResponse?> FetchAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken);
}
