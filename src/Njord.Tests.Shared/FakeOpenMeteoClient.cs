using System.Collections.Immutable;
using Njord.Core.Configuration;
using Njord.Messages.Forecast;
using Njord.Model;
using Njord.OpenMeteo;

namespace Njord.Tests.Shared;

public sealed class FakeOpenMeteoClient : IOpenMeteoClient
{
    public bool EnsembleFails { get; set; }

    public bool Throws { get; set; }

    private volatile TaskCompletionSource _release = CompletedRelease();
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    // Holds every fetch that starts from now on until Release is called.
    public void Hold() => _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _release.TrySetResult();

    private static TaskCompletionSource CompletedRelease()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        release.SetResult();
        return release;
    }

    public bool AirQualityFails { get; set; }

    public int AirQualityCalls => Volatile.Read(ref _airQualityCalls);

    private int _airQualityCalls;

    public async Task<AirQualityFetchResult> FetchAirQualityAsync(
        double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _airQualityCalls);
        await _release.Task.WaitAsync(cancellationToken);

        if (Throws)
        {
            throw new InvalidOperationException("simulated fetch fault");
        }

        if (AirQualityFails)
        {
            return new AirQualityFetchResult.Failure(PollFailureReason.Transport, "simulated air quality failure");
        }

        var start = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
        var points = ImmutableArray.Create(new AirQualityPoint(start.AddHours(3), 8.0, 15.0, 60.0));
        return new AirQualityFetchResult.Success(
            new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, start, TimeResolution.Hourly, points));
    }

    public bool FloodFails { get; set; }

    public int FloodCalls => Volatile.Read(ref _floodCalls);

    private int _floodCalls;

    public async Task<FloodFetchResult> FetchFloodAsync(
        double latitude, double longitude, int forecastDays, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _floodCalls);
        await _release.Task.WaitAsync(cancellationToken);

        if (Throws)
        {
            throw new InvalidOperationException("simulated fetch fault");
        }

        if (FloodFails)
        {
            return new FloodFetchResult.Failure(PollFailureReason.Transport, "simulated flood failure");
        }

        var start = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
        var points = ImmutableArray.Create(new FloodPoint(start, 120.0, DischargeMedian: 100.0, DischargeP75: 140.0));
        return new FloodFetchResult.Success(
            new DomainForecast<FloodPoint>(DomainKind.Flood, start, TimeResolution.Daily, points));
    }

    public async Task<EnsembleFetchResult> FetchEnsembleAsync(
        LocationOptions location, int forecastDays, DateTimeOffset modelRun, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        await _release.Task.WaitAsync(cancellationToken);

        if (Throws)
        {
            throw new InvalidOperationException("simulated fetch fault");
        }

        if (EnsembleFails)
        {
            return new EnsembleFetchResult.Failure(PollFailureReason.Transport, "simulated ensemble failure");
        }

        var points = ImmutableArray.Create(new WeatherPoint(
            modelRun.AddHours(3), 20.0, 18.5, 3.2, 0.0, 10.0, 50.0, 1013.0, 0, 65.0, 12.0, 3.0));
        var forecast = new DomainForecast<WeatherPoint>(
            DomainKind.Weather, modelRun, TimeResolution.Hourly, points);
        return new EnsembleFetchResult.Success(forecast);
    }
}
