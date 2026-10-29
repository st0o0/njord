using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Grpc;
using Njord.Messages.Forecast;
using Njord.Messages.Stream;
using Njord.OpenMeteo.Budget;
using Njord.OpenMeteo.Gateway;
using Njord.Pipeline.Stream;
using Njord.Pipeline.Tests.Helpers;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests.Stream;

// The seam this change exists for: a faulting gateway must not rebuild the pipeline, so its analysis state
// and every consumer's SourceRef survive.
public sealed class GatewayRestartTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    private readonly FakeTimeProvider _time = new FakeTimeProvider(TestTimeConstants.PipelineEpoch);
    private readonly FakeForecastClient _weather = new();
    private readonly FakeAirQualityClient _airQuality = new();
    private readonly FakeFloodClient _flood = new();
    private readonly FakeMarineClient _marine = new();
    private readonly FakeSeasonalClient _seasonal = new();
    private readonly FakeSatelliteRadiationClient _satellite = new();
    private readonly FakeEnsembleClient _ensemble = new();
    private readonly FaultyOnceBudgetProvider _budget = new();

    [Fact(Timeout = 30000)]
    public async Task A_gateway_fault_keeps_the_consumer_stream_open()
    {
        var ct = TestContext.Current.CancellationToken;
        var region = CreateTestProbe();
        ActorRegistry.Register<IPollRegion>(region.Ref, overwrite: true);
        ActorRegistry.Register<IBudgetTracker>(Sys.ActorOf(Props.Create(() => new SilentActor()), "budget"), overwrite: true);
        var pipeline = StartPipeline();
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);
        var consumer = CreateTestProbe();
        await PipelineProducer.AttachConsumerAsync(pipeline, Sys, consumer, ct);
        var gateway = StartGateway();

        await FetchUntilCompletedAsync(gateway, region, ct);
        await consumer.ExpectMsgAsync<ForecastDataReceived>(TimeSpan.FromSeconds(10), cancellationToken: ct);

        // The budget read fault is thrown inside the gate stage: supervision drops that one request without a reply.
        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));
        gateway.Tell(new FetchDomain(Lucerne, DomainKind.Forecast));

        await FetchUntilCompletedAsync(gateway, region, ct);

        var next = await consumer.FishForMessageAsync(
            m => m is ForecastDataReceived or ConsumerCompleted or ConsumerFailed,
            TimeSpan.FromSeconds(10), cancellationToken: ct);
        var weather = Assert.IsType<ForecastDataReceived>(next);
        Assert.Equal(Lucerne.Name, weather.Location.Name);
    }

    [Fact(Timeout = 30000)]
    public async Task A_gateway_fault_does_not_make_the_read_model_attach_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var region = CreateTestProbe();
        ActorRegistry.Register<IPollRegion>(region.Ref, overwrite: true);
        ActorRegistry.Register<IBudgetTracker>(Sys.ActorOf(Props.Create(() => new SilentActor()), "budget"), overwrite: true);
        var outputRequests = 0;
        var pipeline = StartPipeline();
        var proxy = Sys.ActorOf(Props.Create(() => new CountingProxy(pipeline, () => Interlocked.Increment(ref outputRequests))),
            $"pipeline-proxy-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(proxy, overwrite: true);
        Sys.ActorOf(Props.Create(() => new GrpcConsumer()), $"grpc-consumer-{Guid.NewGuid():N}");
        var gateway = StartGateway();
        await AwaitAssertAsync(() => Assert.Equal(1, Volatile.Read(ref outputRequests)),
            TimeSpan.FromSeconds(10), cancellationToken: ct);
        await FetchUntilCompletedAsync(gateway, region, ct);

        _budget.ThrowOnNextRead = true;
        _time.Advance(TimeSpan.FromSeconds(6));
        gateway.Tell(new FetchDomain(Lucerne, DomainKind.Forecast));
        await FetchUntilCompletedAsync(gateway, region, ct);

        Assert.Equal(1, Volatile.Read(ref outputRequests));
    }

    // Until the gateway has attached it rejects with PipelineUnavailable; it is asked again then.
    private async Task FetchUntilCompletedAsync(IActorRef gateway, Akka.TestKit.TestProbe region, CancellationToken ct) =>
        await AwaitAssertAsync(async () =>
        {
            gateway.Tell(new FetchDomain(Lucerne, DomainKind.Forecast));
            var reply = await region.ExpectMsgAsync<FetchDomainResponse>(TimeSpan.FromSeconds(10), cancellationToken: ct);
            Assert.IsType<FetchDomainCompleted>(reply);
        }, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200), cancellationToken: ct);

    private IActorRef StartGateway()
    {
        IReadOnlyDictionary<DomainKind, IDomainClient> clients = new Dictionary<DomainKind, IDomainClient>
        {
            [DomainKind.Forecast] = _weather,
            [DomainKind.AirQuality] = _airQuality,
            [DomainKind.Flood] = _flood,
            [DomainKind.Marine] = _marine,
            [DomainKind.Seasonal] = _seasonal,
            [DomainKind.Satellite] = _satellite,
            [DomainKind.Ensemble] = _ensemble,
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
