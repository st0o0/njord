using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class ForecastReadModelRewireSpec : NjordActorSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);
    private static readonly LocationId Lucerne = new("lucerne");

    protected override bool UseTestPersistence => false;

    private (IActorRef ReadModel, FakePipelineOutput Pipeline) Start()
    {
        var pipeline = new FakePipelineOutput(Sys);
        ActorRegistry.Register<IForecastPipeline>(pipeline.Actor, overwrite: true);
        var readModel = Sys.ActorOf(Props.Create(() => new ForecastReadModel()), $"read-model-{Guid.NewGuid():N}");
        return (readModel, pipeline);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_read_model_keeps_its_state_and_takes_new_events_after_the_subscription_failed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = Start();
        var before = EnrichmentEvents.Alerts(Lucerne, Anchor);
        var after = EnrichmentEvents.Alerts(Lucerne, Anchor.AddHours(1));
        var first = await pipeline.NextSubscriptionAsync("the first subscription", ct);
        await first.OfferAsync(before, ct);
        await AwaitAlertsAsync(readModel, before, ct);

        first.Fail(new InvalidOperationException("pipeline output failed"));

        var during = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);
        Assert.Equal(before, Assert.IsType<LatestAnalysisResult>(during).Alerts);
        var second = await pipeline.NextSubscriptionAsync("a new subscription after the failure", ct);
        await second.OfferAsync(after, ct);
        await AwaitAlertsAsync(readModel, after, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_streaming_client_keeps_its_stream_across_a_rewire()
    {
        var ct = TestContext.Current.CancellationToken;
        var (readModel, pipeline) = Start();
        var first = await pipeline.NextSubscriptionAsync("the first subscription", ct);
        var probe = CreateTestProbe();
        var client = await RequestStreamAsync(readModel, ct);
        client.RunWith(Sink.ActorRef<PipelineEvent>(probe.Ref, "client-completed", ex => $"client-failed: {ex.Message}"),
            Sys.Materializer());
        await OfferUntilReceivedAsync(first, probe, Anchor, ct);

        first.Complete();

        var second = await pipeline.NextSubscriptionAsync("a new subscription after the completion", ct);
        var afterRewire = EnrichmentEvents.Alerts(Lucerne, Anchor.AddHours(2));
        await second.OfferAsync(afterRewire, ct);
        var received = await probe.FishForMessageAsync(
            m => m is string || Equals(m, afterRewire), TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(afterRewire, received);
    }

    // A client attaches to the secondary hub asynchronously: offer until it sees one event.
    private async Task OfferUntilReceivedAsync(
        ISourceQueueWithComplete<PipelineEvent> queue, Akka.TestKit.TestProbe probe, DateTimeOffset at, CancellationToken ct)
    {
        await AwaitAssertAsync(async () =>
        {
            await queue.OfferAsync(EnrichmentEvents.Alerts(Lucerne, at), ct);
            await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(TimeSpan.FromMilliseconds(300), cancellationToken: ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    private async Task AwaitAlertsAsync(IActorRef readModel, PipelineEvent.AlertComputed expected, CancellationToken ct)
    {
        await AwaitAssertAsync(async () =>
        {
            var response = await readModel.Ask<LatestAnalysisResponse>(new QueryLatestAnalysis(Lucerne), AskTimeout, ct);
            Assert.Equal(expected, Assert.IsType<LatestAnalysisResult>(response).Alerts);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    private async Task<Source<PipelineEvent, Akka.NotUsed>> RequestStreamAsync(IActorRef readModel, CancellationToken ct)
    {
        RequestReadModelStreamCompleted? completed = null;
        var attempt = 0;
        await AwaitAssertAsync(async () =>
        {
            var response = await readModel.Ask<RequestReadModelStreamResponse>(new RequestReadModelStream(attempt++), AskTimeout, ct);
            completed = Assert.IsType<RequestReadModelStreamCompleted>(response);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        return Assert.IsType<RequestReadModelStreamCompleted>(completed).SourceRef.Source;
    }
}
