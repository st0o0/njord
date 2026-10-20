namespace Njord.OpenMeteo;

public interface IMarineClient
{
    Task<MarineResponse?> FetchAsync(double latitude, double longitude, CancellationToken cancellationToken);
}
