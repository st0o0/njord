using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerStreamingSpec : GrpcConsumerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_streaming_client_receives_pipeline_events_from_the_secondary_hub()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var probe = CreateTestProbe();

        var source = await RequestStreamAsync(readModel, ct);
        source.RunWith(Sink.ActorRef<PipelineEvent>(probe.Ref, "done", _ => "failed"), Sys.Materializer());
        var alert = EnrichmentEvents.Alerts(Lucerne, Anchor);

        await AwaitAssertAsync(async () =>
        {
            await pipeline.OfferAsync(alert, ct);
            await probe.ExpectMsgAsync<PipelineEvent.AlertRaised>(TimeSpan.FromMilliseconds(500), cancellationToken: ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stream_requested_before_the_first_subscription_is_answered_at_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreateTestProbe();
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);
        var readModel = Sys.ActorOf(Props.Create(() => new GrpcConsumer()), $"grpc-consumer-{Guid.NewGuid():N}");
        var request = await pipeline.ExpectMsgAsync<RequestPipelineOutput>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        var response = await readModel.Ask<RequestGrpcStreamResponse>(new RequestGrpcStream(7), AskTimeout, ct);

        var completed = Assert.IsType<RequestGrpcStreamCompleted>(response);
        Assert.Equal(7, completed.RequestId);
        var client = new EventCollector();
        _ = completed.SourceRef.Source.RunWith(client.AsSink(), Sys.Materializer());
        var (queue, source) = Source.Queue<PipelineEvent>(16, OverflowStrategy.Backpressure).PreMaterialize(Sys.Materializer());
        var sourceRef = await source.RunWith(StreamRefs.SourceRef<PipelineEvent>(), Sys.Materializer());
        readModel.Tell(new RequestPipelineOutputCompleted(request.RequestId, sourceRef), pipeline.Ref);
        await WarmUpAsync(queue, client, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_disconnected_streaming_client_does_not_affect_other_clients()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartAsync();
        var mat = Sys.Materializer();
        var probe = CreateTestProbe();

        var dead = await RequestStreamAsync(readModel, ct);
        var killSwitch = dead.ViaMaterialized(KillSwitches.Single<PipelineEvent>(), Keep.Right)
            .To(Sink.Ignore<PipelineEvent>())
            .Run(mat);
        killSwitch.Shutdown();

        var alive = await RequestStreamAsync(readModel, ct);
        alive.RunWith(Sink.ActorRef<PipelineEvent>(probe.Ref, "done", _ => "failed"), mat);
        var alert = EnrichmentEvents.Alerts(Lucerne, Anchor);

        await AwaitAssertAsync(async () =>
        {
            await pipeline.OfferAsync(alert, ct);
            await probe.ExpectMsgAsync<PipelineEvent.AlertRaised>(TimeSpan.FromMilliseconds(500), cancellationToken: ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }
}
