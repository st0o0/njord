using System.Net;
using System.Text.Json;
using Njord.Core.Configuration;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoClientSpec
{
    private static readonly string[] Vars = ["temperature_2m", "temperature_2m_spread"];

    private static WeatherClient Weather(HttpStatusCode status, string body) =>
        new(StubOpenMeteoHandler.ClientFor("ensemble-api", status, body),
            Microsoft.Extensions.Options.Options.Create(new WeatherOptions()),
            Microsoft.Extensions.Options.Options.Create(new Njord.Core.Configuration.SolarYieldOptions()));

    private static FloodClient Flood(HttpStatusCode status, string body) =>
        new(StubOpenMeteoHandler.ClientFor("flood-api", status, body));

    private static MarineClient Marine(HttpStatusCode status, string body) =>
        new(StubOpenMeteoHandler.ClientFor("marine-api", status, body));

    [Fact(Timeout = 5000)]
    public async Task A_weather_response_is_deserialized_into_typed_hourly_data()
    {
        var client = Weather(HttpStatusCode.OK, OpenMeteoFixtures.WeatherHourly);

        var result = await client.FetchAsync(47.0, 8.0, Vars, 4, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Hourly);
        Assert.NotNull(result.Hourly.Temperature);
        Assert.Multiple(
            () => Assert.Equal([1000L, 1003600L], result.Hourly.Time),
            () => Assert.Equal(20.5, result.Hourly.Temperature[0]),
            () => Assert.Null(result.Hourly.Temperature[1]));
    }

    [Fact(Timeout = 5000)]
    public async Task The_spread_column_is_deserialized_as_ensemble_std_dev()
    {
        var client = Weather(HttpStatusCode.OK, OpenMeteoFixtures.WeatherWithSpread);

        var result = await client.FetchAsync(47.0, 8.0, Vars, 4, TestContext.Current.CancellationToken);

        Assert.NotNull(result?.Hourly?.EnsembleStdDev);
        Assert.Equal(1.7, result.Hourly.EnsembleStdDev[0]);
        var forecast = result.ToDomainForecast(DateTimeOffset.UnixEpoch);
        Assert.NotNull(forecast);
        Assert.Equal(1.7, Assert.Single(forecast.Points).EnsembleStdDev);
    }

    [Fact(Timeout = 5000)]
    public async Task A_flood_response_is_deserialized_into_typed_daily_data()
    {
        var client = Flood(HttpStatusCode.OK, OpenMeteoFixtures.FloodDaily);

        var result = await client.FetchAsync(47.0, 8.0, ["river_discharge"], 92, false, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Daily);
        var discharge = result.Daily.Discharge;
        Assert.NotNull(discharge);
        Assert.Equal(120.0, discharge[0]);
    }

    [Fact(Timeout = 5000)]
    public async Task A_marine_response_is_deserialized_into_typed_hourly_data()
    {
        var client = Marine(HttpStatusCode.OK, OpenMeteoFixtures.MarineHourly);

        var result = await client.FetchAsync(47.0, 8.0, ["wave_height"], 8, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Hourly);
        var hourly = result.Hourly;
        var waveHeight = hourly.WaveHeight;
        var seaTemperature = hourly.SeaSurfaceTemperature;
        var currentVelocity = hourly.OceanCurrentVelocity;
        Assert.NotNull(waveHeight);
        Assert.NotNull(seaTemperature);
        Assert.NotNull(currentVelocity);
        Assert.Multiple(
            () => Assert.Equal([3600L, 7200L], hourly.Time),
            () => Assert.Equal(0.4, waveHeight[0]),
            () => Assert.Null(waveHeight[1]),
            () => Assert.Equal(19.5, seaTemperature[0]),
            () => Assert.Equal(0.2, currentVelocity[0]));
    }

    [Fact(Timeout = 5000)]
    public async Task A_bad_request_returns_null()
    {
        var client = Weather(HttpStatusCode.BadRequest, OpenMeteoFixtures.NoDataReason);

        var result = await client.FetchAsync(47.0, 8.0, Vars, 4, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Theory(Timeout = 5000)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_failing_response_throws_for_the_resilience_pipeline_to_handle(HttpStatusCode status)
    {
        var client = Weather(status, "");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(47.0, 8.0, Vars, 4, TestContext.Current.CancellationToken));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact(Timeout = 5000)]
    public async Task A_malformed_body_throws_a_json_exception()
    {
        var client = Weather(HttpStatusCode.OK, OpenMeteoFixtures.Malformed);

        await Assert.ThrowsAnyAsync<JsonException>(() => client.FetchAsync(47.0, 8.0, Vars, 4, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 5000)]
    public async Task A_cancelled_token_cancels_the_fetch()
    {
        var client = Flood(HttpStatusCode.OK, OpenMeteoFixtures.FloodDaily);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.FetchAsync(47.0, 8.0, ["river_discharge"], 92, false, cts.Token));
    }
}
