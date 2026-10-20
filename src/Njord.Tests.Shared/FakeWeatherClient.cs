using Njord.OpenMeteo;

namespace Njord.Tests.Shared;

public sealed class FakeWeatherClient : IWeatherClient
{
    private static readonly long StartUnix = new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    public IReadOnlyList<string> LastVariables { get; private set; } = [];

    public int LastForecastDays { get; private set; }

    public bool Fails { get; set; }

    public bool Throws { get; set; }

    private volatile TaskCompletionSource _release = CompletedRelease();
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public void Hold() => _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _release.TrySetResult();

    public async Task<WeatherResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        LastVariables = variables;
        LastForecastDays = forecastDays;
        Interlocked.Increment(ref _calls);
        await _release.Task.WaitAsync(cancellationToken);

        if (Throws)
        {
            throw new InvalidOperationException("simulated weather fetch fault");
        }

        if (Fails)
        {
            return null;
        }

        return new WeatherResponse(latitude, longitude,
            new WeatherHourlyData(
                Time: [StartUnix],
                Temperature: [20.0],
                ApparentTemperature: [18.5],
                WindSpeed: [3.2],
                Precipitation: [0.0],
                PrecipitationProbability: [10.0],
                CloudCover: [50.0],
                Pressure: [1013.0],
                WeatherCode: [0.0],
                Humidity: [65.0],
                DewPoint: [12.0],
                UvIndex: [3.0]));
    }

    private static TaskCompletionSource CompletedRelease()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        release.SetResult();
        return release;
    }
}
