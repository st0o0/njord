namespace Njord.OpenMeteo;

public interface IMarineClient
{
    Task<MarineResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken);
}
