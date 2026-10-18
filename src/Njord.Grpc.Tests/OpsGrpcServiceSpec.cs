using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
using Njord.Grpc.V2;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;
using Njord.Messages.Budget;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Grpc.Tests;

public sealed class OpsGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly DateTimeOffset _now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithActors((system, registry) =>
            {
                var fakePollManager = system.ActorOf(Props.Create(() => new FakePollManagerActor(_now)));
                registry.Register<IPollManager>(fakePollManager);

                var fakeBudgetTracker = system.ActorOf(Props.Create(() => new FakeBudgetTrackerActor()));
                registry.Register<IBudgetTracker>(fakeBudgetTracker);
            })
            .AddTestTimefactor();
    }

    private OpsGrpcService CreateService(NjordOptions? options = null)
    {
        options ??= new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var config = TestConfigView.Create(
            options,
            consensus: AppSettingsDefaults.Options<ConsensusOptions>(o => o.Enabled = true),
            alerts: AppSettingsDefaults.Options<AlertOptions>(o => o.Enabled = true),
            derived: AppSettingsDefaults.Options<DerivedOptions>(o => o.Enabled = false),
            trends: AppSettingsDefaults.Options<TrendOptions>(o => o.Enabled = false),
            indices: AppSettingsDefaults.Options<IndexOptions>(o => o.Enabled = false));
        ActorRegistry.Register<IConfigManager>(Sys.ActorOf(Props.Create(() => new FakeConfigManager(config))), overwrite: true);
        return new OpsGrpcService(ActorRegistry, _time,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OpsGrpcService>.Instance);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetStatus_returns_model_poll_states_with_timestamps()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(2, status.Models.Count);

        var lucerne = status.Models.First(m => m.Location == "lucerne");
        Assert.Equal("icon_d2", lucerne.Model);
        Assert.Equal(Njord.Grpc.V2.PollPhase.Steady, lucerne.Phase);
        Assert.Equal(_now.AddHours(1), lucerne.NextPoll.ToDateTimeOffset());
        Assert.Equal(_now.AddHours(-2), lucerne.LastChange.ToDateTimeOffset());
        Assert.Equal(10800, lucerne.CycleSeconds);

        var zurich = status.Models.First(m => m.Location == "zurich");
        Assert.Equal(Njord.Grpc.V2.PollPhase.Discovery, zurich.Phase);
        Assert.Null(zurich.LastChange);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetStatus_returns_active_enrichments()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Contains("consensus", status.ActiveEnrichments);
        Assert.Contains("alerts", status.ActiveEnrichments);
        Assert.Equal(2, status.ActiveEnrichments.Count);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetStatus_returns_process_start_as_timestamp()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.NotNull(status.ProcessStart);
        Assert.True(status.ProcessStart.ToDateTimeOffset() > DateTimeOffset.MinValue);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetStatus_returns_budget_usage()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(42, status.Budget.MonthlyUsed);
        Assert.Equal(7, status.Budget.DailyUsed);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetStatus_flags_nothing_partial_when_all_actors_respond()
    {
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.False(status.BudgetPartial),
            () => Assert.False(status.ModelsPartial));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetStatus_flags_models_partial_on_poll_manager_timeout()
    {
        var slowPollManager = Sys.ActorOf(Props.Create(() => new SlowPollManagerActor()));
        ActorRegistry.Register<IPollManager>(slowPollManager, overwrite: true);
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.True(status.ModelsPartial),
            () => Assert.False(status.BudgetPartial),
            () => Assert.Empty(status.Models),
            () => Assert.Equal(42, status.Budget.MonthlyUsed));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetStatus_flags_budget_partial_on_budget_tracker_timeout()
    {
        var slowTracker = Sys.ActorOf(Props.Create(() => new SlowBudgetTrackerActor()));
        ActorRegistry.Register<IBudgetTracker>(slowTracker, overwrite: true);
        var service = CreateService();

        var status = await service.GetStatus(new GetStatusRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.True(status.BudgetPartial),
            () => Assert.False(status.ModelsPartial),
            () => Assert.Equal(0, status.Budget.MonthlyUsed),
            () => Assert.Equal(2, status.Models.Count));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetTargets_returns_all_configured_pairs_with_timestamps()
    {
        var service = CreateService();

        var response = await service.GetTargets(new GetTargetsRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(2, response.Targets.Count);

        var lucerne = response.Targets.First(t => t.Location == "lucerne");
        Assert.Equal("icon_d2", lucerne.Model);
        Assert.Equal(Njord.Grpc.V2.PollPhase.Steady, lucerne.Phase);
        Assert.Equal(_now.AddHours(1), lucerne.NextPoll.ToDateTimeOffset());
        Assert.Equal(_now.AddHours(-2), lucerne.LastChange.ToDateTimeOffset());

        var zurich = response.Targets.First(t => t.Location == "zurich");
        Assert.Equal(_now.AddMinutes(20), zurich.NextPoll.ToDateTimeOffset());
        Assert.Null(zurich.LastChange);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetTargets_returns_empty_on_poll_manager_timeout()
    {
        var slowPollManager = Sys.ActorOf(Props.Create(() => new SlowPollManagerActor()));
        ActorRegistry.Register<IPollManager>(slowPollManager, overwrite: true);

        var service = CreateService();

        var response = await service.GetTargets(new GetTargetsRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Empty(response.Targets);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task TriggerPoll_triggers_specific_model()
    {
        var service = CreateService();

        var response = await service.TriggerPoll(
            new TriggerPollRequest { Location = "lucerne", Model = "icon_d2" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(1, response.TriggeredCount);
        Assert.Contains("lucerne/icon_d2", response.Targets);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task TriggerPoll_triggers_all_on_empty_filter()
    {
        var service = CreateService();

        var response = await service.TriggerPoll(
            new TriggerPollRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(2, response.TriggeredCount);
    }


    private sealed class FakePollManagerActor : ReceiveActor
    {
        public FakePollManagerActor(DateTimeOffset now)
        {
            Receive<QueryPollStates>(_ =>
            {
                var entries = new List<PollStateEntry>
                {
                    new(new LocationId("lucerne"), "icon_d2", DomainPollPhase.Steady, now.AddHours(1), now.AddHours(-2), 0, 10800),
                    new(new LocationId("zurich"), "gfs_seamless", DomainPollPhase.Discovery, now.AddMinutes(20), null, 2, null),
                };
                Sender.Tell(new PollStatesResult(entries));
            });

            Receive<TriggerImmediatePoll>(msg =>
            {
                if (msg.Location is null && string.IsNullOrEmpty(msg.Model))
                {
                    Sender.Tell(new TriggerImmediatePollCompleted(2, ["lucerne/icon_d2", "zurich/gfs_seamless"]));
                }
                else
                {
                    Sender.Tell(new TriggerImmediatePollCompleted(1, [$"{msg.Location}/{msg.Model}"]));
                }
            });
        }
    }

    private sealed class FakeBudgetTrackerActor : ReceiveActor
    {
        public FakeBudgetTrackerActor()
        {
            Receive<QueryBudgetStatus>(_ =>
                Sender.Tell(new BudgetStatusResult(42, 7, 300_000, 10_000), Self));
        }
    }

    private sealed class SlowBudgetTrackerActor : ReceiveActor
    {
        public SlowBudgetTrackerActor()
        {
            Receive<QueryBudgetStatus>(_ => { });
        }
    }

    private sealed class SlowPollManagerActor : ReceiveActor
    {
        public SlowPollManagerActor()
        {
            Receive<QueryPollStates>(_ => { });
        }
    }
}
