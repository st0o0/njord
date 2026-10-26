using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

// The region registers after the manager started: the manager answers meanwhile and starts the workers later.
public sealed class PollManagerLateRegionTests : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new FakeTimeProvider(TestTimeConstants.PipelineEpoch);

    private readonly IOptions<NjordOptions> _njordOptions = Options.Create(new NjordOptions
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
    });

    private readonly IOptions<PollingOptions> _pollingOptions = Options.Create(AppSettingsDefaults.Options<PollingOptions>());

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.AddTestPersistence();

    [Fact(Timeout = 30_000)]
    public async Task Workers_are_started_once_the_region_registers()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = Sys.ActorOf(Props.Create(() => new PollManager(_njordOptions, _pollingOptions, _time)), "manager");

        var states = await manager.Ask<PollStatesResult>(new QueryPollStates(), TimeSpan.FromSeconds(15), ct);
        var trigger = await manager.Ask<TriggerImmediatePollResponse>(
            new TriggerImmediatePoll(null), TimeSpan.FromSeconds(15), ct);

        var region = CreateTestProbe("region");
        ActorRegistry.Register<IPollRegion>(region.Ref);

        var start = await region.ExpectMsgAsync<StartPolling>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Single(states.Entries),
            () => Assert.IsType<TriggerImmediatePollFailed>(trigger),
            () => Assert.Equal("lucerne", start.LocationName));
    }
}
