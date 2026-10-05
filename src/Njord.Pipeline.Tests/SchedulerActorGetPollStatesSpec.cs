using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Messages.Common;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class SchedulerActorGetPollStatesSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            DiscoveryInterval = TimeSpan.FromMilliseconds(50),
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
            ],
            Models = ["icon_d2"],
        };
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(ParameterRegistry.Resolve(["Weather"], [], []));
        services.AddSingleton(new NjordHealthState { ServiceStartedUtc = _time.GetUtcNow() });
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .WithActors((system, registry) =>
            {
                _offerProbe = CreateTestProbe();
                var fakePipeline = system.ActorOf(
                    Props.Create(() => new FakePipelineActor(_offerProbe)));
                registry.Register<IPipelineActor>(fakePipeline);
            })
            .WithActors((system, registry, resolver) =>
            {
                registry.Register<ISchedulerActor>(
                    system.ActorOf(resolver.Props<SchedulerActor>(), "scheduler"));
            })
            .AddTestTimefactor();
    }

    private IActorRef Scheduler => ActorRegistry.Get<ISchedulerActor>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Get_poll_states_returns_all_configured_models()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        var snapshot = await Scheduler.Ask<QueryPollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Contains(snapshot.Entries, e => e.Location == "lucerne" && e.ModelId == "icon_d2");
        Assert.Contains(snapshot.Entries, e => e.Location == "zurich" && e.ModelId == "icon_d2");
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Get_poll_states_reflects_discovery_phase_initially()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        var snapshot = await Scheduler.Ask<QueryPollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var entry = snapshot.Entries.First(e => e.Location == "lucerne");
        Assert.Equal(PollPhase.Discovery, entry.Phase);
        Assert.Null(entry.CycleSeconds);
        Assert.Null(entry.LastChangeUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Get_poll_states_reflects_state_after_hash_change()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var snapshot = await Scheduler.Ask<QueryPollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var entry = snapshot.Entries.First(e => e.Location == "lucerne");
        Assert.Equal(0, entry.MissCount);
        Assert.NotNull(entry.LastChangeUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Get_poll_states_reflects_miss_count_after_unchanged_hash()
    {
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: TestContext.Current.CancellationToken);

        await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await Scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var snapshot = await Scheduler.Ask<QueryPollStatesResult>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var entry = snapshot.Entries.First(e => e.Location == "lucerne");
        Assert.Equal(1, entry.MissCount);
    }

    private sealed class FakePipelineActor : ReceiveActor
    {
        public FakePipelineActor(IActorRef probe)
        {
            Receive<WeightedTarget>(msg => probe.Tell(msg));
        }
    }
}
