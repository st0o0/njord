using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;

namespace Njord.Tests.Shared;

public sealed class FakeOpenMeteoClient : IOpenMeteoClient
{
    public HashSet<string> FailingModels { get; } = [];

    public Task<FetchOutcome> FetchAsync(
        LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
    {
        if (FailingModels.Contains(model.Id))
        {
            return Task.FromResult<FetchOutcome>(
                new FetchOutcome.Failure(location.Name, model, FetchFailureReason.Transport, "simulated"));
        }

        return Task.FromResult<FetchOutcome>(new FetchOutcome.Success(new ModelForecast(
            model, location.Name, cycle,
            new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3),
                new Dictionary<ParameterDef, double?> { [ParameterRegistry.GetByApiName("temperature_2m")!] = 20.0 })]),
            DailyForecastSeries.Empty)));
    }
}
