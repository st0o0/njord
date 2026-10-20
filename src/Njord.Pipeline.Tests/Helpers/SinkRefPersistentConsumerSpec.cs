using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;

namespace Njord.Pipeline.Tests;

public sealed class SinkRefPersistentConsumerSpec : SinkRefTestKit
{
    [Fact(Timeout = 5000)]
    public async Task Persistent_actor_can_offer_after_recovery_via_sinkref()
    {
        var received = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(Mat, received)));

        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var consumer = Sys.ActorOf(Props.Create(() =>
            new PersistentConsumerActor($"consumer-{Guid.NewGuid():N}")));

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(99, value);
    }

    [Fact(Timeout = 5000)]
    public async Task Persistent_actor_with_recovery_events_can_offer_via_sinkref()
    {
        var persistenceId = $"consumer-{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(Mat, received)));

        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var first = Sys.ActorOf(Props.Create(() =>
            new PersistentConsumerActor(persistenceId)));

        await received.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        received = new TaskCompletionSource<int>();

        await first.GracefulStop(TimeSpan.FromSeconds(2));

        var pipeline2 = Sys.ActorOf(Props.Create(() =>
            new FakePipelineWithFullGraph(Mat, received)));
        ActorRegistry.Register<IForecastPipeline>(pipeline2, overwrite: true);

        var second = Sys.ActorOf(Props.Create(() =>
            new PersistentConsumerActor(persistenceId)));

        var value = await received.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(99, value);
    }
}
