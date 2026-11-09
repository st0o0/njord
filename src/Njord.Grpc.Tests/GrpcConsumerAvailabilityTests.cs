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

public sealed class GrpcConsumerAvailabilityTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly TimeSpan _askTimeout = TimeSpan.FromSeconds(3);

    private readonly FakeTimeProvider _time = new(TestTimeConstants.BudgetEpoch);

    [Fact]
    public void Entities_older_than_the_threshold_become_stale()
    {
        var alert = EnrichmentEvents.Alerts(_vienna, _time.GetUtcNow());
        var state = GrpcConsumerState.Empty.ApplyEntity("vienna_heat", alert, _time.GetUtcNow());

        var (next, unavailable, available) = state.CheckAvailability(
            _time.GetUtcNow() + GrpcConsumer.StaleThreshold + TimeSpan.FromSeconds(1), GrpcConsumer.StaleThreshold);

        Assert.Multiple(
            () => Assert.Equal(["vienna_heat"], unavailable),
            () => Assert.Empty(available),
            () => Assert.Contains("vienna_heat", next.StaleEntities),
            () => Assert.False(next.EntityStates["vienna_heat"].IsAvailable));
    }

    [Fact]
    public void Fresh_entities_stay_available()
    {
        var alert = EnrichmentEvents.Alerts(_vienna, _time.GetUtcNow());
        var state = GrpcConsumerState.Empty.ApplyEntity("vienna_heat", alert, _time.GetUtcNow());

        var (next, unavailable, available) = state.CheckAvailability(
            _time.GetUtcNow() + TimeSpan.FromMinutes(10), GrpcConsumer.StaleThreshold);

        Assert.Multiple(
            () => Assert.Empty(unavailable),
            () => Assert.Empty(available),
            () => Assert.Empty(next.StaleEntities));
    }

    [Fact]
    public void A_refreshed_stale_entity_recovers()
    {
        var alert = EnrichmentEvents.Alerts(_vienna, _time.GetUtcNow());
        var late = _time.GetUtcNow() + GrpcConsumer.StaleThreshold + TimeSpan.FromMinutes(1);
        var (stale, _, _) = GrpcConsumerState.Empty
            .ApplyEntity("vienna_heat", alert, _time.GetUtcNow())
            .CheckAvailability(late, GrpcConsumer.StaleThreshold);

        var (next, unavailable, available) = stale
            .ApplyEntity("vienna_heat", alert, late)
            .CheckAvailability(late, GrpcConsumer.StaleThreshold);

        Assert.Multiple(
            () => Assert.Empty(unavailable),
            () => Assert.Equal(["vienna_heat"], available),
            () => Assert.Empty(next.StaleEntities),
            () => Assert.True(next.EntityStates["vienna_heat"].IsAvailable));
    }

    [Fact(Timeout = 30000)]
    public async Task The_consumer_publishes_unavailable_when_an_entity_goes_stale()
    {
        var ct = TestContext.Current.CancellationToken;
        var ready = new TaskCompletionSource<TestPipeline>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipelineActor = Sys.ActorOf(Props.Create(() => new FakePipeline(Sys.Materializer(), ready)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipelineActor, overwrite: true);
        var lookup = new EntityIdLookup([new EntityTestRule("Heat")], new NjordOptions { ResolvedLocations = [_vienna] });
        var consumer = Sys.ActorOf(Props.Create(() => new GrpcConsumer(lookup, _time)), $"grpc-consumer-{Guid.NewGuid():N}");
        var pipeline = (await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct)).Queue;

        var completed = Assert.IsType<RequestEntityStreamCompleted>(
            await consumer.Ask<RequestEntityStreamResponse>(new RequestEntityStream(1), _askTimeout, ct));
        var unavailable = completed.SourceRef.Source
            .Where(e => e.EventCase == V3.EntityEvent.EventOneofCase.Unavailable)
            .RunWith(Sink.First<V3.EntityEvent>(), Sys.Materializer());

        await pipeline.OfferAsync(EnrichmentEvents.Alerts(_vienna, _time.GetUtcNow()), ct);
        await AwaitAssertAsync(async () =>
        {
            var snapshot = Assert.IsType<RequestEntityStreamCompleted>(
                await consumer.Ask<RequestEntityStreamResponse>(new RequestEntityStream(2), _askTimeout, ct));
            var first = await snapshot.SourceRef.Source.RunWith(Sink.First<V3.EntityEvent>(), Sys.Materializer());
            Assert.Single(first.Snapshot.States);
        }, TimeSpan.FromSeconds(5), cancellationToken: ct);

        _time.Advance(GrpcConsumer.StaleThreshold + TimeSpan.FromMinutes(1));
        consumer.Tell(new GrpcConsumer.CheckAvailability());

        Assert.Equal("vienna_heat", (await unavailable.WaitAsync(TimeSpan.FromSeconds(5), ct)).Unavailable.EntityId);
    }
}
