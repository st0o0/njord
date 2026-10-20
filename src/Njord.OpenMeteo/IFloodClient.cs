namespace Njord.OpenMeteo;

public interface IFloodClient
{
    Task<FloodResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken);
}
