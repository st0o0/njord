using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;

namespace Njord.Tests.Ingest;

/// <summary>
/// Real-API smoke test: verifies the assumed response field names and the
/// units parameter against the live endpoint. Skipped unless the runtime
/// environment provides <c>Njord__ApiKey</c> (never stored in the repo).
/// </summary>
public sealed class KachelmannSmokeSpec
{
    // Real network call — deliberately above the 5s unit-test timeout.
    [Fact(Timeout = 30000)]
    public async Task A_real_icon_d2_fetch_parses_into_the_domain()
    {
        var apiKey = Environment.GetEnvironmentVariable("Njord__ApiKey");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(apiKey), "Njord__ApiKey not set — smoke test skipped.");

        using var http = new HttpClient { BaseAddress = new Uri("https://api.kachelmannwetter.com/v02/") };
        http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        var client = new KachelmannClient(http, TimeProvider.System);

        var outcome = await client.FetchAsync(
            new LocationOptions { Name = "smoke", Latitude = 47.05, Longitude = 8.31 },
            new WeatherModel("ICON-D2"),
            new CycleId(DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        Assert.NotEmpty(success.Forecast.Series.Points);
        Assert.NotNull(success.Forecast.Series.Points[0].Temperature);
    }
}
