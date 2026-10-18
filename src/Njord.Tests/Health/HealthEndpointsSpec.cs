using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Configuration;

namespace Njord.Tests.Health;

public sealed class HealthEndpointsSpec
{
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(HealthStatus status, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" });
        builder.Services.AddRequestTimeouts();
        builder.Services.AddHealthChecks()
            .AddCheck("probe", () => new HealthCheckResult(status, "probe"));
        var app = builder.Build();
        app.UseRequestTimeouts();
        NjordApplicationSetup.ExposeHealthEndpoints(app);
        await app.StartAsync(ct);

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        Assert.NotNull(addresses);
        return (app, new HttpClient { BaseAddress = new Uri(addresses.Addresses.First()) });
    }

    [Theory(Timeout = 30_000)]
    [InlineData(HealthStatus.Healthy, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Degraded, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable)]
    public async Task Healthz_maps_the_health_status_to_the_http_status(HealthStatus status, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var (app, client) = await StartAsync(status, ct);
        await using var _ = app;
        using var __ = client;

        var response = await client.GetAsync("/healthz", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        Assert.Multiple(
            () => Assert.Equal(expected, response.StatusCode),
            () => Assert.Equal(status.ToString(), body));
    }

    [Fact(Timeout = 30_000)]
    public async Task Alive_answers_ok_while_a_check_is_unhealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        var (app, client) = await StartAsync(HealthStatus.Unhealthy, ct);
        await using var _ = app;
        using var __ = client;

        var response = await client.GetAsync("/alive", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, response.StatusCode),
            () => Assert.Equal("\"Alive\"", body));
    }

    [Fact]
    public void The_health_endpoint_timeout_is_five_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), NjordApplicationSetup.HealthCheckTimeout);
    }
}
