using System.Globalization;
using System.Net;
using System.Text;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoUriBuilderSpec
{
    private const string EmptyBody = "{}";

    [Fact(Timeout = 5000)]
    public async Task The_air_quality_uri_carries_location_and_fixed_query_parameters()
    {
        var uri = await Capture(client => new AirQualityClient(client).FetchAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken));

        Assert.Equal(
            $"/v1/air-quality?latitude=47.05&longitude=8.31&hourly={AirQualityClient.HourlyVariables}&forecast_days=4&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_flood_uri_carries_location_and_the_fixed_horizon()
    {
        var uri = await Capture(client => new FloodClient(client).FetchAsync(47.05, 8.31, TestContext.Current.CancellationToken));

        Assert.Equal(
            $"/v1/flood?latitude=47.05&longitude=8.31&daily={FloodClient.DailyVariables}&forecast_days=92&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_marine_uri_carries_location_and_the_fixed_horizon()
    {
        var uri = await Capture(client => new MarineClient(client).FetchAsync(47.05, 8.31, TestContext.Current.CancellationToken));

        Assert.Equal(
            $"/v1/marine?latitude=47.05&longitude=8.31&hourly={MarineClient.HourlyVariables}&forecast_days=8&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact(Timeout = 5000)]
    public async Task Coordinates_use_the_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var uri = await Capture(client => new AirQualityClient(client).FetchAsync(47.05, 8.31, 4, TestContext.Current.CancellationToken));

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
        var handler = new CapturingHandler(request => captured = request.RequestUri?.PathAndQuery);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        await call(client);

        return Assert.IsType<string>(captured);
    }

    private sealed class CapturingHandler(Action<HttpRequestMessage> inspect) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            inspect(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EmptyBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
