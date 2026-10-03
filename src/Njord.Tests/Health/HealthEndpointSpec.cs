using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Njord.Tests.Shared;

namespace Njord.Tests.Health;

public sealed class HealthEndpointSpec : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public HealthEndpointSpec(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Njord:Mqtt:Host", "dummy");
        }).CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        // Host startup (Serilog, Akka.Hosting, gRPC, startup gates) is charged here, not to a fact's Timeout.
        using var warmUp = await _client.GetAsync("/alive", TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Healthz_returns_200_when_within_startup_grace()
    {
        var response = await _client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Alive_returns_200_always()
    {
        var response = await _client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Unknown_path_returns_404()
    {
        var response = await _client.GetAsync("/other", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
