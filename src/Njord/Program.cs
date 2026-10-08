using Njord.Configuration;
using Njord.Egress.Configuration;
using Njord.Enrichment.Configuration;
using Njord.Grpc.Configuration;
using Njord.Ingest.Configuration;
using Njord.Mqtt.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Sensors.Configuration;
using Serilog;
using Servus.Core.Application.Startup;

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

builder.Configuration.AddJsonFile(
    Path.Combine("data", "appsettings.Override.json"),
    optional: true,
    reloadOnChange: true);

var runner = AppBuilder.Create(builder, b => b.Build())
    .WithSetup<CoreSetupContainer>()
    .WithSetup<IngestSetupContainer>()
    .WithSetup<SensorSetupContainer>()
    .WithSetup<PipelineSetupContainer>()
    .WithSetup<EgressSetupContainer>()
    .WithSetup<EnrichmentSetupContainer>()
    .WithSetup<MqttSetupContainer>()
    .WithSetup<GrpcSetupContainer>()
    .WithSetup<AkkaSetupContainer>()
    .WithSetup<NjordApplicationSetup>()
    .Build();

await runner.RunAsync();
