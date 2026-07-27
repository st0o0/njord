using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Grpc;
using Njord.Grpc.V1;
using Njord.Pipeline;

namespace Njord.Tests.Grpc;

public sealed class ConfigGrpcServiceStatusSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-test-{Guid.NewGuid():N}");

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithActors((system, registry) =>
        {
            var fakeScheduler = system.ActorOf(Props.Create(() => new FakeSchedulerActor()));
            registry.Register<SchedulerActor>(fakeScheduler);
        });
    }

    protected override async Task AfterAllAsync()
    {
        await base.AfterAllAsync();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class MutableOptionsMonitor(NjordOptions value) : IOptionsMonitor<NjordOptions>
    {
        public NjordOptions CurrentValue { get; set; } = value;
        public NjordOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<NjordOptions, string?> listener) => null;
    }

    private ConfigGrpcService CreateService(NjordOptions? options = null)
    {
        options ??= new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var monitor = new MutableOptionsMonitor(options);
        var persistence = new ConfigPersistence(_tempDir);
        var tracker = new BudgetTracker(TimeProvider.System);
        return new ConfigGrpcService(monitor, persistence, tracker, ActorRegistry, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigGrpcService>.Instance);
    }

    [Fact(Timeout = 5000)]
    public async Task Get_status_returns_model_poll_states_from_scheduler()
    {
        var service = CreateService();

        var status = await service.GetStatus(
            new GetStatusRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.Equal(2, status.Models.Count);

        var lucerne = status.Models.First(m => m.Location == "lucerne");
        Assert.Equal("icon_d2", lucerne.Model);
        Assert.Equal("steady", lucerne.Phase);
        Assert.Equal(10800, lucerne.CycleSeconds);
        Assert.Equal(0, lucerne.MissCount);

        var zurich = status.Models.First(m => m.Location == "zurich");
        Assert.Equal("gfs_seamless", zurich.Model);
        Assert.Equal("discovery", zurich.Phase);
        Assert.False(zurich.HasCycleSeconds);
    }

    [Fact(Timeout = 5000)]
    public async Task Get_status_returns_active_enrichments()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
            Enrichment = new EnrichmentOptions
            {
                Consensus = new ConsensusOptions { Enabled = true },
                Alerts = new AlertThresholdOptions { Enabled = true },
                Derived = new DerivedOptions { Enabled = false },
                Trends = new TrendOptions { Enabled = true },
                Indices = new IndexOptions { Enabled = false },
                Energy = new EnergyOptions { Enabled = false },
                History = new HistoryOptions { Enabled = false },
            },
        };
        var service = CreateService(options);

        var status = await service.GetStatus(
            new GetStatusRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.Equal(["consensus", "alerts", "trends"], status.ActiveEnrichments);
    }

    [Fact(Timeout = 5000)]
    public async Task Get_status_returns_empty_enrichments_when_all_disabled()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
            Enrichment = new EnrichmentOptions
            {
                Consensus = new ConsensusOptions { Enabled = false },
                Alerts = new AlertThresholdOptions { Enabled = false },
                Derived = new DerivedOptions { Enabled = false },
                Trends = new TrendOptions { Enabled = false },
                Indices = new IndexOptions { Enabled = false },
                Energy = new EnergyOptions { Enabled = false },
                History = new HistoryOptions { Enabled = false },
            },
        };
        var service = CreateService(options);

        var status = await service.GetStatus(
            new GetStatusRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.Empty(status.ActiveEnrichments);
    }

    private sealed class FakeSchedulerActor : ReceiveActor
    {
        public FakeSchedulerActor()
        {
            var now = DateTimeOffset.UtcNow;
            Receive<GetPollStates>(_ =>
            {
                var entries = new List<PollStateEntry>
                {
                    new("lucerne", "icon_d2", PollPhase.Steady, now.AddHours(1), now.AddHours(-2), 0, 10800),
                    new("zurich", "gfs_seamless", PollPhase.Discovery, now.AddMinutes(20), null, 2, null),
                };
                Sender.Tell(new PollStatesSnapshot(entries));
            });
        }
    }
}
