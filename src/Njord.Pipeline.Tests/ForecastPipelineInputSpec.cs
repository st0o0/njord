using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Compute.Consensus;
using Njord.Compute.Settings;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineInputSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private readonly AnalysisFixture _fixture = AnalysisFixture.Create();

    private IActorRef CreatePipeline()
    {
        var settings = AnalysisSettingsFactory.Create(
            new NjordOptions { Locations = [new LocationOptions { Name = AnalysisFixture.Lucerne }, new LocationOptions { Name = AnalysisFixture.Bern }] },
            new ConsensusOptions(), new AlertOptions(), new IndexOptions(), new TrendOptions { Enabled = true }, new DerivedOptions());

        return Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            _fixture.Time,
            new ConsensusBuilder(AnalysisFixture.Parameters, _fixture.Time),
            Options.Create(new ConsensusOptions()),
            DisabledAnalyzers.Alert(),
            DisabledAnalyzers.Derived(),
            new TrendAnalyzer(Options.Create(new TrendOptions { Enabled = true })),
            DisabledAnalyzers.Index(),
            settings)), $"pipeline-{Guid.NewGuid():N}");
    }

    private WeatherForecast Forecast(string location, int index = 0) =>
        _fixture.Forecasts.Where(f => f.Location == location).ElementAt(index);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_output_is_available_before_any_producer_attaches()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(
            new RequestPipelineOutput(7), TestTimeouts.RealTimeWaitMax, ct);

        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        Assert.Equal(7, completed.RequestId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_sink_request_is_answered_with_its_request_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();

        var response = await pipeline.Ask<RequestForecastSinkResponse>(
            new RequestForecastSink(5), TestTimeouts.RealTimeWaitMax, ct);

        var completed = Assert.IsType<RequestForecastSinkCompleted>(response);
        Assert.Equal(5, completed.RequestId);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Forecasts_of_two_producers_are_both_analyzed()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var first = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        var second = await PipelineProducer.AttachAsync(pipeline, Sys, ct);

        await first.OfferAsync(Forecast(AnalysisFixture.Lucerne), ct);
        var fromFirst = await NextForecastReceivedAsync(consumer, ct);
        await second.OfferAsync(Forecast(AnalysisFixture.Bern), ct);
        var fromSecond = await NextForecastReceivedAsync(consumer, ct);

        Assert.Multiple(
            () => Assert.Equal(AnalysisFixture.Lucerne, fromFirst.Location.Name),
            () => Assert.Equal(AnalysisFixture.Bern, fromSecond.Location.Name));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_completed_producer_does_not_end_the_hub_or_the_consumer_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var first = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await first.OfferAsync(Forecast(AnalysisFixture.Lucerne), ct);
        await NextForecastReceivedAsync(consumer, ct);

        first.Complete();
        await first.WatchCompletionAsync().WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        var second = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await second.OfferAsync(Forecast(AnalysisFixture.Bern), ct);

        var received = await NextForecastReceivedAsync(consumer, ct);
        Assert.Equal(AnalysisFixture.Bern, received.Location.Name);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_producer_does_not_end_the_hub_or_the_consumer_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var first = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await first.OfferAsync(Forecast(AnalysisFixture.Lucerne), ct);
        await NextForecastReceivedAsync(consumer, ct);

        first.Fail(new InvalidOperationException("producer failed"));
        var second = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await second.OfferAsync(Forecast(AnalysisFixture.Bern), ct);

        var received = await NextForecastReceivedAsync(consumer, ct);
        Assert.Equal(AnalysisFixture.Bern, received.Location.Name);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Trends_receive_the_previous_consensus_across_a_producer_change()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var first = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await first.OfferAsync(Forecast(AnalysisFixture.Lucerne, 0), ct);
        await consumer.ExpectMsgAsync<PipelineEvent.ForecastReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await consumer.ExpectMsgAsync<PipelineEvent.ConsensusComputed>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        first.Complete();
        var second = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await second.OfferAsync(Forecast(AnalysisFixture.Lucerne, 1), ct);

        await consumer.ExpectMsgAsync<PipelineEvent.ForecastReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await consumer.ExpectMsgAsync<PipelineEvent.ConsensusComputed>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var trend = await consumer.ExpectMsgAsync<PipelineEvent.TrendComputed>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(AnalysisFixture.Lucerne, trend.Result.Location.Name);
    }

    private static async Task<PipelineEvent.ForecastReceived> NextForecastReceivedAsync(Akka.TestKit.TestProbe consumer, CancellationToken ct)
    {
        var message = await consumer.FishForMessageAsync(
            m => m is PipelineEvent.ForecastReceived or ConsumerCompleted or ConsumerFailed,
            TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        return Assert.IsType<PipelineEvent.ForecastReceived>(message);
    }
}
