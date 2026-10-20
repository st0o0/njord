using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerSpec : NjordActorSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    protected override bool UseTestPersistence => false;

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Queries_before_the_first_event_report_that_no_data_is_available()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, _) = await StartAsync();

        var domainData = await consumer.Ask<DomainDataResponse>(
            new QueryDomainData(DomainData.Lucerne, DomainKind.Weather), AskTimeout, ct);
        var analysis = await consumer.Ask<LatestAnalysisResponse>(
            new QueryLatestAnalysis(DomainData.Lucerne), AskTimeout, ct);

        Assert.Multiple(
            () => Assert.Equal(new DomainDataNotAvailable(DomainData.Lucerne, DomainKind.Weather), domainData),
            () => Assert.Equal(new LatestAnalysisNotAvailable(DomainData.Lucerne), analysis));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Pipeline_events_are_answered_by_later_queries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, pipeline) = await StartAsync();
        var weather = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));
        var alert = EnrichmentEvents.Alerts(DomainData.Lucerne, Anchor);

        await pipeline.OfferAsync(weather, ct);
        await pipeline.OfferAsync(alert, ct);

        await AwaitAssertAsync(async () =>
        {
            var domainData = await consumer.Ask<DomainDataResponse>(
                new QueryDomainData(DomainData.Lucerne, DomainKind.Weather), AskTimeout, ct);
            var analysis = await consumer.Ask<LatestAnalysisResponse>(
                new QueryLatestAnalysis(DomainData.Lucerne), AskTimeout, ct);
            Assert.Equal(new DomainDataResult(weather), domainData);
            var result = Assert.IsType<LatestAnalysisResult>(analysis);
            Assert.NotNull(result.Alerts);
            Assert.Equal([alert.Alert], result.Alerts.Alerts);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Lifestyle_events_are_answered_by_later_analysis_queries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, pipeline) = await StartAsync();
        var laundry = new PipelineEvent.LaundryScoreComputed(DomainData.Lucerne, 80, Anchor, 4);

        await pipeline.OfferAsync(laundry, ct);

        await AwaitAssertAsync(async () =>
        {
            var analysis = await consumer.Ask<LatestAnalysisResponse>(
                new QueryLatestAnalysis(DomainData.Lucerne), AskTimeout, ct);
            var result = Assert.IsType<LatestAnalysisResult>(analysis);
            var update = Assert.Single(result.Lifestyle ?? []);
            Assert.Null(result.Alerts);
            Assert.Equal(LifestyleKind.LaundryScore, update.Kind);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stream_request_hands_out_a_source_that_delivers_later_events()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, pipeline) = await StartAsync();
        var alert = EnrichmentEvents.Alerts(DomainData.Lucerne, Anchor);
        var response = await consumer.Ask<RequestGrpcStreamResponse>(new RequestGrpcStream(1), AskTimeout, ct);
        var completed = Assert.IsType<RequestGrpcStreamCompleted>(response);

        var received = completed.SourceRef.Source
            .RunWith(Sink.First<PipelineEvent>(), Sys.Materializer());
        await AwaitAssertAsync(async () =>
        {
            await pipeline.OfferAsync(alert, ct);
            await received.WaitAsync(TimeSpan.FromMilliseconds(500), ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        Assert.Equal(alert, await received);
    }

    private async Task<(IActorRef Consumer, ISourceQueueWithComplete<PipelineEvent> Pipeline)> StartAsync()
    {
        var ready = new TaskCompletionSource<TestPipeline>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeline = Sys.ActorOf(Props.Create(() => new FakePipeline(Sys.Materializer(), ready)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var consumer = Sys.ActorOf(Props.Create(() => new GrpcConsumer()), $"grpc-consumer-{Guid.NewGuid():N}");
        var testPipeline = await ready.Task.WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        return (consumer, testPipeline.Queue);
    }
}
