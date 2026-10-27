using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Polling;
using Njord.OpenMeteo.AirQuality;
using Njord.OpenMeteo.Configuration;
using Njord.OpenMeteo.Ensemble;
using Njord.OpenMeteo.Flood;
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Mapping;
using Njord.OpenMeteo.Marine;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);

    private static IOptions<FeatureRequirementsOptions> Needs(
        ForecastParams weather = ForecastParams.None,
        AirQualityParams air = AirQualityParams.None,
        FloodParams flood = FloodParams.None,
        MarineParams marine = MarineParams.None,
        EnsembleParams ensemble = EnsembleParams.None,
        string[]? models = null,
        string[]? ensembleModels = null) =>
        Options.Create(new FeatureRequirementsOptions
        {
            Requirements = new DomainRequirements(Forecast: weather, AirQuality: air, Flood: flood, Marine: marine, Ensemble: ensemble),
            WeatherModels = [.. models ?? [ForecastOptions.DefaultModel]],
            EnsembleModels = [.. ensembleModels ?? [ForecastOptions.DefaultModel]],
        });

    private static ForecastClient Forecast(
        HttpClient http,
        ForecastParams flags = ForecastParams.Temperature,
        SolarYieldOptions? solar = null,
        ForecastOptions? options = null) =>
        new(http, Options.Create(options ?? new ForecastOptions()), Options.Create(solar ?? new SolarYieldOptions()), Needs(weather: flags), NullLogger<ForecastClient>.Instance);

    private static EnsembleClient Ensemble(
        HttpClient http,
        EnsembleParams ensemble = EnsembleParams.TemperatureSpread,
        ForecastParams weather = ForecastParams.None,
        SolarYieldOptions? solar = null,
        ForecastOptions? options = null,
        string[]? models = null) =>
        new(http, Options.Create(options ?? new ForecastOptions()), Options.Create(solar ?? new SolarYieldOptions()), Needs(weather: weather, ensemble: ensemble, ensembleModels: models), NullLogger<EnsembleClient>.Instance);

    private static FloodClient Flood(HttpClient http, FloodOptions? options = null) =>
        new(http, Options.Create(options ?? new FloodOptions()), Needs(flood: FloodParams.Discharge));

    private static MarineClient Marine(HttpClient http) =>
        new(http, Options.Create(new MarineOptions()), Needs(marine: MarineParams.WaveHeight | MarineParams.WaterTemperature));

    private static AirQualityClient Air(HttpClient http) =>
        new(http, Options.Create(new AirQualityOptions()), Needs(air: AirQualityParams.Pm25 | AirQualityParams.Ozone));

    private static async Task<string> Capture(string host, Func<HttpClient, IDomainClient> create, CancellationToken ct)
    {
        string? captured = null;
        var http = StubOpenMeteoHandler.ClientFor(host, HttpStatusCode.OK, "{}", request => captured = StubOpenMeteoHandler.RawPathAndQuery(request));

        await create(http).FetchAsync(47.05, 8.31, Now, ct);

        Assert.NotNull(captured);
        return captured;
    }

    [Fact(Timeout = 5000)]
    public async Task The_forecast_uri_carries_variables_and_days_without_models()
    {
        var uri = await Capture("api", http => Forecast(http, ForecastParams.Rain | ForecastParams.Temperature), TestContext.Current.CancellationToken);

        Assert.Equal(
            $"/v1/forecast?latitude=47.05&longitude=8.31&hourly=temperature_2m,rain&wind_speed_unit=ms&forecast_days={new ForecastOptions().ForecastDays}&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_ensemble_uri_carries_models_and_spread_variables()
    {
        var uri = await Capture("ensemble-api", http => Ensemble(http, EnsembleParams.TemperatureSpread | EnsembleParams.PrecipitationSpread), TestContext.Current.CancellationToken);

        Assert.Equal(
            $"/v1/ensemble?latitude=47.05&longitude=8.31&hourly=temperature_2m_spread,precipitation_spread&models={ForecastOptions.DefaultModel}&wind_speed_unit=ms&forecast_days={new ForecastOptions().ForecastDays}&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_ensemble_uri_lists_all_models_comma_separated()
    {
        string[] models = ["ecmwf_aifs025_ensemble_mean", "ecmwf_ifs025_ensemble_mean", "ukmo_global_ensemble_mean_20km"];

        var uri = await Capture("ensemble-api", http => Ensemble(http, EnsembleParams.TemperatureSpread, models: models), TestContext.Current.CancellationToken);

        Assert.Contains("&models=ecmwf_aifs025_ensemble_mean,ecmwf_ifs025_ensemble_mean,ukmo_global_ensemble_mean_20km&", uri);
    }

    [Fact(Timeout = 5000)]
    public async Task A_multi_model_ensemble_fetch_returns_spread_and_model_forecasts()
    {
        string[] models = ["ecmwf_ifs025_ensemble_mean", "ecmwf_aifs025_ensemble_mean"];
        var http = StubOpenMeteoHandler.ClientFor("ensemble-api", HttpStatusCode.OK, OpenMeteoFixtures.EnsembleMultiModel);

        var result = await Ensemble(http, EnsembleParams.TemperatureSpread | EnsembleParams.Models, ForecastParams.Temperature, models: models).FetchAsync(47.0, 8.0, Now, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchSuccess>(result);
        var primary = Assert.IsType<DomainForecast<EnsemblePoint>>(success.Forecast);
        Assert.Multiple(
            () => Assert.Equal(1.5, primary.Points[0].TemperatureSpread),
            () => Assert.True(success.Models.Count >= 1));
    }

    [Fact]
    public void The_ensemble_call_weight_is_multiplied_by_the_model_count()
    {
        string[] models = ["ecmwf_ifs025_ensemble_mean", "ecmwf_aifs025_ensemble_mean", "ukmo_global_ensemble_mean_20km"];
        var single = Ensemble(new HttpClient());
        var triple = Ensemble(new HttpClient(), models: models);

        Assert.Equal(single.CallWeight * 3, triple.CallWeight);
    }

    [Fact(Timeout = 5000)]
    public async Task The_forecast_uri_carries_tilt_and_azimuth_when_global_tilted_irradiance_is_requested()
    {
        var solar = new SolarYieldOptions { Tilt = 25, Azimuth = 135 };

        var uri = await Capture("api", http => Forecast(http, ForecastParams.GlobalTiltedIrradiance, solar), TestContext.Current.CancellationToken);

        Assert.Equal(
            $"/v1/forecast?latitude=47.05&longitude=8.31&hourly=global_tilted_irradiance&wind_speed_unit=ms&tilt=25&azimuth=-45&forecast_days={new ForecastOptions().ForecastDays}&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_air_quality_uri_carries_location_variables_and_days()
    {
        var uri = await Capture("air-quality-api", http => Air(http), TestContext.Current.CancellationToken);

        Assert.Equal(
            $"/v1/air-quality?latitude=47.05&longitude=8.31&hourly=pm2_5,ozone&forecast_days={new AirQualityOptions().ForecastDays}&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_flood_uri_carries_location_variables_and_days()
    {
        var uri = await Capture("flood-api", http => Flood(http), TestContext.Current.CancellationToken);

        Assert.Equal(
            $"/v1/flood?latitude=47.05&longitude=8.31&daily=river_discharge&forecast_days={new FloodOptions().ForecastDays}&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_flood_uri_carries_the_ensemble_parameter_when_enabled()
    {
        var uri = await Capture("flood-api", http => Flood(http, new FloodOptions { IncludeEnsemble = true }), TestContext.Current.CancellationToken);

        Assert.Equal(
            $"/v1/flood?latitude=47.05&longitude=8.31&daily=river_discharge&forecast_days={new FloodOptions().ForecastDays}&ensemble=true&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_marine_uri_carries_location_variables_and_days()
    {
        var uri = await Capture("marine-api", http => Marine(http), TestContext.Current.CancellationToken);

        Assert.Equal(
            $"/v1/marine?latitude=47.05&longitude=8.31&hourly=wave_height,sea_surface_temperature&forecast_days={new MarineOptions().ForecastDays}&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task Coordinates_use_the_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var uri = await Capture("air-quality-api", http => Air(http), TestContext.Current.CancellationToken);

            Assert.Contains("latitude=47.05&longitude=8.31", uri);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void The_forecast_call_weight_follows_the_resolved_variables_and_days()
    {
        var client = new ForecastClient(
            new HttpClient(), Options.Create(new ForecastOptions { ForecastDays = 4 }), Options.Create(new SolarYieldOptions()), Options.Create(new FeatureRequirementsOptions { Requirements = TestRequirements.All, WeatherModels = [ForecastOptions.DefaultModel] }), NullLogger<ForecastClient>.Instance);

        var variables = TestRequirements.All.Forecast.ToApiNames();

        Assert.Multiple(
            () => Assert.Equal(CallWeight.Of(variables.Count, 4), client.CallWeight),
            () => Assert.True(client.CallWeight > 1));
    }

    [Fact]
    public void The_other_domains_cost_one_call()
    {
        using var http = new HttpClient();

        Assert.Multiple(
            () => Assert.Equal(1, Air(http).CallWeight),
            () => Assert.Equal(1, Flood(http).CallWeight),
            () => Assert.Equal(1, Marine(http).CallWeight));
    }

    [Fact(Timeout = 5000)]
    public async Task A_forecast_response_is_mapped_into_a_weather_domain_forecast()
    {
        var http = StubOpenMeteoHandler.ClientFor("api", HttpStatusCode.OK, OpenMeteoFixtures.ForecastHourly);

        var result = await Forecast(http).FetchAsync(47.0, 8.0, Now, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchSuccess>(result);
        var forecast = Assert.IsType<DomainForecast<ForecastPoint>>(success.Forecast);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Forecast, forecast.Domain),
            () => Assert.Equal(20.5, forecast.Points[0].Temperature));
    }

    [Fact(Timeout = 5000)]
    public async Task An_ensemble_response_is_mapped_into_spread_points()
    {
        var http = StubOpenMeteoHandler.ClientFor("ensemble-api", HttpStatusCode.OK, OpenMeteoFixtures.EnsembleSpread);

        var result = await Ensemble(http, EnsembleParams.TemperatureSpread).FetchAsync(47.0, 8.0, Now, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchSuccess>(result);
        var forecast = Assert.IsType<DomainForecast<EnsemblePoint>>(success.Forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Ensemble, forecast.Domain),
            () => Assert.Equal(1.7, point.TemperatureSpread));
    }

    [Fact(Timeout = 5000)]
    public async Task A_flood_response_is_mapped_into_a_daily_domain_forecast()
    {
        var http = StubOpenMeteoHandler.ClientFor("flood-api", HttpStatusCode.OK, OpenMeteoFixtures.FloodDaily);

        var result = await Flood(http).FetchAsync(47.0, 8.0, Now, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchSuccess>(result);
        var forecast = Assert.IsType<DomainForecast<FloodPoint>>(success.Forecast);
        Assert.Equal(120.0, Assert.Single(forecast.Points).Discharge);
    }

    [Fact(Timeout = 5000)]
    public async Task A_marine_response_is_mapped_into_a_domain_forecast()
    {
        var http = StubOpenMeteoHandler.ClientFor("marine-api", HttpStatusCode.OK, OpenMeteoFixtures.MarineHourly);

        var result = await Marine(http).FetchAsync(47.0, 8.0, Now, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchSuccess>(result);
        var forecast = Assert.IsType<DomainForecast<MarinePoint>>(success.Forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(0.4, point.WaveHeight),
            () => Assert.Equal(19.5, point.WaterTemperature));
    }

    [Fact(Timeout = 5000)]
    public async Task A_bad_request_returns_empty()
    {
        var http = StubOpenMeteoHandler.ClientFor("api", HttpStatusCode.BadRequest, OpenMeteoFixtures.NoDataReason);

        var result = await Forecast(http).FetchAsync(47.0, 8.0, Now, TestContext.Current.CancellationToken);

        Assert.IsType<FetchEmpty>(result);
    }

    [Theory(Timeout = 5000)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_failing_response_returns_a_transport_error(HttpStatusCode status)
    {
        var http = StubOpenMeteoHandler.ClientFor("api", status, "");

        var result = await Forecast(http).FetchAsync(47.0, 8.0, Now, TestContext.Current.CancellationToken);

        var error = Assert.IsType<FetchError>(result);
        Assert.Equal(PollFailureReason.Transport, error.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task A_malformed_body_returns_a_transport_error()
    {
        var http = StubOpenMeteoHandler.ClientFor("api", HttpStatusCode.OK, OpenMeteoFixtures.Malformed);

        var result = await Forecast(http).FetchAsync(47.0, 8.0, Now, TestContext.Current.CancellationToken);

        var error = Assert.IsType<FetchError>(result);
        Assert.Equal(PollFailureReason.Transport, error.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task A_cancelled_token_cancels_the_fetch()
    {
        var http = StubOpenMeteoHandler.ClientFor("flood-api", HttpStatusCode.OK, OpenMeteoFixtures.FloodDaily);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Flood(http).FetchAsync(47.0, 8.0, Now, cts.Token));
    }
}
