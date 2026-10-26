using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Grpc;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.OpenMeteo;
using Njord.OpenMeteo.Budget;
using Njord.OpenMeteo.Gateway;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

// The seam this change exists for: a restarting gateway must not rebuild the pipeline, so its analysis state
// and every consumer's SourceRef survive.
public sealed class GatewayRestartSpec : NjordActorSpec
{
    private const string Location = "lucerne";

    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly FakeWeatherClient _weather = new();
    private readonly FakeAirQualityClient _airQuality = new();
    private readonly FakeFloodClient _flood = new();
    private readonly FakeMarineClient _marine = new();
    private readonly FakeSeasonalClient _seasonal = new();
    private readonly FakeSatelliteRadiationClient _satellite = new();
    private readonly FaultyOnceBudgetProvider _budget = new();

    protected override bool UseTestPersistence => false;

    // Plays the poll shard region: the gateway sends every reply here.
    private Akka.TestKit.TestProbe _region = null!;

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((_, _) => _region = CreateTestProbe());

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_gateway_restart_keeps_the_consumer_stream_open()
    {
        var ct = TestContext.Current.CancellationToken;
        ActorRegistry.Register<IBudgetTracker>(Sys.ActorOf(Props.Create(() => new SilentActor()), "budget"), overwrite: true);
        ActorRegistry.Register<IPollRegion>(_region.Ref, overwrite: true);
        var pipeline = StartPipeline();
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var gateway = StartGateway();

        await FetchUntilCompletedAsync(gateway, ct);
        await consumer.ExpectMsgAsync<PipelineEvent.WeatherDataReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        // The budget read fault fails the gateway's private stream; the gateway fails and is restarted.
        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));
        // The fault is thrown in the fetch stage; it fails the stream and the restart drops the request without a reply.
        gateway.Tell(new FetchDomain(new Location(Location, 47.05, 8.31), DomainKind.Weather));

        await FetchUntilCompletedAsync(gateway, ct);

        var next = await consumer.FishForMessageAsync(
            m => m is PipelineEvent.WeatherDataReceived or ConsumerCompleted or ConsumerFailed,
            TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var weather = Assert.IsType<PipelineEvent.WeatherDataReceived>(next);
        Assert.Equal(Location, weather.Location.Name);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_gateway_restart_does_not_make_the_read_model_attach_again()
    {
        var ct = TestContext.Current.CancellationToken;
        ActorRegistry.Register<IBudgetTracker>(Sys.ActorOf(Props.Create(() => new SilentActor()), "budget"), overwrite: true);
        ActorRegistry.Register<IPollRegion>(_region.Ref, overwrite: true);
        var outputRequests = 0;
        var pipeline = StartPipeline();
        var proxy = Sys.ActorOf(Props.Create(() => new CountingProxy(pipeline, () => Interlocked.Increment(ref outputRequests))),
            $"pipeline-proxy-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(proxy, overwrite: true);
        Sys.ActorOf(Props.Create(() => new GrpcConsumer()), $"grpc-consumer-{Guid.NewGuid():N}");
        var gateway = StartGateway();
        await AwaitAssertAsync(() => Assert.Equal(1, Volatile.Read(ref outputRequests)),
            TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await FetchUntilCompletedAsync(gateway, ct);

        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));
        // The fault is thrown in the fetch stage; it fails the stream and the restart drops the request without a reply.
        gateway.Tell(new FetchDomain(new Location(Location, 47.05, 8.31), DomainKind.Weather));
        await FetchUntilCompletedAsync(gateway, ct);

        Assert.Equal(1, Volatile.Read(ref outputRequests));
    }

    // Until the restarted gateway has re-attached it rejects with PipelineUnavailable; it is asked again then.
    // Replies go to the poll region, a probe here.
    private async Task FetchUntilCompletedAsync(IActorRef gateway, CancellationToken ct) =>
        await AwaitAssertAsync(async () =>
        {
            gateway.Tell(new FetchDomain(new Location(Location, 47.05, 8.31), DomainKind.Weather));
            var reply = await _region.ExpectMsgAsync<FetchDomainResponse>(TimeSpan.FromSeconds(1), cancellationToken: ct);
            Assert.IsType<FetchDomainCompleted>(reply);
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(200), cancellationToken: ct);

    private IActorRef StartGateway()
    {
        IReadOnlyDictionary<DomainKind, IDomainClient> clients = new Dictionary<DomainKind, IDomainClient>
        {
            [DomainKind.Weather] = _weather,
            [DomainKind.AirQuality] = _airQuality,
            [DomainKind.Flood] = _flood,
            [DomainKind.Marine] = _marine,
            [DomainKind.Seasonal] = _seasonal,
            [DomainKind.Satellite] = _satellite,
        };
        return Sys.ActorOf(
            Props.Create(() => new OpenMeteoGateway(clients, _time, _budget, FetchTimeouts.Default)),
            $"gateway-{Guid.NewGuid():N}");
    }

    private IActorRef StartPipeline()
    {
        var rules = FeatureServices.AllFeatureRules();
        return Sys.ActorOf(Props.Create(() => new ForecastPipeline(_time, rules)), $"pipeline-{Guid.NewGuid():N}");
    }

    private sealed class FaultyOnceBudgetProvider : IBudgetProvider
    {
        public volatile bool ThrowOnNextRead;

        public BudgetRate GetCurrentRate()
        {
            if (ThrowOnNextRead)
            {
                ThrowOnNextRead = false;
                throw new InvalidOperationException("simulated budget read fault");
            }

            return new BudgetRate(480, 1_000_000);
        }
    }

    // Stands in for the singleton proxy: forwards everything to the pipeline and counts SourceRef requests.
    private sealed class CountingProxy : ReceiveActor
    {
        public CountingProxy(IActorRef pipeline, Action onOutputRequest)
        {
            Receive<RequestPipelineOutput>(msg =>
            {
                onOutputRequest();
                pipeline.Forward(msg);
            });
            ReceiveAny(msg => pipeline.Forward(msg));
        }
    }

    private sealed class SilentActor : ReceiveActor
    {
        public SilentActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
