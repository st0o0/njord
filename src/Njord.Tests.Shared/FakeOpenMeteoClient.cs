using Njord.Core.Configuration;
using Njord.Forecast;
using Njord.Model;
using Njord.OpenMeteo;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Tests.Shared;

public sealed class FakeOpenMeteoClient : IOpenMeteoClient
{
    public HashSet<string> FailingModels { get; } = [];

    public HashSet<string> ThrowingModels { get; } = [];

    private volatile TaskCompletionSource _release = CompletedRelease();
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    // Holds every fetch that starts from now on until Release is called.
    public void Hold() => _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _release.TrySetResult();

    public async Task<PollOutcome> FetchAsync(
        LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        await _release.Task.WaitAsync(cancellationToken);
        return await FetchNowAsync(location, model, cycle);
    }

    private static TaskCompletionSource CompletedRelease()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        release.SetResult();
        return release;
    }

    private Task<PollOutcome> FetchNowAsync(LocationOptions location, WeatherModel model, CycleId cycle)
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

        return Task.FromResult<PollOutcome>(new PollOutcome.Success(new WeatherForecast(
            model, location.Name, cycle,
            new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3),
                new Dictionary<ParameterDef, ForecastValue> { [ParameterRegistry.GetByApiName("temperature_2m")!] = 20.0 })]),
            DailyForecastSeries.Empty)));
    }
}
