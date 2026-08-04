using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Grpc;
using Servus.Core.Application.Startup;

namespace Njord.Configuration;

public sealed class NjordApplicationSetup : ApplicationSetupContainer<WebApplication>
{
    protected override void SetupApplication(WebApplication app)
    {
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
        app.MapGrpcService<WeatherGrpcService>();
        app.MapGrpcService<AdminGrpcService>();
        app.MapGrpcService<OpsGrpcService>();
        app.MapGrpcService<SensorGrpcService>();
    }
}
