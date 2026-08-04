using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Grpc;
using Njord.Grpc.V2;
using Njord.Pipeline;

namespace Njord.Tests.Grpc;

public sealed class OpsGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
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

    private OpsGrpcService CreateService(NjordOptions? options = null)
    {
        options ??= new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
            Enrichment = new EnrichmentOptions
            {
                Consensus = new ConsensusOptions { Enabled = true },
                Alerts = new AlertOptions { Enabled = true },
                Derived = new DerivedOptions { Enabled = false },
                Trends = new TrendOptions { Enabled = false },
                Indices = new IndexOptions { Enabled = false },
                History = new HistoryOptions { Enabled = false },
            },
        };
        var monitor = new MutableOptionsMonitor(options);
        return new OpsGrpcService(monitor, ActorRegistry, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OpsGrpcService>.Instance);
    }

    [Fact(Timeout = 5000)]
    public async Task GetStatus_returns_model_poll_states_with_timestamps()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create());

        Assert.Equal(2, status.Models.Count);

        var lucerne = status.Models.First(m => m.Location == "lucerne");
        Assert.Equal("icon_d2", lucerne.Model);
        Assert.Equal("steady", lucerne.Phase);
        Assert.Equal(_now.AddHours(1), lucerne.NextPoll.ToDateTimeOffset());
        Assert.Equal(_now.AddHours(-2), lucerne.LastChange.ToDateTimeOffset());
        Assert.Equal(10800, lucerne.CycleSeconds);

        var zurich = status.Models.First(m => m.Location == "zurich");
        Assert.Equal("discovery", zurich.Phase);
        Assert.Null(zurich.LastChange);
    }

    [Fact(Timeout = 5000)]
    public async Task GetStatus_returns_active_enrichments()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create());

        Assert.Contains("consensus", status.ActiveEnrichments);
        Assert.Contains("alerts", status.ActiveEnrichments);
        Assert.Equal(2, status.ActiveEnrichments.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task GetStatus_returns_process_start_as_timestamp()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create());

        Assert.NotNull(status.ProcessStart);
        Assert.True(status.ProcessStart.ToDateTimeOffset() > DateTimeOffset.MinValue);
    }

    [Fact(Timeout = 5000)]
    public async Task GetStatus_returns_budget_usage()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create());

        Assert.Equal(42, status.Budget.MonthlyUsed);
        Assert.Equal(7, status.Budget.DailyUsed);
    }

    [Fact(Timeout = 5000)]
    public async Task GetTargets_returns_all_configured_pairs_with_timestamps()
    {
        var service = CreateService();

        var response = await service.GetTargets(new GetTargetsRequest(), TestServerCallContext.Create());

        Assert.Equal(2, response.Targets.Count);

        var lucerne = response.Targets.First(t => t.Location == "lucerne");
        Assert.Equal("icon_d2", lucerne.Model);
        Assert.Equal("steady", lucerne.Phase);
        Assert.Equal(_now.AddHours(1), lucerne.NextPoll.ToDateTimeOffset());
        Assert.Equal(_now.AddHours(-2), lucerne.LastChange.ToDateTimeOffset());

        var zurich = response.Targets.First(t => t.Location == "zurich");
        Assert.Equal(_now.AddMinutes(20), zurich.NextPoll.ToDateTimeOffset());
        Assert.Null(zurich.LastChange);
    }

    [Fact(Timeout = 15000)]
    public async Task GetTargets_returns_empty_on_scheduler_timeout()
    {
        var slowScheduler = Sys.ActorOf(Props.Create(() => new SlowSchedulerActor()));
        ActorRegistry.Register<SchedulerActor>(slowScheduler, overwrite: true);

        var service = CreateService();

        var response = await service.GetTargets(new GetTargetsRequest(), TestServerCallContext.Create());

        Assert.Empty(response.Targets);
    }

    [Fact(Timeout = 5000)]
    public async Task TriggerPoll_triggers_specific_model()
    {
        var service = CreateService();

        var response = await service.TriggerPoll(
            new TriggerPollRequest { Location = "lucerne", Model = "icon_d2" },
            TestServerCallContext.Create());

        Assert.Equal(1, response.TriggeredCount);
        Assert.Contains("lucerne/icon_d2", response.Targets);
    }

    [Fact(Timeout = 5000)]
    public async Task TriggerPoll_triggers_all_on_empty_filter()
    {
        var service = CreateService();

        var response = await service.TriggerPoll(
            new TriggerPollRequest(), TestServerCallContext.Create());

        Assert.Equal(2, response.TriggeredCount);
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

            Receive<TriggerImmediatePoll>(msg =>
            {
                if (string.IsNullOrEmpty(msg.Location) && string.IsNullOrEmpty(msg.Model))
                {
                    Sender.Tell(new TriggerPollResult(2, ["lucerne/icon_d2", "zurich/gfs_seamless"]));
                }
                else
                {
                    Sender.Tell(new TriggerPollResult(1, [$"{msg.Location}/{msg.Model}"]));
                }
            });
        }
    }

    private sealed class FakeBudgetTrackerActor : ReceiveActor
    {
        public FakeBudgetTrackerActor()
        {
            Receive<BudgetTrackerActor.GetBudgetUsage>(_ =>
                Sender.Tell(new BudgetTrackerActor.BudgetUsage(42, 7), Self));
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
