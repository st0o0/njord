using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Model;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoAirQualityClientSpec
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ResolvedParameterSet DefaultParameters = ParameterSelection.Resolve(["Weather"], [], []);

    private const string Body = """
        {
          "hourly": {
            "time": [1783728000, 1783731600, 1783735200],
            "pm2_5": [12.5, 30.1, null],
            "pm10": [20.0, 40.0, null],
            "ozone": [55.0, 60.0, null],
            "nitrogen_dioxide": [10.0, 11.0, null],
            "european_aqi": [30, 45, null],
            "alder_pollen": [1.0, null, null],
            "birch_pollen": [25.0, 80.0, null],
            "grass_pollen": [0.0, 3.0, null],
            "mugwort_pollen": [null, null, null],
            "ragweed_pollen": [0.0, 0.0, null]
          }
        }
        """;

    private static (OpenMeteoClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://ensemble-api.open-meteo.com/") };
        return (new OpenMeteoClient(http, DefaultParameters, new FakeTimeProvider(Now)), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public void The_air_quality_uri_targets_the_dedicated_host_with_all_variables()
    {
        var uri = OpenMeteoRequestMappings.ToAirQualityRequestUri(47.05, 8.31, 4);

        Assert.Equal(
            "https://air-quality-api.open-meteo.com/v1/air-quality?latitude=47.05&longitude=8.31" +
            "&hourly=pm2_5,pm10,ozone,nitrogen_dioxide,european_aqi,alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,ragweed_pollen" +
            "&forecast_days=4&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_request_goes_to_the_air_quality_host_without_credentials()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, Body));

        await client.FetchAirQualityAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.NotNull(request.RequestUri);
        Assert.Multiple(
            () => Assert.Equal("air-quality-api.open-meteo.com", request.RequestUri.Host),
            () => Assert.Equal("/v1/air-quality", request.RequestUri.AbsolutePath),
            () => Assert.Contains("forecast_days=4", request.RequestUri.Query),
            () => Assert.Empty(request.Headers));
    }

    [Fact(Timeout = 5000)]
    public async Task A_successful_response_maps_to_an_air_quality_forecast_and_trims_trailing_empty_hours()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, Body));

        var outcome = await client.FetchAirQualityAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken);

        var success = Assert.IsType<AirQualityFetchResult.Success>(outcome);
        var forecast = success.Forecast;
        Assert.Equal(2, forecast.Points.Length);
        var first = forecast.Points[0];
        Assert.Multiple(
            () => Assert.Equal(DomainKind.AirQuality, forecast.Domain),
            () => Assert.Equal(Now, forecast.ModelRun),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_783_728_000), first.ValidFrom),
            () => Assert.Equal(12.5, first.Pm25),
            () => Assert.Equal(20.0, first.Pm10),
            () => Assert.Equal(55.0, first.Ozone),
            () => Assert.Equal(10.0, first.NitrogenDioxide),
            () => Assert.Equal(30.0, first.EuropeanAqi),
            () => Assert.Equal(25.0, first.BirchPollen),
            () => Assert.Equal(0.0, first.GrassPollen),
            () => Assert.Equal(1.0, first.Alder),
            () => Assert.Null(first.Mugwort),
            () => Assert.Null(forecast.Points[1].Alder));
    }

    [Fact]
    public void Mapping_a_series_without_any_core_values_yields_no_forecast()
    {
        var series = new OpenMeteoTimeSeries { Time = [1_783_728_000] };

        Assert.Null(series.ToAirQualityForecast(Now));
    }

    [Fact(Timeout = 5000)]
    public async Task A_response_without_hourly_data_is_malformed()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, """{"hourly":{"time":[]}}"""));

        var outcome = await client.FetchAirQualityAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<AirQualityFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.MalformedPayload, failure.Reason),
            () => Assert.Equal("Response contained no hourly data", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task Http_429_maps_to_rate_limited()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.TooManyRequests, """{"error":true,"reason":"Too many"}"""));

        var outcome = await client.FetchAirQualityAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<AirQualityFetchResult.Failure>(outcome);
        Assert.Equal(PollFailureReason.RateLimited, failure.Reason);
    }

    [Fact(Timeout = 5000)]
    public async Task Http_400_carries_the_api_reason()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.BadRequest, """{"error":true,"reason":"No data is available"}"""));

        var outcome = await client.FetchAirQualityAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<AirQualityFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.ModelUnavailable, failure.Reason),
            () => Assert.Equal("HTTP 400: No data is available", failure.Detail));
    }

    [Fact(Timeout = 5000)]
    public async Task Http_500_maps_to_transport()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.InternalServerError, "{}"));

        var outcome = await client.FetchAirQualityAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<AirQualityFetchResult.Failure>(outcome);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.Transport, failure.Reason),
            () => Assert.Equal("HTTP 500 from the air quality endpoint", failure.Detail));
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
