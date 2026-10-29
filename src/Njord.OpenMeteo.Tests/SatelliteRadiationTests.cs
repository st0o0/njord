using System.Net;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Solar;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Satellite;

namespace Njord.OpenMeteo.Tests;

public sealed class SatelliteRadiationTests
{
    private static readonly DateTimeOffset ModelRun = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    // 2026-10-20 08:00, 09:00, 10:00 UTC (unix seconds); the last hour has no tilted value yet.
    private const string Body =
        """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1792483200,1792486800,1792490400],"shortwave_radiation":[140.0,259.0,340.0],"direct_radiation":[90.0,180.0,250.0],"diffuse_radiation":[50.0,79.0,90.0],"direct_normal_irradiance":[300.0,400.0,null],"global_tilted_irradiance":[211.4,382.0,null]}}""";

    private static SatelliteRadiationClient ClientFor(HttpClient http, SatelliteParams flags, SolarYieldOptions? solar = null) =>
        new(http, Options.Create(new SatelliteOptions()), Options.Create(solar ?? new SolarYieldOptions()),
            Options.Create(new FeatureRequirementsOptions { Requirements = new DomainRequirements(Satellite: flags) }));

    [Fact(Timeout = 5000)]
    public async Task The_client_requests_the_hourly_variables_with_the_panel_orientation()
    {
        string? requested = null;
        var http = StubOpenMeteoHandler.ClientFor("satellite-api", HttpStatusCode.OK, Body, request => requested = StubOpenMeteoHandler.RawPathAndQuery(request));

        await ClientFor(http, SatelliteParams.GlobalHorizontal | SatelliteParams.GlobalTilted, new SolarYieldOptions { Tilt = 25, Azimuth = 135 })
            .FetchAsync(47.05, 8.31, ModelRun, TestContext.Current.CancellationToken);

        Assert.Equal(
            "/v1/archive?latitude=47.05&longitude=8.31&hourly=shortwave_radiation,global_tilted_irradiance&tilt=25&azimuth=-45&past_hours=6&forecast_hours=1&timeformat=unixtime&timezone=UTC",
            requested);
    }

    [Fact(Timeout = 5000)]
    public async Task The_client_returns_empty_for_a_bad_request()
    {
        var http = StubOpenMeteoHandler.ClientFor("satellite-api", HttpStatusCode.BadRequest, OpenMeteoFixtures.NoDataReason);

        var result = await ClientFor(http, SatelliteParams.GlobalTilted).FetchAsync(47, 8, ModelRun, TestContext.Current.CancellationToken);

        Assert.IsType<FetchEmpty>(result);
    }

    [Fact(Timeout = 5000)]
    public async Task The_client_maps_to_hourly_points_and_keeps_missing_values_null()
    {
        var http = StubOpenMeteoHandler.ClientFor("satellite-api", HttpStatusCode.OK, Body);

        var result = await ClientFor(http, SatelliteParams.GlobalTilted).FetchAsync(47, 8, ModelRun, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchSuccess>(result);
        var forecast = Assert.IsType<DomainForecast<SatelliteRadiationPoint>>(success.Forecast);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Satellite, forecast.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(ModelRun, forecast.ModelRun),
            () => Assert.Equal(3, forecast.Points.Length),
            () => Assert.Equal(
                new SatelliteRadiationPoint(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero), 259.0, 180.0, 79.0, 400.0, 382.0),
                forecast.Points[1]));
    }

    [Fact]
    public void A_response_without_hourly_data_maps_to_nothing()
    {
        Assert.Null(new SatelliteRadiationResponse(47, 8, null).ToDomainForecast(ModelRun));
    }

    [Fact]
    public void The_api_names_follow_the_requested_satellite_params()
    {
        Assert.Equal(
            ["shortwave_radiation", "global_tilted_irradiance"],
            (SatelliteParams.GlobalHorizontal | SatelliteParams.GlobalTilted).ToApiNames());
    }

    [Fact]
    public void A_point_has_the_fields_it_carries()
    {
        var point = new SatelliteRadiationPoint(ModelRun, GHI: 100, GTI: null);

        Assert.Multiple(
            () => Assert.True(point.HasFields(SatelliteParams.GlobalHorizontal)),
            () => Assert.False(point.HasFields(SatelliteParams.GlobalHorizontal | SatelliteParams.GlobalTilted)));
    }
}
