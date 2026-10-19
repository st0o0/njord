using System.Collections.Immutable;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Model;
using Njord.OpenMeteo;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Tests.Shared;

public sealed class FakeOpenMeteoClient : IOpenMeteoClient
{
    public HashSet<string> FailingModels { get; } = [];

    public HashSet<string> ThrowingModels { get; } = [];

    public bool EnsembleFails { get; set; }

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

    public Task<EnsembleFetchResult> FetchEnsembleAsync(
        LocationOptions location, int forecastDays, CycleId cycle, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);

        if (EnsembleFails)
        {
            return Task.FromResult<EnsembleFetchResult>(
                new EnsembleFetchResult.Failure(PollFailureReason.Transport, "simulated ensemble failure"));
        }

        var points = ImmutableArray.Create(new WeatherPoint(
            cycle.Timestamp.AddHours(3), 20.0, 18.5, 3.2, 0.0, 10.0, 50.0, 1013.0, 0, 65.0, 12.0, 3.0));
        var forecast = new DomainForecast<WeatherPoint>(
            DomainKind.Weather, cycle.Timestamp, TimeResolution.Hourly, points);
        return Task.FromResult<EnsembleFetchResult>(new EnsembleFetchResult.Success(forecast));
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
