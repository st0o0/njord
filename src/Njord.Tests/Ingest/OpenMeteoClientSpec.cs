using System.Net;
using System.Text;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;
using Njord.Tests.Shared;

namespace Njord.Tests.Ingest;

public sealed class OpenMeteoClientSpec
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly CycleId Cycle = new(Run);
    private static readonly LocationOptions Home = new() { Name = "home", Latitude = 47.05, Longitude = 8.31 };
    private static readonly WeatherModel IconEu = new("icon_eu");
    private static readonly ResolvedParameterSet DefaultParameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private static readonly DateTimeOffset FixtureStart = DateTimeOffset.FromUnixTimeSeconds(1_783_728_000);

    private static string Fixture(string name)
        => FixtureReader.Read(name);

    private static (OpenMeteoClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        ResolvedParameterSet? parameters = null)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.com/") };
        return (new OpenMeteoClient(http, parameters ?? DefaultParameters,
            Microsoft.Extensions.Options.Options.Create(new NjordOptions())), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact(Timeout = 5000)]
    public async Task A_successful_fetch_maps_to_a_model_forecast()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, Fixture("openmeteo-icon_eu-96h.json")));

        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        var forecast = success.Forecast;
        Assert.Equal(IconEu, forecast.Model);
        Assert.Equal("home", forecast.Location);
        Assert.Equal(Cycle, forecast.Cycle);
        Assert.Equal(96, forecast.Hourly.Points.Count);
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var first = forecast.Hourly.Points[0];
        Assert.Equal(FixtureStart, first.ValidAt);
        Assert.Equal(20.2, first.Get(temp));
        var pressureMsl = ParameterRegistry.GetByApiName("pressure_msl")!;
        Assert.Equal(1014.8, first.Get(pressureMsl));
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_carry_the_configured_variables_and_no_credentials()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, Fixture("openmeteo-icon_eu-96h.json")));

        await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        var uri = request.RequestUri!.ToString();
        Assert.Contains("/v1/forecast?", uri);
        Assert.Contains("latitude=47.05", uri);
        Assert.Contains("longitude=8.31", uri);
        Assert.Contains("models=icon_eu", uri);
        Assert.Contains("hourly=", uri);
        Assert.Contains("temperature_2m", uri);
        Assert.Contains("wind_speed_unit=ms", uri);
        Assert.Contains("timeformat=unixtime", uri);
        Assert.Contains("forecast_days=4", uri);
        Assert.Contains("daily=", uri);
        Assert.Empty(request.Headers);
    }

    [Fact(Timeout = 5000)]
    public async Task Http_429_maps_to_rate_limited()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.TooManyRequests, """{"error":true,"reason":"Too many requests"}"""));

        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.RateLimited, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task Http_400_with_error_payload_maps_to_model_unavailable_carrying_the_reason()
    {
        var (client, _) = CreateClient(_ => Json(
            HttpStatusCode.BadRequest,
            """{"reason":"No data is available for this location","error":true}"""));

        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.ModelUnavailable, failure.Reason);
        Assert.Contains("No data is available for this location", failure.Detail);
    }

    [Fact(Timeout = 5000)]
    public async Task Malformed_json_maps_to_malformed_payload()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, "{ this is not json"));

        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.MalformedPayload, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task Unit_drift_maps_to_malformed_payload()
    {
        var body = """
            {
              "hourly_units": {
                "time": "unixtime", "temperature_2m": "°C", "apparent_temperature": "°C",
                "wind_speed_10m": "km/h", "wind_gusts_10m": "km/h"
              },
              "hourly": { "time": [1783728000], "temperature_2m": [20.2], "wind_speed_10m": [3.4] }
            }
            """;
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.MalformedPayload, failure.Reason);
        Assert.Contains("km/h", failure.Detail);
    }

    [Fact(Timeout = 5000)]
    public async Task The_all_null_tail_beyond_the_model_horizon_is_trimmed()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, Fixture("openmeteo-icon_d2-96h.json")));

        var outcome = await client.FetchAsync(Home, new WeatherModel("icon_d2"), Cycle, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        var points = success.Forecast.Hourly.Points;
        Assert.Equal(64, points.Count);
        Assert.Equal(FixtureStart.AddHours(63), points[^1].ValidAt);
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        Assert.Equal(31.6, points[^1].Get(temp));
    }

    [Fact(Timeout = 5000)]
    public async Task Network_errors_map_to_transport_after_exactly_one_attempt()
    {
        var (client, handler) = CreateClient(_ => throw new HttpRequestException("connection refused"));

        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.Transport, failure.Reason);
        Assert.Single(handler.Requests);
    }

    [Fact(Timeout = 5000)]
    public async Task Missing_arrays_are_treated_as_null_values()
    {
        var body = """
            {
              "hourly_units": { "time": "unixtime", "temperature_2m": "°C" },
              "hourly": { "time": [1783728000], "temperature_2m": [20.2] }
            }
            """;
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        var point = success.Forecast.Hourly.Points[0];
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var windSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
        Assert.Equal(20.2, point.Get(temp));
        Assert.Null(point.Get(windSpeed));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
