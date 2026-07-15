using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Njord.ServiceDefaults;

public static class Extensions
{
    public static WebApplicationBuilder AddNjordTelemetry(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog(config =>
        {
            config
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        });

        builder.Logging.ClearProviders();

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
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

        return app;
    }
}
