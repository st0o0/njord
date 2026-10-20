namespace Njord.OpenMeteo;

public interface IWeatherClient
{
    Task<WeatherResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken);
}
