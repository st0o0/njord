using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

public sealed class PipelineActorShutdownSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stop_streams_completes_graph_without_abrupt_termination_when_ready()
    {
        var pipeline = await StartReadyPipeline();

        await EventFilter.Exception<AbruptTerminationException>().ExpectAsync(
            0,
            TimeSpan.FromSeconds(1),
            async () =>
            {
                var reply = await pipeline.Ask<object>(new StopStreams(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.IsType<StreamsStopped>(reply);

                await Sys.Terminate();
            },
            TestContext.Current.CancellationToken);
    }

    private Task<IActorRef> StartReadyPipeline()
    {
        var scheduler = Sys.ActorOf(Props.Create(() => new IdleActor()));
        ActorRegistry.Register<ISchedulerActor>(scheduler, overwrite: true);

        var pipeline = Sys.ActorOf(Props.Create(() => new PipelineActor(
            new FakeOpenMeteoClient(), new FakeTimeProvider(), new AllowAllGate(),
            DisabledAnalyzers.Consensus(),
            DisabledAnalyzers.Alert(), DisabledAnalyzers.Derived(),
            DisabledAnalyzers.Trend(), DisabledAnalyzers.Index())));

        return Task.FromResult(pipeline);
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
