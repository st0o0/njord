using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Health;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using Servus.Akka;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class PipelineConnectionSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .AddTestTimefactor();
    }

    [Fact(Timeout = 30000)]
    public async Task Persistent_scheduler_connects_and_offers_through_full_pipeline()
    {
        var offered = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(
            Props.Create(() => new ProductionLikePipelineActor(offered)),
            "pipeline");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);

        var scheduler = Sys.ActorOf(
            Props.Create(() => new ProductionLikeSchedulerActor($"sched-{Guid.NewGuid():N}")),
            "scheduler");

        var value = await offered.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
        Assert.Equal(42, value);
    }

    [Fact(Timeout = 30000)]
    public async Task Real_scheduler_and_gateway_connect_and_poll()
    {
        var fetchCalled = new TaskCompletionSource<string>();

        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "test", Latitude = 47.0, Longitude = 8.3 }],
            Models = ["icon_d2"],
            DiscoveryInterval = TimeSpan.FromMinutes(20),
        };
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero));
        var health = new NjordHealthMonitor { ServiceStartedUtc = time.GetUtcNow() };

        var fakeGateway = Sys.ActorOf(
            Props.Create(() => new FakeGatewayActor(fetchCalled)), "gateway");
        ActorRegistry.Register<IForecastGateway>(fakeGateway, overwrite: true);

        var scheduler = Sys.ActorOf(
            Props.Create(() => new PollScheduler(
                Options.Create(options), time, health)),
            "real-scheduler");
        ActorRegistry.Register<IPollScheduler>(scheduler, overwrite: true);

        var location = await fetchCalled.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
        Assert.Equal("test", location);
    }

    private sealed class FakeGatewayActor : ReceiveActor
    {
        public FakeGatewayActor(TaskCompletionSource<string>? fetchCalled = null)
        {
            var mat = Context.Materializer();

            Receive<FetchForecast>(msg =>
            {
                fetchCalled?.TrySetResult(msg.Location);
                var hash = msg.Location.GetHashCode() ^ msg.ModelId.GetHashCode();
                Sender.Tell(new FetchForecastCompleted(msg.Location, msg.ModelId, hash));
            });

            Receive<RequestForecastStream>(msg =>
            {
                var (source, sink) = BroadcastHub.Sink<WeatherForecast>(bufferSize: 2)
                    .PreMaterialize(mat);

                source
                    .RunWith(StreamRefs.SourceRef<WeatherForecast>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new RequestForecastStreamCompleted(msg.RequestId, sr),
                        ex => new RequestForecastStreamFailed(msg.RequestId, ex));
            });
        }
    }

    private sealed class ProductionLikePipelineActor : ReceiveActor, IWithStash
    {
        public IStash Stash { get; set; } = null!;

        private sealed record PipelineReady;

        public ProductionLikePipelineActor(TaskCompletionSource<int> offered)
        {
            var mat = Context.Materializer();
            var blackhole = Context.ActorOf(Props.Create(() => new BlackholeActor()));

            var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
                .PreMaterialize(mat);

            var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
                .PreMaterialize(mat);

            mergeHubSource
                .SelectAsyncUnordered(2, x => Task.FromResult(x))
                .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                    _ => Akka.Streams.Supervision.Directive.Resume))
                .Buffer(32, OverflowStrategy.Backpressure)
                .To(broadcastHubSink)
                .Run(mat);

            broadcastHubSource
                .RunWith(Sink.ForEach<int>(x => offered.TrySetResult(x)), mat);

            Receive<RequestPipelineSink>(_ =>
            {
                StreamRefs.SinkRef<int>()
                    .To(mergeHubSink)
                    .Run(mat)
                    .PipeTo(Sender, Self,
                        sr => new SinkRefResponse(sr),
                        _ => null!);
            });
            Receive<RequestPipelineSource>(_ =>
            {
                broadcastHubSource
                    .RunWith(StreamRefs.SourceRef<int>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new SourceRefResponse(sr),
                        _ => null!);
            });
        }
    }

    private sealed class ProductionLikeSchedulerActor : ReceivePersistentActor
    {
        public override string PersistenceId { get; }

        private IMaterializer _mat = null!;
        private ISourceQueueWithComplete<int>? _queue;
        private bool _sourceReceived;

        private sealed record ConnectionEstablished;
        private sealed record OfferFailed(Exception Error);
        private sealed record FailureConsumerDone;
        private sealed record FailureConsumerBroke(Exception Cause);
        private sealed record Evt(int Value);

        public ProductionLikeSchedulerActor(string persistenceId)
        {
            PersistenceId = persistenceId;

            Recover<Evt>(_ => { });
            Recover<SnapshotOffer>(_ => { });

            WaitingForRefs();
        }

        protected override void PreStart()
        {
            _mat = Context.Materializer();
            var pipeline = Context.GetActor<IForecastPipeline>();
            pipeline.Tell(new RequestPipelineSink());
            pipeline.Tell(new RequestPipelineSource());
        }

        private void WaitingForRefs()
        {
            Command<SinkRefResponse>(response =>
            {
                _queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
                    .To(response.SinkRef.Sink)
                    .Run(_mat);
                TryConnect();
            });
            Command<SourceRefResponse>(response =>
            {
                response.SourceRef.Source
                    .Collect(x => x < 0, x => x)
                    .To(Sink.ActorRef<int>(Self, new FailureConsumerDone(),
                        ex => new FailureConsumerBroke(ex)))
                    .Run(_mat);

                _sourceReceived = true;
                TryConnect();
            });
            Command<SchedulePoll>(_ => Stash.Stash());
            Command<ConnectionEstablished>(_ => Stash.Stash());
        }

        private void TryConnect()
        {
            if (_queue is null || !_sourceReceived)
            {
                return;
            }

            Self.Tell(new SchedulePoll());
            Become(Connecting);
        }

        private sealed record SchedulePoll;

        private void Connecting()
        {
            Command<SchedulePoll>(_ =>
            {
                _queue!.OfferAsync(42).PipeTo(Self,
                    success: _ => new ConnectionEstablished(),
                    failure: ex => new OfferFailed(ex));
                Become(WaitingForConnection);
            });
            Command<ConnectionEstablished>(_ => Stash.Stash());
        }

        private void WaitingForConnection()
        {
            Command<ConnectionEstablished>(_ =>
            {
                Become(Ready);
                Stash.UnstashAll();
            });
            Command<OfferFailed>(msg =>
            {
                Self.Tell(new SchedulePoll());
                Become(Connecting);
            });
            Command<SchedulePoll>(_ => Stash.Stash());
        }

        private void Ready()
        {
            Command<SchedulePoll>(_ => { });
        }
    }

    private sealed record RequestPipelineSink;
    private sealed record SinkRefResponse(ISinkRef<int> SinkRef);
    private sealed record RequestPipelineSource;
    private sealed record SourceRefResponse(ISourceRef<int> SourceRef);

    private sealed class BlackholeActor : ReceiveActor
    {
        public BlackholeActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
