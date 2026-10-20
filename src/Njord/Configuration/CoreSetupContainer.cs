using Prometheus;
using Servus.Application.Startup;
using BudgetTelemetry = Njord.Budget.Telemetry;
using CoreTelemetry = Njord.Core.Telemetry;
using OpenMeteoTelemetry = Njord.OpenMeteo.Telemetry;
using PipelineTelemetry = Njord.Pipeline.Telemetry;
using PollingTelemetry = Njord.Polling.Telemetry;

namespace Njord.Configuration;

public sealed class CoreSetupContainer : IServiceSetupContainer
{
    internal static readonly IReadOnlySet<string> NjordMeters = new HashSet<string>
    {
        CoreTelemetry.MeterName,
        OpenMeteoTelemetry.MeterName,
        BudgetTelemetry.MeterName,
        PipelineTelemetry.MeterName,
        PollingTelemetry.MeterName,
    };

    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        Metrics.ConfigureMeterAdapter(options =>
        {
            options.InstrumentFilterPredicate = instrument => NjordMeters.Contains(instrument.Meter.Name);
        });

        services.AddSingleton(TimeProvider.System);
    }
}
