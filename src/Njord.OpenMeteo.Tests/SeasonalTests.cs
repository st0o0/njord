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

    private const string WeeklyBody =
        """{"latitude":47.0,"longitude":8.0,"weekly":{"time":[1790812800,1791417600],"temperature_2m_efi":[0.4,-0.1],"precipitation_efi":[-0.2,0.3],"temperature_2m_sot90":[1.5,null],"temperature_2m_anomaly_gt0":[70.0,55.0],"precipitation_anomaly_gt10":[30.0,10.0]}}""";

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
            "/v1/seasonal?latitude=47.05&longitude=8.31&monthly=temperature_2m_anomaly,precipitation_anomaly&models=ecmwf_seasonal_ensemble_mean_seamless&timeformat=unixtime&timezone=UTC",
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

    [Fact(Timeout = 5000)]
    public async Task The_client_sends_the_configured_model()
    {
        string? requested = null;
        var http = StubOpenMeteoHandler.ClientFor("seasonal-api", HttpStatusCode.OK, Body, request => requested = StubOpenMeteoHandler.RawPathAndQuery(request));
        var client = new SeasonalClient(http, Options.Create(new SeasonalOptions { Model = "ecmwf_seas5_ensemble_mean" }),
            Options.Create(new DomainRequirements(Seasonal: SeasonalParams.Temperature)));

        await client.FetchAsync(47, 8, ModelRun, TestContext.Current.CancellationToken);

        Assert.Equal(
            "/v1/seasonal?latitude=47&longitude=8&monthly=temperature_2m&models=ecmwf_seas5_ensemble_mean&timeformat=unixtime&timezone=UTC",
            requested);
    }

    [Fact]
    public void The_new_monthly_variables_are_mapped_to_the_point()
    {
        var response = new SeasonalResponse(47, 8, new SeasonalMonthlyData(
            [1790812800],
            Temperature: [11.5], Snowfall: [3.0], SnowDepth: [0.2], CloudCover: [60.0], WindSpeed: [4.5], Pressure: [1015.0],
            SunshineDuration: [90000.0], ShortwaveRadiation: [110.0], WindGusts: [9.0], SeaSurfaceTemperature: [14.0]));

        var forecast = response.ToDomainForecast(ModelRun, 3);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Equal(
            new SeasonalPoint(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                Temperature: 11.5, Snowfall: 3.0, SnowDepth: 0.2, CloudCover: 60.0, WindSpeed: 4.5, Pressure: 1015.0,
                SunshineDuration: 90000.0, ShortwaveRadiation: 110.0, WindGusts: 9.0, SeaSurfaceTemperature: 14.0),
            point);
    }

    [Fact]
    public void The_monthly_api_names_include_the_new_variables()
    {
        Assert.Equal(
            ["temperature_2m", "snowfall", "snow_depth", "cloud_cover", "wind_speed_10m", "pressure_msl", "sunshine_duration",
                "shortwave_radiation", "wind_gusts_10m", "sea_surface_temperature"],
            (SeasonalParams.Temperature | SeasonalParams.Snowfall | SeasonalParams.SnowDepth | SeasonalParams.CloudCover
                | SeasonalParams.WindSpeed | SeasonalParams.Pressure | SeasonalParams.SunshineDuration
                | SeasonalParams.ShortwaveRadiation | SeasonalParams.WindGusts | SeasonalParams.SeaSurfaceTemperature).ToApiNames());
    }

    [Fact]
    public void A_point_reports_which_requested_fields_are_present()
    {
        var point = new SeasonalPoint(ModelRun, Temperature: 10.0);

        Assert.Multiple(
            () => Assert.True(point.HasFields(SeasonalParams.Temperature)),
            () => Assert.False(point.HasFields(SeasonalParams.Temperature | SeasonalParams.Snowfall)));
    }

    [Fact]
    public void The_weekly_api_names_follow_the_requested_weekly_params()
    {
        Assert.Equal(
            ["temperature_2m_efi", "precipitation_sot90", "temperature_2m_anomaly_ltm2", "precipitation_anomaly_gt20"],
            (SeasonalWeeklyParams.TemperatureEfi | SeasonalWeeklyParams.PrecipitationSot90
                | SeasonalWeeklyParams.TemperatureAnomalyLtM2 | SeasonalWeeklyParams.PrecipitationAnomalyGt20).ToApiNames());
    }

    [Fact(Timeout = 5000)]
    public async Task The_client_requests_the_weekly_variables_in_a_separate_request()
    {
        string? requested = null;
        var http = StubOpenMeteoHandler.ClientFor("seasonal-api", HttpStatusCode.OK, WeeklyBody, request => requested = StubOpenMeteoHandler.RawPathAndQuery(request));
        var client = new SeasonalClient(http, Options.Create(new SeasonalOptions()),
            Options.Create(new DomainRequirements(Seasonal: SeasonalParams.Temperature, SeasonalWeekly: SeasonalWeeklyParams.TemperatureEfi | SeasonalWeeklyParams.PrecipitationEfi)));

        await client.FetchWeeklyAsync(47, 8, ModelRun, TestContext.Current.CancellationToken);

        Assert.Equal(
            "/v1/seasonal?latitude=47&longitude=8&weekly=temperature_2m_efi,precipitation_efi&models=ecmwf_seasonal_ensemble_mean_seamless&timeformat=unixtime&timezone=UTC",
            requested);
    }

    [Fact(Timeout = 5000)]
    public async Task The_client_maps_the_weekly_response_to_weekly_points()
    {
        var http = StubOpenMeteoHandler.ClientFor("seasonal-api", HttpStatusCode.OK, WeeklyBody);
        var client = new SeasonalClient(http, Options.Create(new SeasonalOptions()),
            Options.Create(new DomainRequirements(SeasonalWeekly: SeasonalWeeklyParams.TemperatureEfi)));

        var result = await client.FetchWeeklyAsync(47, 8, ModelRun, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchSuccess>(result);
        var forecast = Assert.IsType<DomainForecast<SeasonalWeeklyPoint>>(success.Forecast);
        Assert.Multiple(
            () => Assert.Equal(TimeResolution.Weekly, forecast.Resolution),
            () => Assert.Equal(2, forecast.Points.Length),
            () => Assert.Equal(
                new SeasonalWeeklyPoint(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), TemperatureEfi: 0.4, PrecipitationEfi: -0.2,
                    TemperatureSot90: 1.5, TemperatureAnomalyGt0: 70.0, PrecipitationAnomalyGt10: 30.0),
                forecast.Points[0]));
    }

    [Fact(Timeout = 5000)]
    public async Task The_client_fetches_only_the_weekly_data_when_no_monthly_variable_is_required()
    {
        string? requested = null;
        var http = StubOpenMeteoHandler.ClientFor("seasonal-api", HttpStatusCode.OK, WeeklyBody, request => requested = StubOpenMeteoHandler.RawPathAndQuery(request));
        var client = new SeasonalClient(http, Options.Create(new SeasonalOptions()),
            Options.Create(new DomainRequirements(SeasonalWeekly: SeasonalWeeklyParams.TemperatureEfi)));

        var result = await client.FetchAsync(47, 8, ModelRun, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Contains("weekly=temperature_2m_efi", requested),
            () => Assert.DoesNotContain("monthly=", requested),
            () => Assert.IsType<FetchSuccess>(result));
    }

    [Fact]
    public void A_weekly_response_without_any_value_maps_to_nothing()
    {
        var response = new SeasonalResponse(47, 8, null, new SeasonalWeeklyData([1790812800]));

        Assert.Null(response.ToWeeklyDomainForecast(ModelRun));
    }

    [Fact]
    public void A_weekly_point_reports_which_requested_fields_are_present()
    {
        var point = new SeasonalWeeklyPoint(ModelRun, TemperatureEfi: 0.3);

        Assert.Multiple(
            () => Assert.True(point.HasFields(SeasonalWeeklyParams.TemperatureEfi)),
            () => Assert.False(point.HasFields(SeasonalWeeklyParams.TemperatureEfi | SeasonalWeeklyParams.PrecipitationEfi)));
    }

    [Fact]
    public void The_api_names_follow_the_requested_seasonal_params()
    {
        Assert.Equal(
            ["temperature_2m_anomaly", "precipitation_mean"],
            (SeasonalParams.TemperatureAnomaly | SeasonalParams.PrecipitationMean).ToApiNames());
    }
}
