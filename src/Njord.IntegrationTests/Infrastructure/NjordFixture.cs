using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Remote.Hosting;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Grpc;
using Njord.Health;
using Njord.Ingest;
using Njord.Mqtt;
using Njord.Pipeline;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests.Infrastructure;

public sealed class NjordFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private TestProbe? _schedulerProbe;
    private TestProbe? _budgetTrackerProbe;
    private TestProbe? _pipelineProbe;
    private TestProbe? _modelStateProbe;
    private TestProbe? _enrichmentProbe;
    private TestProbe? _sensorHubProbe;
    private TestProbe? _forecastSnapshotProbe;
    private TestProbe? _enrichmentSnapshotProbe;
    private TestProbe? _grpcSnapshotConsumerProbe;
    private TestProbe? _mqttConnectionProbe;
    private TestProbe? _mqttStateProbe;
    private TestProbe? _mqttDiscoveryProbe;
    private TestProbe? _forecastHistoryRegionProbe;
    private TestProbe? _forecastSnapshotRegionProbe;
    private TestProbe? _enrichmentSnapshotRegionProbe;

    public HttpClient Client { get; private set; } = null!;
    public GrpcChannel GrpcChannel { get; private set; } = null!;
    public ActorSystem System { get; private set; } = null!;

    public TestProbe SchedulerProbe => _schedulerProbe!;
    public TestProbe BudgetTrackerProbe => _budgetTrackerProbe!;
    public TestProbe PipelineProbe => _pipelineProbe!;
    public TestProbe ModelStateProbe => _modelStateProbe!;
    public TestProbe EnrichmentProbe => _enrichmentProbe!;
    public TestProbe SensorHubProbe => _sensorHubProbe!;
    public TestProbe ForecastSnapshotProbe => _forecastSnapshotProbe!;
    public TestProbe EnrichmentSnapshotProbe => _enrichmentSnapshotProbe!;
    public TestProbe GrpcSnapshotConsumerProbe => _grpcSnapshotConsumerProbe!;
    public TestProbe MqttConnectionProbe => _mqttConnectionProbe!;
    public TestProbe MqttStateProbe => _mqttStateProbe!;
    public TestProbe MqttDiscoveryProbe => _mqttDiscoveryProbe!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = "Lucerne",
            ["Njord:Locations:0:Latitude"] = "47.05",
            ["Njord:Locations:0:Longitude"] = "8.31",
            ["Njord:Models:0"] = "icon_d2",
            ["Njord:Mqtt:Enabled"] = "false",
            ["Njord:Mqtt:Host"] = "localhost",
            ["Njord:PersistencePath"] = Path.Combine(
                Path.GetTempPath(), $"njord-fixture-{Guid.NewGuid():N}", "journal.db"),
        });

        builder.Services
            .AddOptions<NjordOptions>()
            .Bind(builder.Configuration.GetSection(NjordOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<NjordOptions>, NjordOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<NjordOptions>, ConsensusOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<NjordOptions>, HistoryOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<NjordOptions>, IndexOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<NjordOptions>, SensorOptionsValidator>();
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NjordOptions>>().Value;
            return ParameterRegistry.Resolve(
                options.Parameters.Groups,
                options.Parameters.Extra,
                options.Parameters.Exclude);
        });
        builder.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(
            new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero)));
        builder.Services.AddNjordPipeline();
        builder.Services.AddSingleton(sp =>
        {
            var state = new NjordHealthState
            {
                ServiceStartedUtc = sp.GetRequiredService<TimeProvider>().GetUtcNow(),
            };
            return state;
        });
        builder.Services.AddHealthChecks()
            .AddCheck<PipelineHealthCheck>("pipeline");
        builder.Services.AddNjordEnrichment();
        builder.Services.AddNjordMqtt(false);
        builder.Services.AddNjordGrpc();
        builder.Services.AddNjordIngest();
        builder.Services.AddSingleton<ConfigPersistence>();
        builder.Services.AddGrpc();

        builder.Services.AddAkka("test", (akkaBuilder, _) =>
        {
            akkaBuilder
                .ConfigureLoggers(loggers =>
                {
                    loggers.ClearLoggers();
                    loggers.AddLoggerFactory();
                })
                .AddHocon("akka.test.timefactor = 3", HoconAddMode.Prepend)
                .WithInMemoryJournal()
                .WithInMemorySnapshotStore()
                .WithRemoting(new RemoteOptions { HostName = "localhost", Port = 0 })
                .WithClustering()
                .WithActors((system, registry) =>
                {
                    var cluster = Akka.Cluster.Cluster.Get(system);
                    cluster.Join(cluster.SelfAddress);

                    System = system;

                    _schedulerProbe = CreateAndRegister<ISchedulerActor>(system, registry);
                    _budgetTrackerProbe = CreateAndRegister<IBudgetTrackerActor>(system, registry);
                    _pipelineProbe = CreateAndRegister<IPipelineActor>(system, registry);
                    _modelStateProbe = CreateAndRegister<IModelStateActor>(system, registry);
                    _enrichmentProbe = CreateAndRegister<IEnrichmentActor>(system, registry);
                    _sensorHubProbe = CreateAndRegister<ISensorHubActor>(system, registry);
                    _forecastSnapshotProbe = CreateAndRegister<IForecastSnapshotActor>(system, registry);
                    _enrichmentSnapshotProbe = CreateAndRegister<IEnrichmentSnapshotActor>(system, registry);
                    _grpcSnapshotConsumerProbe = CreateAndRegister<IGrpcSnapshotConsumerActor>(system, registry);
                    _mqttConnectionProbe = CreateAndRegister<IMqttConnectionActor>(system, registry);
                    _mqttStateProbe = CreateAndRegister<IMqttStateActor>(system, registry);
                    _mqttDiscoveryProbe = CreateAndRegister<IMqttDiscoveryActor>(system, registry);
                    _forecastHistoryRegionProbe = CreateAndRegister<IForecastHistoryRegion>(system, registry);
                    _forecastSnapshotRegionProbe = CreateAndRegister<IForecastSnapshotRegion>(system, registry);
                    _enrichmentSnapshotRegionProbe = CreateAndRegister<IEnrichmentSnapshotRegion>(system, registry);
                });
        });

        _app = builder.Build();

        _app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = 200,
                [HealthStatus.Degraded] = 200,
                [HealthStatus.Unhealthy] = 503,
            },
        });
        _app.MapGet("/alive", () => Results.Ok("Alive"));
        _app.MapNjordGrpc();

        await _app.StartAsync();
        Client = _app.GetTestClient();

        var grpcHandler = _app.GetTestServer().CreateHandler();
        GrpcChannel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = grpcHandler,
        });
    }

    public async ValueTask DisposeAsync()
    {
        GrpcChannel.Dispose();
        Client.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static TestProbe CreateAndRegister<TKey>(ActorSystem system, IActorRegistry registry)
    {
        var probe = new TestProbe(system, new XunitAssertions());
        registry.Register<TKey>(probe);
        return probe;
    }
}
