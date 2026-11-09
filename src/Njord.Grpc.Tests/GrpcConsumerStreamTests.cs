using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Stream;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerStreamTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly TimeSpan _askTimeout = TimeSpan.FromSeconds(3);

    private readonly FakeTimeProvider _time = new(TestTimeConstants.BudgetEpoch);

    [Fact(Timeout = 30000)]
    public async Task An_entity_stream_request_hands_out_a_source_ref()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, _) = await StartAsync();

        var response = await consumer.Ask<RequestEntityStreamResponse>(new RequestEntityStream(7), _askTimeout, ct);

        var completed = Assert.IsType<RequestEntityStreamCompleted>(response);
        Assert.Equal(7, completed.RequestId);
    }

    [Fact(Timeout = 30000)]
    public async Task The_entity_stream_starts_with_a_snapshot_of_the_current_entity_states()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, pipeline) = await StartAsync();
        await pipeline.OfferAsync(EnrichmentEvents.Alerts(_vienna, _time.GetUtcNow()), ct);
        await AwaitAssertAsync(async () =>
        {
            var completed = Assert.IsType<RequestEntityStreamCompleted>(
                await consumer.Ask<RequestEntityStreamResponse>(new RequestEntityStream(1), _askTimeout, ct));
            var first = await completed.SourceRef.Source.RunWith(Sink.First<Proto.EntityEvent>(), Sys.Materializer());
            Assert.Equal(Proto.EntityEvent.EventOneofCase.Snapshot, first.EventCase);
            var state = Assert.Single(first.Snapshot.States);
            Assert.Equal("vienna_heat", state.EntityId);
        }, TimeSpan.FromSeconds(5), cancellationToken: ct);
    }

    [Fact(Timeout = 30000)]
    public async Task Later_results_are_delivered_as_state_updates()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, pipeline) = await StartAsync();
        var completed = Assert.IsType<RequestEntityStreamCompleted>(
            await consumer.Ask<RequestEntityStreamResponse>(new RequestEntityStream(1), _askTimeout, ct));
        var received = completed.SourceRef.Source
            .Where(e => e.EventCase == Proto.EntityEvent.EventOneofCase.StateUpdate)
            .RunWith(Sink.First<Proto.EntityEvent>(), Sys.Materializer());

        await AwaitAssertAsync(async () =>
        {
            await pipeline.OfferAsync(EnrichmentEvents.Alerts(_vienna, _time.GetUtcNow()), ct);
            await received.WaitAsync(TimeSpan.FromMilliseconds(500), ct);
        }, TimeSpan.FromSeconds(5), cancellationToken: ct);

        Assert.Equal("vienna_heat", (await received).StateUpdate.EntityId);
    }

    private async Task<(IActorRef Consumer, ISourceQueueWithComplete<PipelineEvent> Pipeline)> StartAsync()
    {
        var ready = new TaskCompletionSource<TestPipeline>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = Sys.ActorOf(Props.Create(() => new FakePipeline(Sys.Materializer(), ready)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var lookup = new EntityIdLookup([new EntityTestRule("Heat")], new NjordOptions { ResolvedLocations = [_vienna] });
        var consumer = Sys.ActorOf(Props.Create(() => new GrpcConsumer(lookup, _time)), $"grpc-consumer-{Guid.NewGuid():N}");
        var testPipeline = await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        return (consumer, testPipeline.Queue);
    }
}
