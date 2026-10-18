using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.Tests.Configuration;

public sealed class NjordApplicationSetupSpec
{
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(
        ConfigOverrideFile overrideFile, ILogger logger, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" });
        builder.Services.AddSingleton(overrideFile);
        builder.Services.AddSingleton<ILogger<ConfigOverrideFile>>(new TypedLogger<ConfigOverrideFile>(logger));
        builder.Services.AddRequestTimeouts();
        builder.Services.AddHealthChecks();
        builder.Services.AddGrpc();
        var app = builder.Build();
        NjordApplicationSetup.Configure(app);
        await app.StartAsync(ct);

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        Assert.NotNull(addresses);
        return (app, new HttpClient { BaseAddress = new Uri(addresses.Addresses.First()) });
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Override_file_problems_found_before_logging_existed_are_logged_at_startup()
    {
        var ct = TestContext.Current.CancellationToken;
        var overrideFile = new ConfigOverrideFile("data/appsettings.Override.json");
        overrideFile.RecordLoadFailed(new InvalidOperationException("unexpected end of json"));
        var logger = new RecordingLogger();

        var (app, client) = await StartAsync(overrideFile, logger, ct);
        await using var _ = app;
        using var __ = client;

        var error = Assert.Single(logger.Errors);
        Assert.Multiple(
            () => Assert.Contains(overrideFile.Path, error.Message),
            () => Assert.Contains("unexpected end of json", error.Message));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_application_serves_prometheus_metrics_and_the_liveness_probe()
    {
        var ct = TestContext.Current.CancellationToken;
        var (app, client) = await StartAsync(new ConfigOverrideFile("data/appsettings.Override.json"), new RecordingLogger(), ct);
        await using var _ = app;
        using var __ = client;

        var metrics = await client.GetAsync("/metrics", ct);
        var alive = await client.GetAsync("/alive", ct);

        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.OK, metrics.StatusCode),
            () => Assert.Equal(HttpStatusCode.OK, alive.StatusCode));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_four_gRPC_services_are_mapped()
    {
        var ct = TestContext.Current.CancellationToken;
        var (app, client) = await StartAsync(new ConfigOverrideFile("data/appsettings.Override.json"), new RecordingLogger(), ct);
        await using var _ = app;
        using var __ = client;

        var routes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToList();

        Assert.Multiple(
            () => Assert.Contains(routes, r => r.Contains("WeatherService", StringComparison.OrdinalIgnoreCase)),
            () => Assert.Contains(routes, r => r.Contains("AdminService", StringComparison.OrdinalIgnoreCase)),
            () => Assert.Contains(routes, r => r.Contains("OpsService", StringComparison.OrdinalIgnoreCase)),
            () => Assert.Contains(routes, r => r.Contains("SensorService", StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class TypedLogger<T>(ILogger inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
