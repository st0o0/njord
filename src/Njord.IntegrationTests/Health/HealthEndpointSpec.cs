using System.Net;
using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests.Health;

[Collection("Health")]
public sealed class HealthEndpointSpec
{
    private readonly HttpClient _client;

    public HealthEndpointSpec(NjordFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task Healthz_returns_200_when_within_startup_grace()
    {
        var response = await _client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Alive_returns_200_always()
    {
        var response = await _client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_path_returns_404()
    {
        var response = await _client.GetAsync("/other", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
