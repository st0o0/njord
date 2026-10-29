using Njord.Core;
using Njord.Core.Forecast;
using Njord.Messages.Fetch;

namespace Njord.Tests.Shared;

public sealed class FakeForecastClient : IDomainClient
{
    public DomainKind Domain => DomainKind.Forecast;

    public int CallWeight { get; set; } = 1;

    public bool Fails { get; set; }

    public bool Throws { get; set; }

    private volatile TaskCompletionSource _release = CompletedRelease();
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public void Hold() => _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _release.TrySetResult();

    public async Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        await _release.Task.WaitAsync(cancellationToken);

        if (Throws)
        {
            throw new InvalidOperationException("simulated forecast fetch fault");
        }

        if (Fails)
        {
            return new FetchEmpty();
        }

        var point = new ForecastPoint(
            new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero),
            Temperature: 20.0,
            ApparentTemperature: 18.5,
            WindSpeed: 3.2,
            Precipitation: 0.0,
            PrecipitationProbability: 10.0,
            CloudCover: 50.0,
            Pressure: 1013.0,
            WeatherCode: 0.0,
            Humidity: 65.0,
            DewPoint: 12.0,
            UvIndex: 3.0);

        var forecast = new DomainForecast<ForecastPoint>(DomainKind.Forecast, now, TimeResolution.Hourly, [point]);
        return new FetchSuccess(forecast);
    }

    private static TaskCompletionSource CompletedRelease()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        release.SetResult();
        return release;
    }
}
