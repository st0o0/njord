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
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class SchedulerActorRefFailureSpec : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));
    private Akka.TestKit.TestProbe _offerProbe = null!;

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var options = new NjordOptions
        {
            DiscoveryInterval = TimeSpan.FromMilliseconds(50),
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
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
    public async Task Recovers_after_pipeline_termination_with_new_ref()
    {
        var ct = TestContext.Current.CancellationToken;
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: ct);

        // Stop the pipeline actor to simulate failure
        var oldPipeline = ActorRegistry.Get<IPipelineActor>();
        await WatchAsync(oldPipeline);
        await oldPipeline.GracefulStop(TimeSpan.FromSeconds(2));
        await ExpectTerminatedAsync(oldPipeline, cancellationToken: ct);

        // Register a replacement pipeline actor
        var newPipeline = Sys.ActorOf(
            Props.Create(() => new FakePipelineActor(_offerProbe)), "pipeline-replacement");
        ActorRegistry.Register<IPipelineActor>(newPipeline, overwrite: true);

        // Scheduler should re-resolve and send targets to the new pipeline
        var target = await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: ct);
        Assert.Equal("lucerne", target.Location.Name);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stays_responsive_after_pipeline_termination()
    {
        var ct = TestContext.Current.CancellationToken;
        await _offerProbe.ExpectMsgAsync<WeightedTarget>(cancellationToken: ct);

        // Stop the pipeline actor
        var pipeline = ActorRegistry.Get<IPipelineActor>();
        await WatchAsync(pipeline);
        await pipeline.GracefulStop(TimeSpan.FromSeconds(2));
        await ExpectTerminatedAsync(pipeline, cancellationToken: ct);

        // Scheduler should still respond to queries while waiting for a new pipeline
        var states = await Scheduler.Ask<QueryPollStatesResponse>(
            new QueryPollStates(), TimeSpan.FromSeconds(2), ct);

        Assert.IsAssignableFrom<QueryPollStatesResponse>(states);
    }

    private sealed class FakePipelineActor : ReceiveActor
    {
        public FakePipelineActor(IActorRef probe)
        {
            Receive<WeightedTarget>(msg => probe.Tell(msg));
        }
    }
}
