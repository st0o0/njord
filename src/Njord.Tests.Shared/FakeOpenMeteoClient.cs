using Njord.OpenMeteo;
using Njord.Weather;

namespace Njord.Tests.Shared;

public sealed class FakeOpenMeteoClient : IOpenMeteoClient
{
    public HashSet<string> FailingModels { get; } = [];

    public HashSet<string> ThrowingModels { get; } = [];

    public Task<PollOutcome> FetchAsync(
        LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
    {
        if (ThrowingModels.Contains(model.Id))
        {
            return Task.FromException<PollOutcome>(
                new InvalidOperationException($"simulated fetch fault for {model.Id}"));
        }

        if (FailingModels.Contains(model.Id))
        {
            return Task.FromResult<PollOutcome>(
                new PollOutcome.Failure(location.Name, model, PollFailureReason.Transport, "simulated"));
        }

        return Task.FromResult<PollOutcome>(new PollOutcome.Success(new Forecast(
            model, location.Name, cycle,
            new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3),
                new Dictionary<ParameterDef, double?> { [ParameterRegistry.GetByApiName("temperature_2m")!] = 20.0 })]),
            DailyForecastSeries.Empty)));
    }
}
