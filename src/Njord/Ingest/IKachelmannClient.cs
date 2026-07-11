using Njord.Configuration;
using Njord.Domain;

namespace Njord.Ingest;

public interface IKachelmannClient
{
    Task<FetchOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken);
}
