using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Messages.Forecast;
using Njord.Model;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoFloodSpec
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private const string Body = """
        {"daily":{"time":[1783728000,1783814400,1783900800],
        "river_discharge":[120.5,150.0,null],
        "river_discharge_mean":[110.0,115.0,null],
        "river_discharge_median":[100.0,null,null],
        "river_discharge_max":[300.0,310.0,null],
        "river_discharge_min":[20.0,21.0,null],
        "river_discharge_p25":[80.0,82.0,null],
        "river_discharge_p75":[140.0,145.0,null]},
        "daily_units":{"river_discharge":"m3/s"}}
        """;

    private static (OpenMeteoClient Client, RecordingHandler Handler) CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler);
        return (new OpenMeteoClient(http, ParameterSelection.Resolve(["Weather"], [], []), new FakeTimeProvider(Now)), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public void The_flood_uri_targets_the_flood_host_with_daily_variables_and_92_days()
    {
        var uri = OpenMeteoRequestMappings.ToFloodRequestUri(47.05, 8.31);

        Assert.Equal(
            "https://flood-api.open-meteo.com/v1/flood?latitude=47.05&longitude=8.31" +
            "&daily=river_discharge,river_discharge_mean,river_discharge_median,river_discharge_max,river_discharge_min,river_discharge_p25,river_discharge_p75" +
            "&forecast_days=92&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task A_successful_fetch_maps_every_daily_variable_and_drops_days_without_discharge()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, Body));

        var outcome = await client.FetchFloodAsync(47.05, 8.31, 92, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FloodFetchResult.Success>(outcome);
        var forecast = success.Forecast;
        Assert.Equal(2, forecast.Points.Length);
        var first = forecast.Points[0];
        var second = forecast.Points[1];
        var request = Assert.Single(handler.Requests);
        Assert.NotNull(request.RequestUri);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, forecast.Domain),
            () => Assert.Equal(TimeResolution.Daily, forecast.Resolution),
            () => Assert.Equal(Now, forecast.ModelRun),
            () => Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_783_728_000), first.ValidFrom),
            () => Assert.Equal(120.5, first.Discharge),
            () => Assert.Equal(110.0, first.DischargeMean),
            () => Assert.Equal(100.0, first.DischargeMedian),
            () => Assert.Equal(300.0, first.DischargeMax),
            () => Assert.Equal(20.0, first.DischargeMin),
            () => Assert.Equal(80.0, first.DischargeP25),
            () => Assert.Equal(140.0, first.DischargeP75),
            () => Assert.Equal(150.0, second.Discharge),
            () => Assert.Null(second.DischargeMedian),
            () => Assert.Equal("flood-api.open-meteo.com", request.RequestUri.Host));
    }

    [Theory(Timeout = 5000)]
    [InlineData(HttpStatusCode.TooManyRequests, PollFailureReason.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, PollFailureReason.Transport)]
    [InlineData(HttpStatusCode.BadRequest, PollFailureReason.ModelUnavailable)]
    public async Task An_error_status_maps_to_its_failure_reason(HttpStatusCode status, PollFailureReason reason)
    {
        var (client, _) = CreateClient(_ => Json(status, """{"error":true,"reason":"No data is available for this location"}"""));

        var outcome = await client.FetchFloodAsync(47.05, 8.31, 92, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FloodFetchResult.Failure>(outcome);
        Assert.Equal(reason, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task A_payload_without_daily_data_is_malformed()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, """{"daily":{"time":[]}}"""));

        var outcome = await client.FetchFloodAsync(47.05, 8.31, 92, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FloodFetchResult.Failure>(outcome);
        Assert.Equal(PollFailureReason.MalformedPayload, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task A_payload_with_days_but_no_discharge_values_is_malformed()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, """{"daily":{"time":[1783728000],"river_discharge":[null]}}"""));

        var outcome = await client.FetchFloodAsync(47.05, 8.31, 92, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FloodFetchResult.Failure>(outcome);
        Assert.Equal("Response carried daily timestamps but no discharge values", failure.Detail);
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
