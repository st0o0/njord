using System.Net;
using System.Text;
using Njord.Core.Configuration;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoClientSpec
{
    private static readonly DateTimeOffset Run = TestTimeConstants.ForecastEpoch;
    private static readonly LocationOptions Home = new() { Name = "home", Latitude = 47.05, Longitude = 8.31 };
    private static readonly ResolvedParameterSet DefaultParameters = ParameterSelection.Resolve(["Weather"], [], []);

    private static readonly DateTimeOffset FixtureStart = DateTimeOffset.FromUnixTimeSeconds(1_783_728_000);

    private static string Fixture(string name) => FixtureReader.Read(name);

    private static (OpenMeteoClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string? baseUrl = null)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl ?? "https://ensemble-api.open-meteo.com/") };
        return (new OpenMeteoClient(http, DefaultParameters), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Task<EnsembleFetchResult> FetchAsync(OpenMeteoClient client, CancellationToken ct)
        => client.FetchEnsembleAsync(Home, 4, Run, ct);

    [Fact(Timeout = 5000)]
    public async Task A_successful_fetch_maps_to_a_weather_forecast()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, Fixture("openmeteo-icon_eu-96h.json")));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var success = Assert.IsType<EnsembleFetchResult.Success>(outcome);
        var forecast = success.Forecast;
        Assert.Equal(96, forecast.Points.Length);
        var first = forecast.Points[0];
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Weather, forecast.Domain),
            () => Assert.Equal(Run, forecast.ModelRun),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(FixtureStart, first.ValidFrom),
            () => Assert.Equal(20.2, first.Temperature),
            () => Assert.Equal(1014.8, first.Pressure));
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_carry_the_configured_variables_and_no_credentials()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, Fixture("openmeteo-icon_eu-96h.json")));

        await FetchAsync(client, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.NotNull(request.RequestUri);
        var uri = request.RequestUri.ToString();
        Assert.Multiple(
            () => Assert.Contains("/v1/ensemble?", uri),
            () => Assert.Contains("latitude=47.05", uri),
            () => Assert.Contains("longitude=8.31", uri),
            () => Assert.Contains("hourly=", uri),
            () => Assert.Contains("temperature_2m", uri),
            () => Assert.Contains("wind_speed_unit=ms", uri),
            () => Assert.Contains("timeformat=unixtime", uri),
            () => Assert.Contains("timezone=UTC", uri),
            () => Assert.Contains("forecast_days=4", uri),
            () => Assert.Empty(request.Headers));
    }

    [Fact(Timeout = 5000)]
    public async Task Http_429_maps_to_rate_limited()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.TooManyRequests, """{"error":true,"reason":"Too many requests"}"""));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Equal(PollFailureReason.RateLimited, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task Http_400_with_error_payload_maps_to_model_unavailable_carrying_the_reason()
    {
        var (client, _) = CreateClient(_ => Json(
            HttpStatusCode.BadRequest,
            """{"reason":"No data is available for this location","error":true}"""));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.ModelUnavailable, failure.Reason),
            () => Assert.Equal("HTTP 400: No data is available for this location", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task Http_400_with_an_unreadable_body_maps_to_model_unavailable_without_a_reason()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.BadRequest, "not json"));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.ModelUnavailable, failure.Reason),
            () => Assert.Equal("HTTP 400 from the forecast endpoint", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task Other_http_errors_map_to_transport()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.InternalServerError, "{}"));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.Transport, failure.Reason),
            () => Assert.Equal("HTTP 500 from the ensemble endpoint", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task Malformed_json_maps_to_malformed_payload()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, "{ this is not json"));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Equal(PollFailureReason.MalformedPayload, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task A_response_without_hourly_data_maps_to_malformed_payload()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, """{"hourly":{"time":[]}}"""));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.MalformedPayload, failure.Reason),
            () => Assert.Equal("Response contained no hourly data", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task Timestamps_without_any_values_map_to_malformed_payload()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, """{"hourly":{"time":[1783728000,1783731600]}}"""));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.MalformedPayload, failure.Reason),
            () => Assert.Equal("Response carried hourly timestamps but no values", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task The_all_null_tail_beyond_the_model_horizon_is_trimmed()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, Fixture("openmeteo-icon_d2-96h.json")));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var success = Assert.IsType<EnsembleFetchResult.Success>(outcome);
        var points = success.Forecast.Points;
        Assert.Equal(64, points.Length);
        Assert.Equal(FixtureStart.AddHours(63), points[^1].ValidFrom);
        Assert.Equal(31.6, points[^1].Temperature);
    }

    [Fact(Timeout = 5000)]
    public async Task Network_errors_map_to_transport_after_exactly_one_attempt()
    {
        var (client, handler) = CreateClient(_ => throw new HttpRequestException("connection refused"));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.Transport, failure.Reason),
            () => Assert.Equal("connection refused", failure.Detail),
            () => Assert.Single(handler.Requests));
    }

    [Fact(Timeout = 5000)]
    public async Task A_timeout_without_caller_cancellation_maps_to_transport()
    {
        var (client, _) = CreateClient(_ => throw new TaskCanceledException("timed out"));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<EnsembleFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.Transport, failure.Reason),
            () => Assert.Equal("Request timed out", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task Missing_arrays_are_treated_as_zero_values()
    {
        var body = """{ "hourly": { "time": [1783728000], "temperature_2m": [20.2] } }""";
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, body));

        var outcome = await FetchAsync(client, TestContext.Current.CancellationToken);

        var success = Assert.IsType<EnsembleFetchResult.Success>(outcome);
        var point = Assert.Single(success.Forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(20.2, point.Temperature),
            () => Assert.Equal(0, point.WindSpeed),
            () => Assert.Null(point.WindGusts));
    }

    [Fact(Timeout = 5000)]
    public async Task Custom_base_url_is_applied_to_requests()
    {
        var customUrl = "http://wiremock.local:8080/";
        var (client, handler) = CreateClient(
            _ => Json(HttpStatusCode.OK, Fixture("openmeteo-icon_eu-96h.json")),
            baseUrl: customUrl);

        await FetchAsync(client, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.NotNull(request.RequestUri);
        Assert.StartsWith(customUrl, request.RequestUri.ToString());
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
