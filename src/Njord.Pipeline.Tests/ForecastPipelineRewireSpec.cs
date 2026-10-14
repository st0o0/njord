using System.Diagnostics.Metrics;
using System.Threading.Channels;
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
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineRewireSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private readonly AnalysisFixture _fixture = AnalysisFixture.Create();

    private IActorRef CreatePipeline(IActorRef gateway)
    {
        ActorRegistry.Register<IForecastGateway>(gateway, overwrite: true);
        var settings = AnalysisSettingsFactory.Create(
            new NjordOptions { Locations = [new LocationOptions { Name = AnalysisFixture.Lucerne }] },
            new ConsensusOptions(), new AlertOptions(), new IndexOptions(), new TrendOptions(), new DerivedOptions());

        return Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            _fixture.Time,
            new ConsensusBuilder(AnalysisFixture.Parameters, _fixture.Time),
            Options.Create(new ConsensusOptions()),
            DisabledAnalyzers.Alert(),
            DisabledAnalyzers.Derived(),
            DisabledAnalyzers.Trend(),
            DisabledAnalyzers.Index(),
            settings)), $"pipeline-{Guid.NewGuid():N}");
    }

    private WeatherForecast LucerneForecast() => _fixture.Forecasts.First(f => f.Location == AnalysisFixture.Lucerne);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_forecast_stream_is_requested_again_and_analyzed()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeGateway(Sys);
        var pipeline = CreatePipeline(gateway.Actor);
        var first = await gateway.NextStreamAsync("the first forecast stream", ct);
        await AttachConsumerAsync(pipeline, ct);

        first.Fail(new InvalidOperationException("forecast stream failed"));

        var second = await gateway.NextStreamAsync("a new forecast stream after the failure", ct);
        var probe = await AttachConsumerAsync(pipeline, ct);
        await second.OfferAsync(LucerneForecast(), ct);
        var received = await probe.ExpectMsgAsync<PipelineEvent.ForecastReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(AnalysisFixture.Lucerne, received.Location.Name);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_read_model_follows_the_pipeline_onto_its_new_forecast_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeGateway(Sys);
        var pipeline = CreatePipeline(gateway.Actor);
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);
        var readModel = Sys.ActorOf(Props.Create(() => new ForecastReadModel()), $"read-model-{Guid.NewGuid():N}");
        var first = await gateway.NextStreamAsync("the first forecast stream", ct);

        first.Fail(new InvalidOperationException("forecast stream failed"));

        var second = await gateway.NextStreamAsync("a new forecast stream after the failure", ct);
        var forecast = LucerneForecast();
        await AwaitAssertAsync(async () =>
        {
            await second.OfferAsync(forecast, ct);
            var response = await readModel.Ask<ForecastResponse>(
                new QueryForecast(new LocationId(AnalysisFixture.Lucerne), forecast.Model.Id), TimeSpan.FromSeconds(1), ct);
            Assert.Same(forecast, Assert.IsType<ForecastResult>(response).Forecast);
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(500), cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_completed_forecast_stream_is_requested_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeGateway(Sys);
        CreatePipeline(gateway.Actor);
        var first = await gateway.NextStreamAsync("the first forecast stream", ct);

        first.Complete();

        await gateway.NextStreamAsync("a new forecast stream after the completion", ct);
        Assert.Equal(2, gateway.Requests);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_rewire_of_the_forecast_stream_is_counted()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeGateway(Sys);
        var rewires = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Njord.Pipeline" && instrument.Name == "njord_pipeline_stream_rewires_total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref rewires, value));
        listener.Start();
        CreatePipeline(gateway.Actor);
        var first = await gateway.NextStreamAsync("the first forecast stream", ct);

        first.Fail(new InvalidOperationException("forecast stream failed"));

        await gateway.NextStreamAsync("a new forecast stream after the failure", ct);
        await AwaitAssertAsync(() => Assert.True(Interlocked.Read(ref rewires) >= 1,
            $"expected at least one counted rewire, got {Interlocked.Read(ref rewires)}"), TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_handshake_reply_to_another_request_is_ignored()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeGateway(Sys, sendStaleReplyFirst: true);
        var pipeline = CreatePipeline(gateway.Actor);
        var stream = await gateway.NextStreamAsync("the forecast stream of the current request", ct);
        var probe = await AttachConsumerAsync(pipeline, ct);

        await stream.OfferAsync(LucerneForecast(), ct);

        var received = await probe.ExpectMsgAsync<PipelineEvent.ForecastReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(AnalysisFixture.Lucerne, received.Location.Name);
    }

    private async Task<Akka.TestKit.TestProbe> AttachConsumerAsync(IActorRef pipeline, CancellationToken ct)
    {
        var output = await pipeline.Ask<RequestPipelineOutputResponse>(
            new RequestPipelineOutput(Random.Shared.NextInt64()), TestTimeouts.RealTimeWaitMax, ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var probe = CreateTestProbe();
        completed.SourceRef.Source
            .RunWith(Sink.ActorRef<PipelineEvent>(probe.Ref, "done", ex => ex), Sys.Materializer());
        return probe;
    }

    // Every RequestForecastStream gets its own queue-backed SourceRef. With sendStaleReplyFirst the first request is
    // preceded by a completed reply carrying another request id, whose stream must never be analyzed.
    private sealed class FakeGateway
    {
        private readonly Channel<ISourceQueueWithComplete<WeatherForecast>> _streams =
            Channel.CreateUnbounded<ISourceQueueWithComplete<WeatherForecast>>();

        private int _requests;

        public FakeGateway(ActorSystem system, bool sendStaleReplyFirst = false)
        {
            Actor = system.ActorOf(Props.Create(() => new GatewayActor(this, sendStaleReplyFirst)), $"gateway-{Guid.NewGuid():N}");
        }

        public IActorRef Actor { get; }

        public int Requests => Volatile.Read(ref _requests);

        public async Task<ISourceQueueWithComplete<WeatherForecast>> NextStreamAsync(string description, CancellationToken ct)
        {
            try
            {
                return await _streams.Reader.ReadAsync(ct).AsTask().WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(
                    $"Timed out after {TestTimeouts.RealTimeWaitMax} waiting for {description} ({Requests} stream requests so far)");
            }
        }

        private sealed class GatewayActor : ReceiveActor
        {
            public GatewayActor(FakeGateway owner, bool sendStaleReplyFirst)
            {
                var mat = Context.Materializer();
                var staleSent = !sendStaleReplyFirst;

                ReceiveAsync<RequestForecastStream>(async msg =>
                {
                    Interlocked.Increment(ref owner._requests);
                    var requester = Sender;
                    if (!staleSent)
                    {
                        staleSent = true;
                        var staleSource = Source.Queue<WeatherForecast>(16, OverflowStrategy.Backpressure)
                            .PreMaterialize(mat).Item2;
                        var staleRef = await staleSource.RunWith(StreamRefs.SourceRef<WeatherForecast>(), mat);
                        requester.Tell(new RequestForecastStreamCompleted(Guid.NewGuid(), staleRef));
                    }

                    var (queue, source) = Source.Queue<WeatherForecast>(16, OverflowStrategy.Backpressure).PreMaterialize(mat);
                    owner._streams.Writer.TryWrite(queue);
                    var sourceRef = await source.RunWith(StreamRefs.SourceRef<WeatherForecast>(), mat);
                    requester.Tell(new RequestForecastStreamCompleted(msg.RequestId, sourceRef));
                });
            }
        }
    }
}
