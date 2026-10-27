using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerStreamReplayTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    [Fact(Timeout = 30000)]
    public async Task Stream_replays_cached_domain_data_before_live_events()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, pipeline) = await StartAsync();
        var weather = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));

        await pipeline.OfferAsync(weather, ct);
        await AwaitAssertAsync(async () =>
        {
            var q = await consumer.Ask<DomainDataResponse>(
                new QueryDomainData(DomainData.Lucerne, DomainKind.Weather), AskTimeout, ct);
            Assert.IsType<DomainDataResult>(q);
        }, TimeSpan.FromSeconds(5), cancellationToken: ct);

        var response = await consumer.Ask<RequestGrpcStreamResponse>(new RequestGrpcStream(1), AskTimeout, ct);
        var completed = Assert.IsType<RequestGrpcStreamCompleted>(response);

        var first = await completed.SourceRef.Source
            .RunWith(Sink.First<PipelineEvent>(), Sys.Materializer())
            .WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Equal(weather, first);
    }

    [Fact(Timeout = 30000)]
    public async Task Stream_with_empty_state_receives_only_live_events()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, pipeline) = await StartAsync();

        var response = await consumer.Ask<RequestGrpcStreamResponse>(new RequestGrpcStream(1), AskTimeout, ct);
        var completed = Assert.IsType<RequestGrpcStreamCompleted>(response);

        var first = completed.SourceRef.Source
            .RunWith(Sink.First<PipelineEvent>(), Sys.Materializer());

        var alert = EnrichmentEvents.Alerts(DomainData.Lucerne, Anchor);
        await AwaitAssertAsync(async () =>
        {
            await pipeline.OfferAsync(alert, ct);
            await first.WaitAsync(TimeSpan.FromMilliseconds(500), ct);
        }, TimeSpan.FromSeconds(5), cancellationToken: ct);

        Assert.Equal(alert, await first);
    }

    [Fact(Timeout = 30000)]
    public async Task Stream_replays_all_cached_domains_and_results()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, pipeline) = await StartAsync();

        var weather = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));
        var flood = new PipelineEvent.FloodDataReceived(DomainData.Lucerne, DomainData.Flood(Anchor));
        var alert = EnrichmentEvents.Alerts(DomainData.Lucerne, Anchor);

        await pipeline.OfferAsync(weather, ct);
        await pipeline.OfferAsync(flood, ct);
        await pipeline.OfferAsync(alert, ct);

        await AwaitAssertAsync(async () =>
        {
            var q = await consumer.Ask<LatestAnalysisResponse>(
                new QueryLatestAnalysis(DomainData.Lucerne), AskTimeout, ct);
            Assert.IsType<LatestAnalysisResult>(q);
        }, TimeSpan.FromSeconds(5), cancellationToken: ct);

        var response = await consumer.Ask<RequestGrpcStreamResponse>(new RequestGrpcStream(1), AskTimeout, ct);
        var completed = Assert.IsType<RequestGrpcStreamCompleted>(response);

        var replayed = await completed.SourceRef.Source
            .Take(3)
            .RunWith(Sink.Seq<PipelineEvent>(), Sys.Materializer())
            .WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Multiple(
            () => Assert.Equal(3, replayed.Count),
            () => Assert.Contains(weather, replayed),
            () => Assert.Contains(flood, replayed),
            () => Assert.Contains(alert, replayed));
    }

    private async Task<(IActorRef Consumer, ISourceQueueWithComplete<PipelineEvent> Pipeline)> StartAsync()
    {
        var ready = new TaskCompletionSource<TestPipeline>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = Sys.ActorOf(Props.Create(() => new FakePipeline(Sys.Materializer(), ready)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var consumer = Sys.ActorOf(Props.Create(() => new GrpcConsumer()), $"grpc-consumer-{Guid.NewGuid():N}");
        var testPipeline = await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        return (consumer, testPipeline.Queue);
    }
}
