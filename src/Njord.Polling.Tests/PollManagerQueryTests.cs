using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollManagerQueryTests : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(TestTimeConstants.PipelineEpoch);
    private TestProbe _region = null!;

    private readonly IOptions<NjordOptions> _njordOptions = Options.Create(new NjordOptions
    {
        Locations =
        [
            new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
            new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
        ],
        ResolvedLocations =
        [
            new Location("lucerne", 47.05, 8.31),
            new Location("zurich", 47.37, 8.54),
        ],
    });

    private readonly IOptions<PollingOptions> _pollingOptions = Options.Create(new PollingOptions());

    private static readonly IOptions<DomainRequirements> _requirements =
        Options.Create(new DomainRequirements(Forecast: ForecastParams.Temperature));

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
        builder.WithActors((_, registry) =>
        {
            _region = CreateTestProbe("region");
            registry.Register<IPollRegion>(_region.Ref);
        });
    }

    private static readonly PollTarget[] _targets =
    [
        new(new Location("lucerne", 0, 0)),
        new(new Location("zurich", 0, 0)),
    ];

    private IActorRef CreateManager() =>
        Sys.ActorOf(Props.Create(() => new PollManager(_njordOptions, _pollingOptions, _requirements, _time)), $"manager-{Guid.NewGuid():N}");

    private async Task<IActorRef> StartedManagerAsync(CancellationToken ct)
    {
        var manager = CreateManager();
        foreach (var _ in _targets)
        {
            await _region.ExpectMsgAsync<StartPolling>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        }

        return manager;
    }

    private PollStateEntry Entry(PollTarget target, PollPhase phase = PollPhase.Active,
        DateTimeOffset? lastChange = null) =>
        new(target.Location, phase, _time.GetUtcNow() + TimeSpan.FromMinutes(60), lastChange);

    private static async Task<PollStatesResult> QueryAsync(IActorRef manager, CancellationToken ct) =>
        await manager.Ask<PollStatesResult>(new QueryPollStates(), TimeSpan.FromSeconds(15), ct);

    [Fact(Timeout = 30_000)]
    public async Task The_query_before_any_report_lists_every_target_in_its_initial_state()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        var states = await QueryAsync(manager, ct);

        Assert.Equal(_targets.Length, states.Entries.Count);
        Assert.All(states.Entries, e => Assert.Multiple(
            () => Assert.Equal(PollPhase.Active, e.Phase),
            () => Assert.Equal(_time.GetUtcNow(), e.NextPollUtc),
            () => Assert.Null(e.LastChangeUtc)));
        Assert.Equal(_targets.Select(t => t.Location), states.Entries.Select(e => e.Location));
    }

    [Fact(Timeout = 30_000)]
    public async Task The_query_returns_the_latest_reported_state_of_each_target()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);
        var change = _time.GetUtcNow();
        var active = Entry(_targets[0], PollPhase.Active, lastChange: change);
        var unchanged = Entry(_targets[1], lastChange: change);

        manager.Tell(new PollWorkerReport(_targets[0], PollWorkerOutcome.Changed, active));
        manager.Tell(new PollWorkerReport(_targets[1], PollWorkerOutcome.Changed, unchanged));
        manager.Tell(new PollWorkerReport(_targets[1], PollWorkerOutcome.Unchanged, unchanged));

        var states = await QueryAsync(manager, ct);
        Assert.Multiple(
            () => Assert.Equal(active, states.Entries[0]),
            () => Assert.Equal(unchanged, states.Entries[1]));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_report_for_an_unknown_target_leaves_the_poll_states_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);
        var atlantis = new PollTarget(new Location("atlantis", 0, 0));

        manager.Tell(new PollWorkerReport(atlantis, PollWorkerOutcome.Changed, Entry(atlantis, lastChange: _time.GetUtcNow())));

        var states = await QueryAsync(manager, ct);
        Assert.Multiple(
            () => Assert.Equal(_targets.Length, states.Entries.Count),
            () => Assert.DoesNotContain(states.Entries, e => e.Location.Name == "atlantis"),
            () => Assert.All(states.Entries, e => Assert.Null(e.LastChangeUtc)));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_query_sends_nothing_to_the_region()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        await QueryAsync(manager, ct);

        await _region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_successful_poll_updates_the_poll_health_and_a_failure_does_not()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = await StartedManagerAsync(ct);

        manager.Tell(new PollWorkerReport(_targets[0], PollWorkerOutcome.Failed, Entry(_targets[0])));
        await QueryAsync(manager, ct);
        var afterFailure = await manager.Ask<PollHealthResult>(new QueryPollHealth(), TimeSpan.FromSeconds(3), ct);

        _time.Advance(TimeSpan.FromMinutes(1));
        manager.Tell(new PollWorkerReport(_targets[0], PollWorkerOutcome.Unchanged, Entry(_targets[0])));
        await QueryAsync(manager, ct);

        var afterSuccess = await manager.Ask<PollHealthResult>(new QueryPollHealth(), TimeSpan.FromSeconds(3), ct);
        Assert.Multiple(
            () => Assert.Null(afterFailure.LastSuccessfulPollUtc),
            () => Assert.Equal(_time.GetUtcNow(), afterSuccess.LastSuccessfulPollUtc));
    }
}
