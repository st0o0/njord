using System.Net;
using System.Text;
using Njord.Messages.Forecast;
using Njord.Model;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoClientSpec
{
    private static readonly OpenMeteoRequest Request = new("v1/ensemble?latitude=47&longitude=8", "home", DomainKind.Weather);

    private static OpenMeteoClient ClientAnswering(HttpStatusCode status, string body, Action<HttpRequestMessage>? inspect = null) =>
        new(new HttpClient(new StubHandler(status, body, inspect)) { BaseAddress = new Uri("https://ensemble-api.open-meteo.com/") });

    [Fact(Timeout = 5000)]
    public async Task An_hourly_payload_is_parsed_into_a_series_with_missing_values_as_null()
    {
        var client = ClientAnswering(HttpStatusCode.OK,
            """{"hourly":{"time":[1000,1003600],"temperature_2m":[20.5,null]}}""");

        var result = await client.FetchAsync(Request, TestContext.Current.CancellationToken);

        var series = Assert.IsType<OpenMeteoFetchResult.Success>(result).Response.Hourly;
        Assert.NotNull(series);
        Assert.Multiple(
            () => Assert.Equal([1000L, 1003600L], series.Time),
            () => Assert.Equal(20.5, series.ValueAt("temperature_2m", 0)),
            () => Assert.Null(series.ValueAt("temperature_2m", 1)));
    }

    [Fact(Timeout = 5000)]
    public async Task A_daily_payload_is_parsed_into_the_daily_series()
    {
        var client = ClientAnswering(HttpStatusCode.OK,
            """{"daily":{"time":[86400],"river_discharge":[120.0]}}""");

        var result = await client.FetchAsync(Request with { Domain = DomainKind.Flood }, TestContext.Current.CancellationToken);

        var response = Assert.IsType<OpenMeteoFetchResult.Success>(result).Response;
        Assert.Multiple(
            () => Assert.Null(response.Hourly),
            () => Assert.Equal(120.0, response.Daily?.ValueAt("river_discharge", 0)));
    }

    [Fact(Timeout = 5000)]
    public async Task The_request_uri_is_sent_unchanged()
    {
        Uri? sent = null;
        var client = ClientAnswering(HttpStatusCode.OK, """{"hourly":{"time":[]}}""", r => sent = r.RequestUri);

        await client.FetchAsync(Request, TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://ensemble-api.open-meteo.com/v1/ensemble?latitude=47&longitude=8"), sent);
    }

    [Fact(Timeout = 5000)]
    public async Task A_bad_request_is_reported_as_model_unavailable_with_the_api_reason()
    {
        var client = ClientAnswering(HttpStatusCode.BadRequest, """{"error":true,"reason":"No data is available for this location"}""");

        var result = await client.FetchAsync(Request, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OpenMeteoFetchResult.Failure>(result);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.ModelUnavailable, failure.Reason),
            () => Assert.Equal("HTTP 400: No data is available for this location", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task A_rate_limited_response_is_reported_as_rate_limited()
    {
        var client = ClientAnswering(HttpStatusCode.TooManyRequests, "");

        var result = await client.FetchAsync(Request, TestContext.Current.CancellationToken);

        Assert.Equal(PollFailureReason.RateLimited, Assert.IsType<OpenMeteoFetchResult.Failure>(result).Reason);
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
