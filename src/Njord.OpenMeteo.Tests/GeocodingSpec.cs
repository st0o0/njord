using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Tests;

public sealed class GeocodingSpec
{
    private const string LucerneBody =
        """{"results":[{"id":2659811,"name":"Lucerne","latitude":47.05048,"longitude":8.30635,"elevation":454.0,"timezone":"Europe/Zurich","country":"Switzerland"}]}""";

    [Fact(Timeout = 5000)]
    public async Task The_search_uri_carries_the_escaped_name_count_and_language()
    {
        Uri? sent = null;
        var client = Client(HttpStatusCode.OK, LucerneBody, r => sent = r.RequestUri);

        await client.ResolveAsync("Bad Ragaz", TestContext.Current.CancellationToken);

        Assert.NotNull(sent);
        Assert.Equal(
            "https://geocoding-api.open-meteo.com/v1/search?name=Bad%20Ragaz&count=1&language=en&format=json",
            sent.AbsoluteUri);
    }

    [Fact(Timeout = 5000)]
    public async Task The_first_result_is_parsed_with_all_fields()
    {
        var result = await Client(HttpStatusCode.OK, LucerneBody)
            .ResolveAsync("Lucerne", TestContext.Current.CancellationToken);

        Assert.Equal(new GeocodingResult("Lucerne", 47.05048, 8.30635, 454.0, "Europe/Zurich", "Switzerland"), result);
    }

    [Theory(Timeout = 5000)]
    [InlineData("""{"generationtime_ms":0.5}""")]
    [InlineData("""{"results":[]}""")]
    public async Task A_response_without_results_resolves_to_null(string body)
    {
        var result = await Client(HttpStatusCode.OK, body).ResolveAsync("Nowhere", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact(Timeout = 5000)]
    public async Task A_bad_request_resolves_to_null()
    {
        var result = await Client(HttpStatusCode.BadRequest, "").ResolveAsync("Lucerne", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact(Timeout = 5000)]
    public async Task A_rate_limited_request_throws_for_the_resilience_pipeline_to_handle()
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Client(HttpStatusCode.TooManyRequests, "").ResolveAsync("Lucerne", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact(Timeout = 5000)]
    public async Task A_name_only_location_receives_the_geocoded_coordinates()
    {
        var geocoding = new FakeGeocoding(new GeocodingResult("Lucerne", 47.05, 8.31));
        var options = Options([new LocationOptions { Name = "Lucerne" }]);

        await new LocationResolutionService(options, geocoding).StartAsync(TestContext.Current.CancellationToken);

        var location = Assert.Single(options.Value.Locations);
        Assert.Multiple(
            () => Assert.Equal(47.05, location.Latitude),
            () => Assert.Equal(8.31, location.Longitude),
            () => Assert.Equal(["Lucerne"], geocoding.Requested));
    }

    [Fact(Timeout = 5000)]
    public async Task A_location_with_explicit_coordinates_skips_geocoding()
    {
        var geocoding = new FakeGeocoding(new GeocodingResult("Zurich", 1, 2));
        var options = Options([new LocationOptions { Name = "Zurich", Latitude = 47.38, Longitude = 8.54 }]);

        await new LocationResolutionService(options, geocoding).StartAsync(TestContext.Current.CancellationToken);

        var location = Assert.Single(options.Value.Locations);
        Assert.Multiple(
            () => Assert.Equal(47.38, location.Latitude),
            () => Assert.Equal(8.54, location.Longitude),
            () => Assert.Empty(geocoding.Requested));
    }

    [Fact(Timeout = 5000)]
    public async Task An_unresolvable_name_fails_startup_naming_the_location()
    {
        var options = Options([new LocationOptions { Name = "Atlantis" }]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new LocationResolutionService(options, new FakeGeocoding(null)).StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Location 'Atlantis' has no Latitude/Longitude and geocoding found no match.", ex.Message);
    }

    private static IOptions<NjordOptions> Options(IList<LocationOptions> locations) =>
        Microsoft.Extensions.Options.Options.Create(new NjordOptions { Locations = locations });

    private static GeocodingClient Client(HttpStatusCode status, string body, Action<HttpRequestMessage>? inspect = null) =>
        new(new HttpClient(new StubHandler(status, body, inspect)) { BaseAddress = new Uri("https://geocoding-api.open-meteo.com/") });

    private sealed class FakeGeocoding(GeocodingResult? result) : IGeocodingClient
    {
        public List<string> Requested { get; } = [];

        public Task<GeocodingResult?> ResolveAsync(string name, CancellationToken cancellationToken)
        {
            Requested.Add(name);
            return Task.FromResult(result);
        }
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
