using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class ClimateSpec
{
    private static long Day(int year, int month, int day) =>
        new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    [Fact(Timeout = 5000)]
    public async Task The_uri_requests_the_thirty_year_baseline_in_utc()
    {
        string? sent = null;
        var client = new ClimateClient(new HttpClient(new CapturingHandler(r => sent = r.RequestUri?.PathAndQuery)) { BaseAddress = new Uri("https://climate-api.open-meteo.com/") });

        await client.FetchAsync(47.05, 8.31, TestContext.Current.CancellationToken);

        Assert.Equal(
            "/v1/climate?latitude=47.05&longitude=8.31&start_date=1991-01-01&end_date=2020-12-31" +
            "&models=EC_Earth3P_HR&daily=temperature_2m_mean,precipitation_sum,sunshine_duration&timeformat=unixtime&timezone=UTC",
            sent);
    }

    [Fact(Timeout = 5000)]
    public async Task The_response_is_parsed_into_typed_daily_data()
    {
        const string body = """{"latitude":47.0,"longitude":8.0,"daily":{"time":[1,2],"temperature_2m_mean":[1.5,null],"precipitation_sum":[0.2,0.4]}}""";
        var client = new ClimateClient(new HttpClient(new StubHandler(HttpStatusCode.OK, body)) { BaseAddress = new Uri("https://climate-api.open-meteo.com/") });

        var response = await client.FetchAsync(47.0, 8.0, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var daily = Assert.IsType<ClimateDailyData>(response.Daily);
        Assert.Multiple(
            () => Assert.Equal([1L, 2L], daily.Time),
            () => Assert.Equal([1.5, null], daily.TemperatureMean),
            () => Assert.Equal([0.2, 0.4], daily.PrecipitationSum),
            () => Assert.Null(daily.SunshineDuration));
    }

    [Fact]
    public void Daily_data_is_aggregated_to_twelve_monthly_means()
    {
        var days = new List<long>();
        var temps = new List<double?>();
        var precip = new List<double?>();
        var sun = new List<double?>();
        for (var month = 1; month <= 12; month++)
        {
            foreach (var (day, t) in new[] { (1, 10.0 + month), (2, 12.0 + month) })
            {
                days.Add(Day(2000, month, day));
                temps.Add(t);
                precip.Add(day == 1 ? 0.0 : 6.0);
                sun.Add(7200.0);
            }
        }

        var baselines = new ClimateResponse(0, 0, new ClimateDailyData([.. days], [.. temps], [.. precip], [.. sun])).ToMonthlyBaselines();

        Assert.NotNull(baselines);
        Assert.Equal(12, baselines.Length);
        Assert.Multiple(
            () => Assert.Equal(new MonthlyBaseline(1, 12.0, 3.0, 2.0), baselines[0]),
            () => Assert.Equal(new MonthlyBaseline(7, 18.0, 3.0, 2.0), baselines[6]));
    }

    [Fact]
    public void A_response_missing_a_month_yields_no_baseline()
    {
        var daily = new ClimateDailyData([Day(2000, 1, 1)], [1.0]);

        Assert.Null(new ClimateResponse(0, 0, daily).ToMonthlyBaselines());
    }

    [Fact(Timeout = 5000)]
    public async Task The_service_stores_a_baseline_per_location()
    {
        var store = new ClimateBaselineStore();

        var complete = await Service(FullYear(), store).RefreshAsync(TestContext.Current.CancellationToken);

        var months = Assert.Single(store.Baselines).Value;
        Assert.Multiple(
            () => Assert.True(complete),
            () => Assert.Equal(12, months.Length),
            () => Assert.True(store.Baselines.ContainsKey("lucerne")));
    }

    [Fact(Timeout = 5000)]
    public async Task A_failed_fetch_keeps_the_store_empty_and_reports_incomplete()
    {
        var store = new ClimateBaselineStore();

        var complete = await Service(null, store).RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Multiple(() => Assert.False(complete), () => Assert.Empty(store.Baselines));
    }

    private static ClimateBaselineService Service(ClimateResponse? response, ClimateBaselineStore store) =>
        new(
            Options.Create(new NjordOptions { Locations = [new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31 }] }),
            Options.Create(new EnrichmentOptions { ClimateAnomaly = true }),
            new FakeClimate(response),
            store,
            new FakeTimeProvider(),
            NullLogger<ClimateBaselineService>.Instance);

    private static ClimateResponse FullYear()
    {
        var days = Enumerable.Range(1, 12).Select(m => Day(2000, m, 1)).ToArray();
        return new ClimateResponse(0, 0, new ClimateDailyData(days, [.. days.Select(_ => (double?)10.0)]));
    }

    private sealed class FakeClimate(ClimateResponse? response) : IClimateClient
    {
        public Task<ClimateResponse?> FetchAsync(double latitude, double longitude, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class CapturingHandler(Action<HttpRequestMessage> inspect) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            inspect(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
