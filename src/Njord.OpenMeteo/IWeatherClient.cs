namespace Njord.OpenMeteo;

public interface IWeatherClient
{
    Task<WeatherResponse?> FetchAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken);
}
