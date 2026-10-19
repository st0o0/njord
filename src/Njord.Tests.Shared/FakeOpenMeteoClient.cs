using Njord.Messages.Forecast;
using Njord.Model;
using Njord.OpenMeteo;

namespace Njord.Tests.Shared;

public sealed class FakeOpenMeteoClient : IOpenMeteoClient
{
    private static readonly DateTimeOffset Start = new(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);

    public bool EnsembleFails { get; set; }

    public bool AirQualityFails { get; set; }

    public bool FloodFails { get; set; }

    public bool Throws { get; set; }

    private volatile TaskCompletionSource _release = CompletedRelease();
    private int _calls;
    private int _airQualityCalls;
    private int _floodCalls;

    public int Calls => Volatile.Read(ref _calls);

    public int AirQualityCalls => Volatile.Read(ref _airQualityCalls);

    public int FloodCalls => Volatile.Read(ref _floodCalls);

    // Holds every fetch that starts from now on until Release is called.
    public void Hold() => _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _release.TrySetResult();

    private static TaskCompletionSource CompletedRelease()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        release.SetResult();
        return release;
    }

    public async Task<OpenMeteoFetchResult> FetchAsync(OpenMeteoRequest request, CancellationToken cancellationToken)
    {
        switch (request.Domain)
        {
            case DomainKind.Weather:
                Interlocked.Increment(ref _calls);
                break;
            case DomainKind.AirQuality:
                Interlocked.Increment(ref _airQualityCalls);
                break;
            case DomainKind.Flood:
                Interlocked.Increment(ref _floodCalls);
                break;
        }

        await _release.Task.WaitAsync(cancellationToken);

        if (Throws)
        {
            throw new InvalidOperationException("simulated fetch fault");
        }

        return request.Domain switch
        {
            DomainKind.Weather when EnsembleFails => Failed("ensemble"),
            DomainKind.AirQuality when AirQualityFails => Failed("air quality"),
            DomainKind.Flood when FloodFails => Failed("flood"),
            DomainKind.Weather => Hourly(
                ("temperature_2m", 20.0), ("apparent_temperature", 18.5), ("wind_speed_10m", 3.2), ("precipitation", 0.0),
                ("precipitation_probability", 10.0), ("cloud_cover", 50.0), ("pressure_msl", 1013.0), ("weather_code", 0.0),
                ("relative_humidity_2m", 65.0), ("dew_point_2m", 12.0), ("uv_index", 3.0)),
            DomainKind.AirQuality => Hourly(("pm2_5", 8.0), ("pm10", 15.0), ("ozone", 60.0)),
            _ => Daily(
                ("river_discharge", 120.0), ("river_discharge_median", 100.0), ("river_discharge_p75", 140.0)),
        };
    }

    private static OpenMeteoFetchResult Failed(string what) =>
        new OpenMeteoFetchResult.Failure(PollFailureReason.Transport, $"simulated {what} failure");

    private static OpenMeteoFetchResult Hourly(params (string Name, double Value)[] values) =>
        new OpenMeteoFetchResult.Success(new OpenMeteoResponse(Series(Start.AddHours(3), values), null));

    private static OpenMeteoFetchResult Daily(params (string Name, double Value)[] values) =>
        new OpenMeteoFetchResult.Success(new OpenMeteoResponse(null, Series(Start, values)));

    private static OpenMeteoSeries Series(DateTimeOffset time, (string Name, double Value)[] values) =>
        new([time.ToUnixTimeSeconds()], values.ToDictionary(v => v.Name, v => new double?[] { v.Value }));
}
