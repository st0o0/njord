using System.Globalization;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoUriBuilderSpec
{
    private const string EmptyBody = "{}";

    [Fact(Timeout = 5000)]
    public async Task The_air_quality_uri_carries_location_variables_and_days()
    {
        var uri = await Capture(client => new AirQualityClient(client).FetchAsync(47.05, 8.31, ["pm2_5", "ozone"], 4, TestContext.Current.CancellationToken));

        Assert.Equal(
            $"/v1/air-quality?latitude=47.05&longitude=8.31&hourly=pm2_5,ozone&forecast_days=4&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_flood_uri_carries_location_variables_and_days()
    {
        var uri = await Capture(client => new FloodClient(client).FetchAsync(47.05, 8.31, ["river_discharge", "river_discharge_max"], 92, false, TestContext.Current.CancellationToken));

        Assert.Equal(
            $"/v1/flood?latitude=47.05&longitude=8.31&daily=river_discharge,river_discharge_max&forecast_days=92&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_flood_uri_carries_the_ensemble_parameter_when_requested()
    {
        var uri = await Capture(client => new FloodClient(client).FetchAsync(47.05, 8.31, ["river_discharge"], 92, true, TestContext.Current.CancellationToken));

        Assert.Equal(
            "/v1/flood?latitude=47.05&longitude=8.31&daily=river_discharge&forecast_days=92&ensemble=true&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_marine_uri_carries_location_variables_and_days()
    {
        var uri = await Capture(client => new MarineClient(client).FetchAsync(47.05, 8.31, ["wave_height"], 8, TestContext.Current.CancellationToken));

        Assert.Equal(
            $"/v1/marine?latitude=47.05&longitude=8.31&hourly=wave_height&forecast_days=8&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_weather_uri_carries_the_ensemble_model_and_the_given_variables_and_days()
    {
        var uri = await Capture(client => new WeatherClient(client, Microsoft.Extensions.Options.Options.Create(new WeatherOptions()), Microsoft.Extensions.Options.Options.Create(new SolarYieldOptions()))
            .FetchAsync(47.05, 8.31, ["shortwave_radiation", "rain"], 7, TestContext.Current.CancellationToken));

        Assert.Equal(
            "/v1/ensemble?latitude=47.05&longitude=8.31&hourly=shortwave_radiation,rain&models=ecmwf_ifs025_ensemble_mean&wind_speed_unit=ms&forecast_days=7&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_weather_uri_carries_tilt_and_azimuth_when_global_tilted_irradiance_is_requested()
    {
        var solar = new SolarYieldOptions { Tilt = 25, Azimuth = 135 };
        var uri = await Capture(client => new WeatherClient(client, Microsoft.Extensions.Options.Options.Create(new WeatherOptions()), Microsoft.Extensions.Options.Options.Create(solar))
            .FetchAsync(47.05, 8.31, ["global_tilted_irradiance"], 7, TestContext.Current.CancellationToken));

        Assert.Equal(
            "/v1/ensemble?latitude=47.05&longitude=8.31&hourly=global_tilted_irradiance&models=ecmwf_ifs025_ensemble_mean&wind_speed_unit=ms&tilt=25&azimuth=-45&forecast_days=7&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task Coordinates_use_the_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var uri = await Capture(client => new AirQualityClient(client).FetchAsync(47.05, 8.31, ["pm2_5", "ozone"], 4, TestContext.Current.CancellationToken));

            Assert.Contains("latitude=47.05&longitude=8.31", uri);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void The_builder_appends_time_parameters_to_the_endpoint()
    {
        var forecast = OpenMeteoRequestBuilder.To("v1/x").WithLocation(1.5, 2.5).Build();

        Assert.Equal("v1/x?latitude=1.5&longitude=2.5&timeformat=unixtime&timezone=UTC", forecast);
    }

    private static async Task<string> Capture(Func<HttpClient, Task> call)
    {
        string? captured = null;
        var handler = StubOpenMeteoHandler.Json(System.Net.HttpStatusCode.OK, EmptyBody, request => captured = request.RequestUri?.PathAndQuery);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        await call(client);

        return Assert.IsType<string>(captured);
    }
}
