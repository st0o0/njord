using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Grpc;
using Njord.Grpc.V1;
using Njord.Pipeline;

namespace Njord.Tests.Grpc;

public sealed class GetTriggerTargetsSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-test-{Guid.NewGuid():N}");
    private readonly DateTimeOffset _now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithActors((system, registry) =>
        {
            var fakeScheduler = system.ActorOf(Props.Create(() => new FakeSchedulerActor(_now)));
            registry.Register<SchedulerActor>(fakeScheduler);

            var fakeBudgetTracker = system.ActorOf(Props.Create(() => new FakeBudgetTrackerActor()));
            registry.Register<BudgetTrackerActor>(fakeBudgetTracker);
        });
    }

    protected override async Task AfterAllAsync()
    {
        await base.AfterAllAsync();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private ConfigGrpcService CreateService()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var monitor = new MutableOptionsMonitor(options);
        var persistence = new ConfigPersistence(_tempDir);
        return new ConfigGrpcService(monitor, persistence, ActorRegistry, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigGrpcService>.Instance);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_all_configured_location_model_pairs()
    {
        var service = CreateService();

        var response = await service.GetTriggerTargets(
            new GetTriggerTargetsRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.Equal(2, response.Targets.Count);
        Assert.Contains(response.Targets, t => t.Location == "lucerne" && t.Model == "icon_d2");
        Assert.Contains(response.Targets, t => t.Location == "zurich" && t.Model == "gfs_seamless");
    }

    [Fact(Timeout = 5000)]
    public async Task Each_target_includes_poll_state()
    {
        var service = CreateService();

        var response = await service.GetTriggerTargets(
            new GetTriggerTargetsRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        var lucerne = response.Targets.First(t => t.Location == "lucerne");
        Assert.Equal("steady", lucerne.Phase);
        Assert.Equal(0, lucerne.MissCount);
        Assert.Equal(10800, lucerne.CycleSeconds);

        var zurich = response.Targets.First(t => t.Location == "zurich");
        Assert.Equal("discovery", zurich.Phase);
        Assert.Equal(2, zurich.MissCount);
        Assert.False(zurich.HasCycleSeconds);
    }

    [Fact(Timeout = 5000)]
    public async Task Temporal_fields_use_protobuf_timestamps()
    {
        var service = CreateService();

        var response = await service.GetTriggerTargets(
            new GetTriggerTargetsRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        var lucerne = response.Targets.First(t => t.Location == "lucerne");
        Assert.Equal(_now.AddHours(1), lucerne.NextPoll.ToDateTimeOffset());
        Assert.Equal(_now.AddHours(-2), lucerne.LastChange.ToDateTimeOffset());

        var zurich = response.Targets.First(t => t.Location == "zurich");
        Assert.Equal(_now.AddMinutes(20), zurich.NextPoll.ToDateTimeOffset());
        Assert.Null(zurich.LastChange);
    }

    [Fact(Timeout = 15000)]
    public async Task Returns_empty_targets_when_scheduler_times_out()
    {
        var slowScheduler = Sys.ActorOf(Props.Create(() => new SlowSchedulerActor()));
        ActorRegistry.Register<SchedulerActor>(slowScheduler, overwrite: true);

        var service = CreateService();

        var response = await service.GetTriggerTargets(
            new GetTriggerTargetsRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.Empty(response.Targets);
    }

    private sealed class MutableOptionsMonitor(NjordOptions value) : IOptionsMonitor<NjordOptions>
    {
        public NjordOptions CurrentValue { get; set; } = value;
        public NjordOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<NjordOptions, string?> listener) => null;
    }

    private sealed class FakeSchedulerActor : ReceiveActor
    {
        public FakeSchedulerActor(DateTimeOffset now)
        {
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

    private sealed class FakeBudgetTrackerActor : ReceiveActor
    {
        public FakeBudgetTrackerActor()
        {
            Receive<BudgetTrackerActor.GetBudgetUsage>(_ =>
                Sender.Tell(new BudgetTrackerActor.BudgetUsage(0, 0), Self));
        }
    }

    private sealed class SlowSchedulerActor : ReceiveActor
    {
        public SlowSchedulerActor()
        {
            Receive<GetPollStates>(_ => { });
        }
    }
}
