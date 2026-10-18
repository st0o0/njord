using Njord.Budget.Configuration;
using Njord.Configuration;
using Njord.Grpc.Configuration;
using Njord.Mqtt.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Pipeline.Configuration;
using Njord.SensorHub.Configuration;
using Serilog;
using Servus.Application.Startup;

namespace Njord;

public static class NjordHost
{
    // The composition of the service. `overridePath` and `build` exist for the integration tests; the service
    // itself runs with the defaults (the data volume override file and the plain WebApplicationBuilder.Build()).
    public static AppRunner Create(
        WebApplicationBuilder builder,
        string? overridePath = null,
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

        // A corrupt or unreadable override file never stops startup; see ResilientJsonConfigurationSource.
        var overrideFile = builder.Configuration.AddResilientJsonFile(overridePath ?? ConfigSetupContainer.OverridePath, reloadOnChange: true);
        builder.Services.AddSingleton(overrideFile);

        return AppBuilder.Create(builder, build ?? (b => b.Build()))
            .WithSetup<CoreSetupContainer>()
            .WithSetup<ConfigSetupContainer>()
            .WithSetup<HealthSetupContainer>()
            .WithSetup<OpenMeteoSetupContainer>()
            .WithSetup<BudgetSetupContainer>()
            .WithSetup<SensorSetupContainer>()
            .WithSetup<PipelineSetupContainer>()
            .WithSetup<AnalysisSetupContainer>()
            .WithSetup<MqttSetupContainer>()
            .WithSetup<GrpcSetupContainer>()
            .WithSetup<AkkaSetupContainer>()
            .WithSetup<NjordApplicationSetup>()
            .Build();
    }
}
