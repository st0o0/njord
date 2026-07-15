using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;
using Njord.Tests.Shared;
using WireMock.Client;

namespace Njord.Tests.Integration.Ingest;

[Collection("NjordAppHost")]
public sealed class OpenMeteoClientIntegrationSpec
{
    private readonly NjordAppHostFixture _fixture;

    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly CycleId Cycle = new(Run);
    private static readonly LocationOptions Home = new() { Name = "home", Latitude = 47.05, Longitude = 8.31 };
    private static readonly WeatherModel IconEu = new("icon_eu");
    private static readonly ResolvedParameterSet DefaultParameters = ParameterRegistry.Resolve(["Weather"], [], []);
    private static readonly DateTimeOffset FixtureStart = DateTimeOffset.FromUnixTimeSeconds(1_783_728_000);

    public OpenMeteoClientIntegrationSpec(NjordAppHostFixture fixture)
    {
        _fixture = fixture;
    }

    private OpenMeteoClient CreateClient(ResolvedParameterSet? parameters = null)
    {
        var http = new HttpClient { BaseAddress = new Uri(_fixture.WireMockBaseUrl) };
        return new OpenMeteoClient(http, parameters ?? DefaultParameters,
            Microsoft.Extensions.Options.Options.Create(new NjordOptions()));
    }

    [Fact(Timeout = 60000)]
    public async Task Successful_fetch_through_real_http_connection()
    {
        await _fixture.WireMockAdmin.ResetMappingsAsync();
        await _fixture.WireMockAdmin.PostMappingAsync(new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel { Matchers = [new() { Name = "WildcardMatcher", Pattern = "/v1/forecast" }] },
                Params = [new() { Name = "models", Matchers = [new() { Name = "ExactMatcher", Pattern = "icon_eu" }] }],
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 200,
                Body = FixtureReader.Read("openmeteo-icon_eu-96h.json"),
                Headers = new Dictionary<string, object> { ["Content-Type"] = "application/json" },
            },
        });

        var client = CreateClient();
        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        Assert.Equal(IconEu, success.Forecast.Model);
        Assert.Equal("home", success.Forecast.Location);
        Assert.Equal(96, success.Forecast.Hourly.Points.Count);
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        Assert.Equal(20.2, success.Forecast.Hourly.Points[0].Get(temp));
    }

    [Fact(Timeout = 60000)]
    public async Task Request_url_contains_correct_query_parameters()
    {
        await _fixture.WireMockAdmin.ResetMappingsAsync();
        await _fixture.WireMockAdmin.PostMappingAsync(new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel { Matchers = [new() { Name = "WildcardMatcher", Pattern = "/v1/forecast" }] },
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 200,
                Body = FixtureReader.Read("openmeteo-icon_eu-96h.json"),
                Headers = new Dictionary<string, object> { ["Content-Type"] = "application/json" },
            },
        });

        await _fixture.WireMockAdmin.DeleteRequestsAsync();

        var client = CreateClient();
        await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var requests = await _fixture.WireMockAdmin.GetRequestsAsync();
        var request = requests.Last();
        var url = request.Request!.Url!;
        Assert.Contains("latitude=47.05", url);
        Assert.Contains("longitude=8.31", url);
        Assert.Contains("models=icon_eu", url);
        Assert.Contains("wind_speed_unit=ms", url);
        Assert.Contains("timeformat=unixtime", url);
        Assert.Contains("forecast_days=4", url);
        Assert.Contains("hourly=", url);
    }

    [Fact(Timeout = 60000)]
    public async Task Http_429_maps_to_rate_limited_through_real_http()
    {
        await _fixture.WireMockAdmin.ResetMappingsAsync();
        await _fixture.WireMockAdmin.PostMappingAsync(new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel { Matchers = [new() { Name = "WildcardMatcher", Pattern = "/v1/forecast" }] },
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 429,
                Body = """{"error":true,"reason":"Too many requests"}""",
                Headers = new Dictionary<string, object> { ["Content-Type"] = "application/json" },
            },
        });

        var client = CreateClient();
        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.RateLimited, failure.Reason);
    }

    [Fact(Timeout = 60000)]
    public async Task Http_400_maps_to_model_unavailable_through_real_http()
    {
        await _fixture.WireMockAdmin.ResetMappingsAsync();
        await _fixture.WireMockAdmin.PostMappingAsync(new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel { Matchers = [new() { Name = "WildcardMatcher", Pattern = "/v1/forecast" }] },
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 400,
                Body = """{"reason":"No data is available for this location","error":true}""",
                Headers = new Dictionary<string, object> { ["Content-Type"] = "application/json" },
            },
        });

        var client = CreateClient();
        var outcome = await client.FetchAsync(Home, IconEu, Cycle, TestContext.Current.CancellationToken);

        var failure = Assert.IsType<FetchOutcome.Failure>(outcome);
        Assert.Equal(FetchFailureReason.ModelUnavailable, failure.Reason);
        Assert.Contains("No data is available for this location", failure.Detail);
    }

    [Fact(Timeout = 60000)]
    public async Task Icon_d2_fixture_trims_null_tail_to_64_points()
    {
        await _fixture.WireMockAdmin.ResetMappingsAsync();
        await _fixture.WireMockAdmin.PostMappingAsync(new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel { Matchers = [new() { Name = "WildcardMatcher", Pattern = "/v1/forecast" }] },
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 200,
                Body = FixtureReader.Read("openmeteo-icon_d2-96h.json"),
                Headers = new Dictionary<string, object> { ["Content-Type"] = "application/json" },
            },
        });

        var client = CreateClient();
        var outcome = await client.FetchAsync(Home, new WeatherModel("icon_d2"), Cycle, TestContext.Current.CancellationToken);

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        Assert.Equal(64, success.Forecast.Hourly.Points.Count);
        Assert.Equal(FixtureStart.AddHours(63), success.Forecast.Hourly.Points[^1].ValidAt);
    }
}
