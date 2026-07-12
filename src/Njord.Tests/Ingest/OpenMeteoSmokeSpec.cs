using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;

namespace Njord.Tests.Ingest;

public sealed class OpenMeteoSmokeSpec
{
    private static readonly ResolvedParameterSet DefaultParameters =
        ParameterRegistry.Resolve(["Weather"], [], []);

    [Fact(Timeout = 30000)]
    public async Task A_real_icon_d2_fetch_parses_into_the_domain()
    {
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("NJORD_SMOKE_TESTS") != "1",
            "NJORD_SMOKE_TESTS not set to 1 — smoke test skipped.");

        using var http = new HttpClient { BaseAddress = new Uri("https://api.open-meteo.com/") };
        var client = new OpenMeteoClient(http, DefaultParameters);

        var outcome = await client.FetchAsync(
            new LocationOptions { Name = "smoke", Latitude = 47.05, Longitude = 8.31 },
            new WeatherModel("icon_d2"),
            new CycleId(DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        Assert.NotEmpty(success.Forecast.Hourly.Points);
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        Assert.NotNull(success.Forecast.Hourly.Points[0].Get(temp));
    }
}
