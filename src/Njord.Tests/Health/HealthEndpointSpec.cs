using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Njord.Tests.Health;

public sealed class HealthEndpointSpec : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointSpec(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Njord:Mqtt:Host", "dummy");
        }).CreateClient();
    }

    [Fact(Timeout = 5000)]
    public async Task Healthz_returns_200_when_within_startup_grace()
    {
        var response = await _client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Timeout = 5000)]
    public async Task Alive_returns_200_always()
    {
        var response = await _client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Timeout = 5000)]
    public async Task Unknown_path_returns_404()
    {
        var response = await _client.GetAsync("/other", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
