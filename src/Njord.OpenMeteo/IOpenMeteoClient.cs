using Njord.Core.Configuration;
using Njord.Forecast;
using Njord.Model;

namespace Njord.OpenMeteo;

public interface IOpenMeteoClient
{
    Task<PollOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken);
}
