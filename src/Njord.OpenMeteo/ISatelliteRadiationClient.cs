namespace Njord.OpenMeteo;

public interface ISatelliteRadiationClient
{
    Task<SatelliteRadiationResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int pastHours, CancellationToken cancellationToken);
}
