using System.Net;
using System.Text;
using Njord.Core.Configuration;
using Njord.Model;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoClientSpec
{
    private static HttpClient StubHttp(HttpStatusCode status, string body, Action<HttpRequestMessage>? inspect = null) =>
        new(new StubHandler(status, body, inspect)) { BaseAddress = new Uri("https://api.open-meteo.com/") };

    private static WeatherClient Weather(HttpStatusCode status, string body, Action<HttpRequestMessage>? inspect = null) =>
        new(StubHttp(status, body, inspect), ParameterSelection.Resolve(["Weather"], [], []));

    [Fact(Timeout = 5000)]
    public async Task A_weather_response_is_deserialized_into_typed_hourly_data()
    {
        var client = Weather(HttpStatusCode.OK,
            """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000,1003600],"temperature_2m":[20.5,null],"apparent_temperature":[18.0,null],"wind_speed_10m":[3.0,null],"precipitation":[0.0,null],"precipitation_probability":[10.0,null],"cloud_cover":[50.0,null],"pressure_msl":[1013.0,null],"weather_code":[3,null],"relative_humidity_2m":[65.0,null],"dew_point_2m":[12.0,null],"uv_index":[3.0,null]}}""");

        var result = await client.FetchAsync(47.0, 8.0, 4, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Hourly);
        Assert.Multiple(
            () => Assert.Equal([1000L, 1003600L], result.Hourly.Time),
            () => Assert.Equal(20.5, result.Hourly.Temperature[0]),
            () => Assert.Null(result.Hourly.Temperature[1]));
    }

    [Fact(Timeout = 5000)]
    public async Task A_flood_response_is_deserialized_into_typed_daily_data()
    {
        var client = new FloodClient(new HttpClient(new StubHandler(HttpStatusCode.OK,
            """{"latitude":47.0,"longitude":8.0,"daily":{"time":[86400],"river_discharge":[120.0]}}""", null))
        { BaseAddress = new Uri("https://flood-api.open-meteo.com/") });

        var result = await client.FetchAsync(47.0, 8.0, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Daily);
        Assert.Equal(120.0, result.Daily.Discharge[0]);
    }

    [Fact(Timeout = 5000)]
    public async Task A_bad_request_returns_null()
    {
        var client = Weather(HttpStatusCode.BadRequest,
            """{"error":true,"reason":"No data is available for this location"}""");

        var result = await client.FetchAsync(47.0, 8.0, 4, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact(Timeout = 5000)]
    public async Task A_rate_limited_response_returns_null()
    {
        var client = Weather(HttpStatusCode.TooManyRequests, "");

        var result = await client.FetchAsync(47.0, 8.0, 4, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact(Timeout = 5000)]
    public async Task The_weather_request_uri_carries_the_ensemble_path_and_query_parameters()
    {
        Uri? sent = null;
        var client = Weather(HttpStatusCode.OK,
            """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[],"temperature_2m":[],"apparent_temperature":[],"wind_speed_10m":[],"precipitation":[],"precipitation_probability":[],"cloud_cover":[],"pressure_msl":[],"weather_code":[],"relative_humidity_2m":[],"dew_point_2m":[],"uv_index":[]}}""",
            r => sent = r.RequestUri);

        await client.FetchAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken);

        Assert.NotNull(sent);
        Assert.Contains("v1/ensemble?", sent.ToString());
        Assert.Contains("latitude=47.05", sent.ToString());
        Assert.Contains("longitude=8.31", sent.ToString());
        Assert.Contains("wind_speed_unit=ms", sent.ToString());
        Assert.Contains("timeformat=unixtime", sent.ToString());
        Assert.Contains("timezone=UTC", sent.ToString());
        Assert.Contains("forecast_days=4", sent.ToString());
    }

    [Fact(Timeout = 5000)]
    public async Task The_air_quality_request_uri_carries_the_air_quality_path()
    {
        Uri? sent = null;
        var client = new AirQualityClient(new HttpClient(new StubHandler(HttpStatusCode.OK,
            """{"latitude":47.0,"longitude":8.0,"hourly":{"time":[],"pm2_5":[],"pm10":[],"ozone":[]}}""",
            r => sent = r.RequestUri))
        { BaseAddress = new Uri("https://air-quality-api.open-meteo.com/") });

        await client.FetchAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken);

        Assert.NotNull(sent);
        Assert.Contains("v1/air-quality?", sent.ToString());
        Assert.Contains("latitude=47.05", sent.ToString());
    }

    [Fact(Timeout = 5000)]
    public async Task The_flood_request_uri_carries_the_flood_path_with_the_fixed_horizon()
    {
        Uri? sent = null;
        var client = new FloodClient(new HttpClient(new StubHandler(HttpStatusCode.OK,
            """{"latitude":47.0,"longitude":8.0,"daily":{"time":[],"river_discharge":[]}}""",
            r => sent = r.RequestUri))
        { BaseAddress = new Uri("https://flood-api.open-meteo.com/") });

        await client.FetchAsync(47.05, 8.31, TestContext.Current.CancellationToken);

        Assert.NotNull(sent);
        Assert.Contains("v1/flood?", sent.ToString());
        Assert.Contains("forecast_days=92", sent.ToString());
    }

    private sealed class StubHandler(HttpStatusCode status, string body, Action<HttpRequestMessage>? inspect) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            inspect?.Invoke(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
