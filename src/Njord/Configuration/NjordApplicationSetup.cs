using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core.Configuration;
using Njord.Grpc;
using Prometheus;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class NjordApplicationSetup : ApplicationSetupContainer<WebApplication>
{
    internal static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    protected override void SetupApplication(WebApplication app) => Configure(app);

    internal static void Configure(WebApplication app)
    {
        app.UseRequestTimeouts();
        ExposeHealthEndpoints(app);
        app.UseHttpMetrics();
        app.MapMetrics();
        app.MapNjordGrpc();
    }

    internal static void ExposeHealthEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = 200,
                [HealthStatus.Degraded] = 200,
                [HealthStatus.Unhealthy] = 503,
            },
        }).WithRequestTimeout(HealthCheckTimeout);
        endpoints.MapGet("/alive", () => Results.Ok("Alive"));
    }
}
