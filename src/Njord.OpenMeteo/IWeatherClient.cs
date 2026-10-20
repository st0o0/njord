namespace Njord.OpenMeteo;

public interface IWeatherClient
{
    IReadOnlyList<string> HourlyVariables { get; }

    Task<WeatherResponse?> FetchAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken);
}
