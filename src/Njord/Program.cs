using Njord.Budget.Configuration;
using Njord.Configuration;
using Njord.Grpc.Configuration;
using Njord.Mqtt.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Pipeline.Configuration;
using Njord.SensorHub.Configuration;
using Serilog;
using Servus.Application.Startup;

var builder = WebApplication.CreateBuilder(args);

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
var overrideFile = builder.Configuration.AddResilientJsonFile(CoreSetupContainer.OverridePath, reloadOnChange: true);
builder.Services.AddSingleton(overrideFile);

var runner = AppBuilder.Create(builder, b => b.Build())
    .WithSetup<CoreSetupContainer>()
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

await runner.RunAsync();
