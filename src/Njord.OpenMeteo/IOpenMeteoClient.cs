using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

public interface IOpenMeteoClient
{
    Task<EnsembleFetchResult> FetchEnsembleAsync(LocationOptions location, int forecastDays, DateTimeOffset modelRun, CancellationToken cancellationToken);

    Task<AirQualityFetchResult> FetchAirQualityAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken);

    Task<FloodFetchResult> FetchFloodAsync(double latitude, double longitude, int forecastDays, CancellationToken cancellationToken);
}
