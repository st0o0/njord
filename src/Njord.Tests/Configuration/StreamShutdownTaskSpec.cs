using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Messages.Pipeline;
using Njord.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Tests.Configuration;

public sealed class StreamShutdownTaskSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Coordinated_shutdown_stops_both_streams_without_abrupt_termination()
    {
        var scheduler = Sys.ActorOf(Props.Create(() => new IdleActor()));
        ActorRegistry.Register<ISchedulerActor>(scheduler, overwrite: true);
        var pipeline = Sys.ActorOf(Props.Create(() => new PipelineActor(
            new FakeOpenMeteoClient(), new FakeTimeProvider(), new AllowAllGate())));
        ActorRegistry.Register<IPipelineActor>(pipeline, overwrite: true);
        await pipeline.Ask<PipelineSourceResponse>(
            new RequestPipelineSource(0), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        NjordActorSystemSetup.AddStreamShutdownTask(Sys, ActorRegistry, TimeSpan.FromSeconds(5));

        await EventFilter.Exception<AbruptTerminationException>().ExpectAsync(
            0,
            TimeSpan.FromSeconds(1),
            async () => await CoordinatedShutdown.Get(Sys).Run(CoordinatedShutdown.ClrExitReason.Instance),
            TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Unresponsive_actor_logs_warning_and_shutdown_still_completes()
    {
        var silent = Sys.ActorOf(Props.Create(() => new IdleActor()));
        ActorRegistry.Register<IPipelineActor>(silent, overwrite: true);

        NjordActorSystemSetup.AddStreamShutdownTask(Sys, ActorRegistry, TimeSpan.FromMilliseconds(300));

        await EventFilter.Warning(contains: "Failed to stop streams").ExpectAsync(
            1,
            TimeSpan.FromSeconds(10),
            async () => await CoordinatedShutdown.Get(Sys).Run(CoordinatedShutdown.ClrExitReason.Instance),
            TestContext.Current.CancellationToken);

        Assert.True(Sys.WhenTerminated.IsCompleted);
    }

    private sealed class AllowAllGate : IBudgetGate<WeightedTarget>
    {
        public bool TryAcquire(WeightedTarget element) => true;
        public TimeSpan EstimateDelay(WeightedTarget element) => TimeSpan.Zero;
    }

    private sealed class IdleActor : ReceiveActor
    {
        public IdleActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
