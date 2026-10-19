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

public sealed class GrpcConsumerBackpressureSpec : GrpcConsumerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_read_model_keeps_consuming_when_no_streaming_client_is_attached()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartWithPipelineHubAsync();
        var otherConsumer = await AttachOtherConsumerAsync(readModel, pipeline, ct);
        var events = CreateAlerts(50);

        await OfferAllAsync(pipeline.Queue, events, ct);

        await AssertReadModelAndOtherConsumerReceivedAllAsync(readModel, otherConsumer, events, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_streaming_client_that_does_not_pull_never_holds_up_the_read_model()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartWithPipelineHubAsync();
        var otherConsumer = await AttachOtherConsumerAsync(readModel, pipeline, ct);
        var idleClient = await RequestStreamAsync(readModel, ct);
        var events = CreateAlerts(300);

        await OfferAllAsync(pipeline.Queue, events, ct);

        await AssertReadModelAndOtherConsumerReceivedAllAsync(readModel, otherConsumer, events, ct);

        var client = new EventCollector();
        _ = idleClient.RunWith(client.AsSink(), Sys.Materializer());
        var received = await client.WaitUntilAsync(
            r => r.Count > 0 && Equals(r[^1], events[^1]), "the idle client to receive the last event", ct);

        Assert.Multiple(
            () => Assert.InRange(received.Count, 1, 256),
            () => Assert.Equal(events.TakeLast(received.Count), received));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_streaming_client_that_keeps_up_receives_every_event_in_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = await StartWithPipelineHubAsync();
        var client = new EventCollector();
        var clientSource = await RequestStreamAsync(readModel, ct);
        _ = clientSource.RunWith(client.AsSink(), Sys.Materializer());
        await WarmUpAsync(pipeline.Queue, client, ct);
        var events = CreateAlerts(50);

        await OfferAllAsync(pipeline.Queue, events, ct);

        var received = await client.WaitUntilAsync(r => r.Count >= events.Count, "50 events at the client", ct);
        Assert.Equal(events, received);
    }
}
