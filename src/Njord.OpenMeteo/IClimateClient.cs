namespace Njord.OpenMeteo;

public interface IClimateClient
{
    Task<ClimateResponse?> FetchAsync(double latitude, double longitude, CancellationToken cancellationToken);
}
