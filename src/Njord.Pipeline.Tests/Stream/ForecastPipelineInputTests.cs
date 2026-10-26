using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Features.Alerts.Frost;
using Njord.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineInputTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly DateTimeOffset Start = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private IActorRef CreatePipeline(IReadOnlyList<IFeatureRule>? features = null)
    {
        var time = new FakeTimeProvider(Start);
        var rules = features ?? FeatureServices.AllFeatureRules();
        return Sys.ActorOf(Props.Create(() => new ForecastPipeline(time, rules)), $"pipeline-{Guid.NewGuid():N}");
    }

    private static IReadOnlyList<IFeatureRule> AllFeaturesOff() =>
        [new FrostAlert(Options.Create(new FrostConfig { Enabled = false }))];

    private static DomainDataReceived Weather(Location location, double temperature = 10.0) =>
        DomainData.Received(location, DomainData.Weather(Start, 48, (_, p) => p with { Temperature = temperature }));

    [Fact(Timeout = 30000)]
    public async Task The_output_is_available_before_any_producer_attaches()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(
            new RequestPipelineOutput(7), TimeSpan.FromSeconds(10), ct);

        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        Assert.Equal(7, completed.RequestId);
    }

    [Fact(Timeout = 30000)]
    public async Task A_sink_request_is_answered_with_its_request_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();

        var response = await pipeline.Ask<RequestForecastSinkResponse>(
            new RequestForecastSink(5), TimeSpan.FromSeconds(10), ct);

        var completed = Assert.IsType<RequestForecastSinkCompleted>(response);
        Assert.Equal(5, completed.RequestId);
    }

    [Fact(Timeout = 30000)]
    public async Task Domain_data_of_two_producers_is_published_as_weather_events()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var first = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        var second = await PipelineProducer.AttachAsync(pipeline, Sys, ct);

        await first.OfferAsync(Weather(DomainData.Lucerne), ct);
        var fromFirst = await NextWeatherAsync(consumer, ct);
        await second.OfferAsync(Weather(DomainData.Bern), ct);
        var fromSecond = await NextWeatherAsync(consumer, ct);

        Assert.Multiple(
            () => Assert.Equal("Lucerne", fromFirst.Location.Name),
            () => Assert.Equal(48, fromFirst.Forecast.Points.Length),
            () => Assert.Equal("Bern", fromSecond.Location.Name));
    }

    [Fact(Timeout = 30000)]
    public async Task Weather_below_the_frost_threshold_raises_a_frost_alert()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var producer = await PipelineProducer.AttachAsync(pipeline, Sys, ct);

        await producer.OfferAsync(Weather(DomainData.Lucerne, temperature: -3.0), ct);

        var raised = await NextFrostAlertAsync(consumer, ct);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", raised.Location.Name),
            () => Assert.Equal(AlertSeverity.Yellow, raised.Severity),
            () => Assert.Equal(ConfidenceLevel.High, raised.Level));
    }

    [Fact(Timeout = 30000)]
    public async Task A_disabled_enrichment_raises_nothing_even_when_its_domain_data_is_present()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline(AllFeaturesOff());
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var producer = await PipelineProducer.AttachAsync(pipeline, Sys, ct);

        await producer.OfferAsync(Weather(DomainData.Lucerne, temperature: -3.0), ct);

        await NextWeatherAsync(consumer, ct);
        await consumer.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }

    [Fact(Timeout = 30000)]
    public async Task A_completed_producer_does_not_end_the_hub_or_the_consumer_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var first = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await first.OfferAsync(Weather(DomainData.Lucerne), ct);
        await NextWeatherAsync(consumer, ct);

        first.Complete();
        await first.WatchCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10), ct);
        var second = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await second.OfferAsync(Weather(DomainData.Bern), ct);

        var received = await NextWeatherAsync(consumer, ct);
        Assert.Equal("Bern", received.Location.Name);
    }

    [Fact(Timeout = 30000)]
    public async Task A_failed_producer_does_not_end_the_hub_or_the_consumer_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var first = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await first.OfferAsync(Weather(DomainData.Lucerne), ct);
        await NextWeatherAsync(consumer, ct);

        first.Fail(new InvalidOperationException("producer failed"));
        var second = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await second.OfferAsync(Weather(DomainData.Bern), ct);

        var received = await NextWeatherAsync(consumer, ct);
        Assert.Equal("Bern", received.Location.Name);
    }

    [Fact(Timeout = 30000)]
    public async Task A_producer_change_keeps_the_analysis_running_for_the_same_location()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipeline = CreatePipeline();
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var first = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await first.OfferAsync(Weather(DomainData.Lucerne, temperature: 10.0), ct);
        await NextWeatherAsync(consumer, ct);

        first.Complete();
        var second = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        await second.OfferAsync(Weather(DomainData.Lucerne, temperature: -4.0), ct);

        var raised = await NextFrostAlertAsync(consumer, ct);
        Assert.Equal("Lucerne", raised.Location.Name);
    }

    private static async Task<PipelineEvent.WeatherDataReceived> NextWeatherAsync(Akka.TestKit.TestProbe consumer, CancellationToken ct)
    {
        var message = await consumer.FishForMessageAsync(
            m => m is PipelineEvent.WeatherDataReceived or ConsumerCompleted or ConsumerFailed,
            TimeSpan.FromSeconds(10), cancellationToken: ct);
        return Assert.IsType<PipelineEvent.WeatherDataReceived>(message);
    }

    private static async Task<FrostAlertResult> NextFrostAlertAsync(Akka.TestKit.TestProbe consumer, CancellationToken ct)
    {
        var message = await consumer.FishForMessageAsync(
            m => m is FrostAlertResult or ConsumerCompleted or ConsumerFailed,
            TimeSpan.FromSeconds(10), cancellationToken: ct);
        return Assert.IsType<FrostAlertResult>(message);
    }
}
