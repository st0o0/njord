using Njord.Budget.Configuration;
using Njord.Configuration;
using Njord.Grpc.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Polling.Configuration;
using Servus.Application.Startup;

namespace Njord;

public static class NjordHost
{
    public static AppRunner Create(
        WebApplicationBuilder builder,
        Func<WebApplicationBuilder, WebApplication>? build = null)
    {
        builder.Logging.ClearProviders();

        return AppBuilder.Create(builder, build ?? (b => b.Build()))
            .WithSetup<LoggingSetupContainer>()
            .WithSetup<TelemetrySetupContainer>()
            .WithSetup<CoreSetupContainer>()
            .WithSetup<ConfigSetupContainer>()
            .WithSetup<HealthSetupContainer>()
            .WithSetup<OpenMeteoSetupContainer>()
            .WithSetup<BudgetSetupContainer>()
            .WithSetup<PollingSetupContainer>()
            .WithSetup<PipelineSetupContainer>()
            .WithSetup<DomainSetupContainer>()
            .WithSetup<GrpcSetupContainer>()
            .WithSetup<AkkaSetupContainer>()
            .WithSetup<NjordApplicationSetup>()
            .Build();
    }
}
