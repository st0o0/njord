namespace Njord.OpenMeteo;

public interface IFloodClient
{
    Task<FloodResponse?> FetchAsync(double latitude, double longitude, CancellationToken cancellationToken);
}
