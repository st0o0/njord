using System.Net;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Seasonal;
using Njord.Messages.Fetch;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Seasonal;

namespace Njord.OpenMeteo.Tests;

public sealed class SeasonalTests
{
    private static readonly DateTimeOffset ModelRun = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    // 2026-10-01 .. 2027-03-01 (unix seconds)
    private const string Body =
        """{"latitude":47.0,"longitude":8.0,"monthly":{"time":[1790812800,1793491200,1796083200,1798761600,1801440000,1803859200],"temperature_2m_anomaly":[1.7,1.0,1.9,1.4,1.8,1.2],"precipitation_anomaly":[2.4,-20.0,3.5,9.1,23.5,14.3],"precipitation_mean":[99.5,80.0,100.0,98.7,100.0,106.5]}}""";

    private static SeasonalClient ClientFor(HttpClient http, SeasonalParams flags) =>
        new(http, Options.Create(new SeasonalOptions()), Options.Create(new DomainRequirements(Seasonal: flags)));

    private const SeasonalParams All = SeasonalParams.TemperatureAnomaly | SeasonalParams.PrecipitationAnomaly | SeasonalParams.PrecipitationMean;

    [Fact(Timeout = 5000)]
    public async Task The_client_requests_the_monthly_variables()
    {
        string? requested = null;
        var http = StubOpenMeteoHandler.ClientFor("seasonal-api", HttpStatusCode.OK, Body, request => requested = StubOpenMeteoHandler.RawPathAndQuery(request));

        await ClientFor(http, SeasonalParams.TemperatureAnomaly | SeasonalParams.PrecipitationAnomaly)
            .FetchAsync(47.05, 8.31, ModelRun, TestContext.Current.CancellationToken);

        Assert.Equal(
            "/v1/seasonal?latitude=47.05&longitude=8.31&monthly=temperature_2m_anomaly,precipitation_anomaly&timeformat=unixtime&timezone=UTC",
            requested);
    }

    [Fact(Timeout = 5000)]
    public async Task The_client_returns_empty_for_a_bad_request()
    {
        var http = StubOpenMeteoHandler.ClientFor("seasonal-api", HttpStatusCode.BadRequest, OpenMeteoFixtures.NoDataReason);

        var result = await ClientFor(http, SeasonalParams.TemperatureAnomaly).FetchAsync(47, 8, ModelRun, TestContext.Current.CancellationToken);

        Assert.IsType<FetchEmpty>(result);
    }

    [Fact(Timeout = 5000)]
    public async Task The_client_maps_to_monthly_points_limited_to_the_current_month_plus_the_forecast_months()
    {
        var http = StubOpenMeteoHandler.ClientFor("seasonal-api", HttpStatusCode.OK, Body);

        var result = await ClientFor(http, All).FetchAsync(47, 8, ModelRun, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchSuccess>(result);
        var forecast = Assert.IsType<DomainForecast<SeasonalPoint>>(success.Forecast);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Seasonal, forecast.Domain),
            () => Assert.Equal(TimeResolution.Monthly, forecast.Resolution),
            () => Assert.Equal(ModelRun, forecast.ModelRun),
            () => Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), forecast.FirstValid),
            () => Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), forecast.LastValid),
            () => Assert.Equal(new SeasonalPoint(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), 1.0, -20.0, 80.0), forecast.Points[1]));
    }

    [Fact]
    public void A_response_without_any_anomaly_maps_to_nothing()
    {
        var response = new SeasonalResponse(47, 8, new SeasonalMonthlyData([1790812800], null, null, [99.5]));

        Assert.Null(response.ToDomainForecast(ModelRun, 3));
    }

    [Fact]
    public void The_api_names_follow_the_requested_seasonal_params()
    {
        Assert.Equal(
            ["temperature_2m_anomaly", "precipitation_mean"],
            (SeasonalParams.TemperatureAnomaly | SeasonalParams.PrecipitationMean).ToApiNames());
    }
}
