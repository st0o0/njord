using Microsoft.AspNetCore.Server.Kestrel.Core;
using Njord.Configuration;
using Njord.Core.Configuration;
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

var njordConfig = builder.Configuration.GetSection(NjordOptions.SectionName);
var grpcPort = njordConfig.GetValue("Grpc:Port", 8081);
var httpPort = njordConfig.GetValue("Http:Port", 8080);

builder.WebHost.ConfigureKestrel(options =>
{
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    {
        options.ListenAnyIP(httpPort, o => o.Protocols = HttpProtocols.Http1);
        options.ListenAnyIP(grpcPort, o =>
        {
            o.Protocols = HttpProtocols.Http2;
            o.KestrelServerOptions.Limits.MinResponseDataRate = null;
        });
    }
    else
    {
        options.ConfigureEndpointDefaults(o => o.Protocols = HttpProtocols.Http1);
        options.ListenAnyIP(grpcPort, o =>
        {
            o.Protocols = HttpProtocols.Http2;
            o.KestrelServerOptions.Limits.MinResponseDataRate = null;
        });
    }
});

builder.Configuration.AddJsonFile(
    Path.Combine("data", "njord-config.json"),
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
