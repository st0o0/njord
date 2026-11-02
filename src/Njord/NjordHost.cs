using Njord.Budget.Configuration;
using Njord.Configuration;
using Njord.Grpc.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Polling.Configuration;
using Terp.Hosting;

namespace Njord;

public static class NjordHost
{
    public static WebApplicationBuilder Configure(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        return builder
            .Setup(new LoggingSetup())
            .Setup(new TelemetrySetup())
            .Setup(new CoreSetup())
            .Setup(new ConfigSetup())
            .Setup(new HealthSetup())
            .Setup(new OpenMeteoSetup())
            .Setup(new BudgetSetup())
            .Setup(new PollingSetup())
            .Setup(new PipelineSetup())
            .Setup(new DomainSetup())
            .Setup(new GrpcSetup())
            .Setup(new AkkaSetup());
    }

    public static WebApplication Configure(WebApplication app) => app.Setup(new NjordApplicationSetup());
}
