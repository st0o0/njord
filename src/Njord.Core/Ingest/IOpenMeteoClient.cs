using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Ingest;

public interface IOpenMeteoClient
{
    Task<FetchOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken);
}
