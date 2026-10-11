using Njord.Weather;

namespace Njord.Core.Ingest;

public interface IOpenMeteoClient
{
    Task<PollOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken);
}
