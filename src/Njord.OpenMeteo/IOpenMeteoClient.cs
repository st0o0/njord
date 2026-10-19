using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

public interface IOpenMeteoClient
{
    Task<EnsembleFetchResult> FetchEnsembleAsync(LocationOptions location, int forecastDays, DateTimeOffset modelRun, CancellationToken cancellationToken);
}
