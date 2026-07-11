using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;

namespace Njord.Tests.Ingest;

public sealed class KachelmannClientSpec
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly CycleId Cycle = new(Run);
    private static readonly LocationOptions Home = new() { Name = "home", Latitude = 47.05, Longitude = 8.31 };
    private static readonly WeatherModel IconD2 = new("ICON-D2");
    private const string ApiKey = "super-secret-key";

    private static string FixtureJson()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ingest", "Fixtures", "advanced-3h-sample.json"));

    private static (KachelmannClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.kachelmannwetter.com/v02/") };
        http.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        return (new KachelmannClient(http, new FakeTimeProvider(Run.AddSeconds(30))), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact(Timeout = 5000)]
    public async Task A_successful_fetch_maps_to_a_model_forecast()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, FixtureJson()));

        var outcome = await client.FetchAsync(Home, IconD2, Cycle, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        var forecast = success.Forecast;
        Assert.Equal(IconD2, forecast.Model);
        Assert.Equal("home", forecast.Location);
        Assert.Equal(Cycle, forecast.Cycle);
        Assert.Equal(Run.AddSeconds(30), forecast.RetrievedAt);
        Assert.Equal(21.3, forecast.Series.Points[0].Temperature);
        Assert.Contains(forecast.Series.Points, p => p.ValidAt == Run.AddHours(72));
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_hit_the_advanced_3h_endpoint_with_invariant_coordinates()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, FixtureJson()));

        await client.FetchAsync(Home, IconD2, Cycle, TestContext.Current.CancellationToken);

        var uri = Assert.Single(handler.Requests).RequestUri!.ToString();
        Assert.Contains("/v02/forecast/47.05/8.31/advanced/3h", uri);
        Assert.Contains("model=ICON-D2", uri);
        Assert.Contains("units=metric", uri);
    }

    [Fact(Timeout = 5000)]
    public async Task Http_401_maps_to_auth_failure()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.Unauthorized, """{"title":"Unauthorized"}"""));

        var outcome = await client.FetchAsync(Home, IconD2, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.AuthFailed, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task Http_429_maps_to_rate_limited()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.TooManyRequests, """{"title":"Too many requests"}"""));

        var outcome = await client.FetchAsync(Home, IconD2, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.RateLimited, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task Http_400_maps_to_model_unavailable_carrying_the_model()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.BadRequest, """{"title":"unknown model"}"""));

        var outcome = await client.FetchAsync(Home, IconD2, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.ModelUnavailable, failure.Reason);
        Assert.Equal(IconD2, failure.Model);
    }

    [Fact(Timeout = 5000)]
    public async Task Malformed_json_maps_to_malformed_payload()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, "{ this is not json"));

        var outcome = await client.FetchAsync(Home, IconD2, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.MalformedPayload, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task Network_errors_map_to_transport_after_exactly_one_attempt()
    {
        var (client, handler) = CreateClient(_ => throw new HttpRequestException("connection refused"));

        var outcome = await client.FetchAsync(Home, IconD2, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.Transport, failure.Reason);
        Assert.Single(handler.Requests);
    }

    [Fact(Timeout = 5000)]
    public async Task Failure_outcomes_never_contain_the_api_key()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.Unauthorized, $$"""{"title":"bad key {{ApiKey}}"}"""));

        var outcome = await client.FetchAsync(Home, IconD2, Cycle, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(ApiKey, outcome.ToString());
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
