using Njord.Weather;

namespace Njord.OpenMeteo;

public interface IOpenMeteoClient
{
    Task<PollOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken);
}
