using Njord.Budget.Configuration;
using Njord.Configuration;
using Njord.Grpc.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Polling.Configuration;
using Terp.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();

builder
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

var app = builder.Build();
app.Setup(new NjordApplicationSetup());

await app.RunAsync();
