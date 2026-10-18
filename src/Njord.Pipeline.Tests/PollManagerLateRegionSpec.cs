using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Health;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

// The region registers after the manager started: the manager answers meanwhile and starts the workers later.
public sealed class PollManagerLateRegionSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(new NjordHealthMonitor(default, new BudgetHealthSnapshot(0, 0, 0, 0)));
        services.AddSingleton(Options.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        }));
        services.AddSingleton(Options.Create(new PollingOptions()));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Workers_are_started_once_the_region_registers()
    {
        var ct = TestContext.Current.CancellationToken;
        var manager = Sys.ActorOf(DependencyResolver.For(Sys).Props<PollManager>(), "manager");

        var states = await manager.Ask<PollStatesResult>(new QueryPollStates(), TestTimeouts.RealTimeWaitMax, ct);
        var trigger = await manager.Ask<TriggerImmediatePollResponse>(
            new TriggerImmediatePoll(null, ""), TestTimeouts.RealTimeWaitMax, ct);

        var region = CreateTestProbe("region");
        ActorRegistry.Register<IPollRegion>(region.Ref);

        var start = await region.ExpectMsgAsync<StartPolling>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Single(states.Entries),
            () => Assert.IsType<TriggerImmediatePollFailed>(trigger),
            () => Assert.Equal("lucerne|icon_d2", start.TargetId));
    }
}
