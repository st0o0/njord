using Prometheus;
using Servus.Application.Startup;
using BudgetTelemetry = Njord.Budget.Telemetry;
using OpenMeteoTelemetry = Njord.OpenMeteo.Telemetry;
using PipelineTelemetry = Njord.Pipeline.Telemetry;
using PollingTelemetry = Njord.Polling.Telemetry;

namespace Njord.Configuration;

public sealed class TelemetrySetupContainer : IServiceSetupContainer
{
    public static readonly IReadOnlySet<string> NjordMeters = new HashSet<string>
    {
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
    }
}
