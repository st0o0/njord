using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Actors;
using Njord.Domain.Weather;
using Njord.Messages.Common;
using Njord.Messages.Pipeline;
using Njord.Pipeline;
using Njord.Tests.Shared;

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

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stop_streams_sent_while_initializing_is_answered_once_ready()
    {
        var pipeline = Sys.ActorOf(Props.Create(() => new PipelineActor(
            new FakeOpenMeteoClient(), new FakeTimeProvider(), new AllowAllGate())));

        var pending = pipeline.Ask<object>(new StopStreams(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var scheduler = Sys.ActorOf(Props.Create(() => new IdleActor()));
        ActorRegistry.Register<ISchedulerActor>(scheduler, overwrite: true);

        var reply = await pending;
        Assert.IsType<StreamsStopped>(reply);
    }

    private async Task<IActorRef> StartReadyPipeline()
    {
        var scheduler = Sys.ActorOf(Props.Create(() => new IdleActor()));
        ActorRegistry.Register<ISchedulerActor>(scheduler, overwrite: true);

        var pipeline = Sys.ActorOf(Props.Create(() => new PipelineActor(
            new FakeOpenMeteoClient(), new FakeTimeProvider(), new AllowAllGate())));

        var source = await pipeline.Ask<PipelineSourceResponse>(
            new RequestPipelineSource(0), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _ = source.SourceRef.Source.RunWith(Sink.Ignore<FetchOutcome>(), Sys.Materializer());

        return pipeline;
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
