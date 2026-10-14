using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core.Configuration;
using Njord.Grpc;
using Prometheus;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class NjordApplicationSetup : ApplicationSetupContainer<WebApplication>
{
    protected override void SetupApplication(WebApplication app)
    {
        // Override-file problems found while the configuration was built (before logging existed) are
        // logged now; later ones are logged as they happen.
        app.Services.GetRequiredService<ConfigOverrideFile>()
            .AttachLogger(app.Services.GetRequiredService<ILogger<ConfigOverrideFile>>());

        app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = 200,
                [HealthStatus.Degraded] = 200,
                [HealthStatus.Unhealthy] = 503,
            },
        });
        app.MapGet("/alive", () => Results.Ok("Alive"));
        app.UseHttpMetrics();
        app.MapMetrics();
        app.MapNjordGrpc();
    }
}
