using Njord.Budget.Configuration;
using Njord.Configuration;
using Njord.Grpc.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Polling.Configuration;
using Serilog;
using Servus.Application.Startup;

namespace Njord;

public static class NjordHost
{
    // The composition of the service. `build` exists for the integration tests; the service itself runs
    // with the default, the plain WebApplicationBuilder.Build().
    public static AppRunner Create(
        WebApplicationBuilder builder,
        Func<WebApplicationBuilder, WebApplication>? build = null)
    {
        builder.Services.AddSerilog(config =>
        {
            config
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");
        });
        builder.Logging.ClearProviders();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MinResponseDataRate = null;
        });

        return AppBuilder.Create(builder, build ?? (b => b.Build()))
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
