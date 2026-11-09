using Njord.Budget.Configuration;
using Njord.Configuration;
using Njord.Grpc.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Polling.Configuration;
using Terp.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();

builder.Setups()
    .Add<LoggingSetup>()
    .Add<TelemetrySetup>()
    .Add<CoreSetup>()
    .Add<ConfigSetup>()
    .Add<HealthSetup>()
    .Add<OpenMeteoSetup>()
    .Add<BudgetSetup>()
    .Add<PollingSetup>()
    .Add<PipelineSetup>()
    .Add<DomainSetup>()
    .Add<GrpcSetup>()
    .Add<AkkaSetup>();

var app = builder.Build();
app.Setups().Add<NjordApplicationSetup>();

await app.RunAsync().ConfigureAwait(false);
