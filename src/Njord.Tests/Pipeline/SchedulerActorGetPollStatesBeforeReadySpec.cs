using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Pipeline;
using Njord.Tests.Shared;
using Servus.Akka;

namespace Njord.Tests.Pipeline;

public sealed class SchedulerActorGetPollStatesBeforeReadySpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            DiscoveryInterval = TimeSpan.FromMilliseconds(50),
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
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
                var silentPipeline = system.ActorOf(
                    Props.Create(() => new SilentPipelineActor()));
                registry.Register<PipelineActor>(silentPipeline);
            })
            .WithResolvableActors(r =>
            {
                r.Register<SchedulerActor>("scheduler");
            });
    }

    private IActorRef Scheduler => ActorRegistry.Get<SchedulerActor>();

    [Fact(Timeout = 5000)]
    public async Task Get_poll_states_responds_while_waiting_for_pipeline_refs()
    {
        var snapshot = await Scheduler.Ask<PollStatesSnapshot>(
            new GetPollStates(), TimeSpan.FromSeconds(2));

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Entries);
    }

    private sealed class SilentPipelineActor : ReceiveActor
    {
        public SilentPipelineActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
