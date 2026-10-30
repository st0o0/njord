using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Servus.Application.Startup;
using BudgetTelemetry = Njord.Budget.Telemetry;
using OpenMeteoTelemetry = Njord.OpenMeteo.Telemetry;
using PipelineTelemetry = Njord.Pipeline.Telemetry;
using PollingTelemetry = Njord.Polling.Telemetry;

namespace Njord.Configuration;

public sealed class TelemetrySetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        var version = typeof(TelemetrySetupContainer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("Njord", serviceVersion: version))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(OpenMeteoTelemetry.MeterName)
                .AddMeter(BudgetTelemetry.MeterName)
                .AddMeter(PipelineTelemetry.MeterName)
                .AddMeter(PollingTelemetry.MeterName)
                .AddPrometheusExporter());
    }
}
