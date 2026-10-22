using Akka.Actor;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

// The region registers after the manager started: the manager answers meanwhile and starts the workers later.
public sealed class PollManagerLateRegionSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();

    private readonly IOptions<NjordOptions> _njordOptions = Options.Create(new NjordOptions
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
    });

    private readonly IOptions<PollingOptions> _pollingOptions = Options.Create(AppSettingsDefaults.Options<PollingOptions>());

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Workers_are_started_once_the_region_registers()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = Sys.ActorOf(Props.Create(() => new PollManager(_njordOptions, _pollingOptions, _time)), "manager");

        var states = await manager.Ask<PollStatesResult>(new QueryPollStates(), TestTimeouts.RealTimeWaitMax, ct);
        var trigger = await manager.Ask<TriggerImmediatePollResponse>(
            new TriggerImmediatePoll(null), TestTimeouts.RealTimeWaitMax, ct);

        var region = CreateTestProbe("region");
        ActorRegistry.Register<IPollRegion>(region.Ref);

        var start = await region.ExpectMsgAsync<StartPolling>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Single(states.Entries),
            () => Assert.IsType<TriggerImmediatePollFailed>(trigger),
            () => Assert.Equal("lucerne", start.LocationName));
    }
}
