using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests.Helpers;

public sealed class SinkRefActorBoundaryTests : Akka.Hosting.TestKit.TestKit
{
    protected IMaterializer Mat => Sys.Materializer();

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_across_actor_boundary()
    {
        var received = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(Mat, received)));

        var response = await pipeline.Ask<SinkRefResponse>(
            new RequestSinkRef(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(response.SinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(42, value);
    }

    [Fact(Timeout = 5000)]
    public async Task OfferAsync_completes_across_actor_boundary_with_budget_throttle()
    {
        var received = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(Props.Create(() =>
            new ThrottledPipelineActor(Mat, received)));

        var response = await pipeline.Ask<SinkRefResponse>(
            new RequestSinkRef(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
            .To(response.SinkRef.Sink)
            .Run(Mat);

        var result = await queue.OfferAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(QueueOfferResult.Enqueued.Instance, result);

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(42, value);
    }
}
