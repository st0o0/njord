using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;

namespace Njord.Tests.Ingest;

/// <summary>
/// Real-API smoke test: verifies the assumed response shape and unit
/// parameters against the live endpoint. No API key needed — gated behind
/// <c>NJORD_SMOKE_TESTS=1</c> only because it requires network access.
/// </summary>
public sealed class OpenMeteoSmokeSpec
{
    // Real network call — deliberately above the 5s unit-test timeout.
    [Fact(Timeout = 30000)]
    public async Task A_real_icon_d2_fetch_parses_into_the_domain()
    {
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("NJORD_SMOKE_TESTS") != "1",
            "NJORD_SMOKE_TESTS not set to 1 — smoke test skipped.");

        using var http = new HttpClient { BaseAddress = new Uri("https://api.open-meteo.com/") };
        var client = new OpenMeteoClient(http, TimeProvider.System);

        var outcome = await client.FetchAsync(
            new LocationOptions { Name = "smoke", Latitude = 47.05, Longitude = 8.31 },
            new WeatherModel("icon_d2"),
            new CycleId(DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        Assert.NotEmpty(success.Forecast.Series.Points);
        Assert.NotNull(success.Forecast.Series.Points[0].Temperature);
        Assert.NotNull(success.Forecast.Series.Points[0].ApparentTemperature);
    }
}
