using Akka.Actor;
using Akka.Hosting;
using Njord.Core;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Forecast;
using Njord.OpenMeteo.Gateway;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoGatewayAttachTests : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeForecastClient _weather = new();
    private readonly FakeAirQualityClient _airQuality = new();
    private readonly FakeFloodClient _flood = new();
    private readonly FakeMarineClient _marine = new();
    private readonly FakeSeasonalClient _seasonal = new();
    private readonly FakeSatelliteRadiationClient _satellite = new();
    private readonly FakeEnsembleClient _ensemble = new();
    private readonly SwitchableBudgetProvider _budget = new();
    private Akka.TestKit.TestProbe _apiCalls = null!;
    private Akka.TestKit.TestProbe _region = null!;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry) =>
        {
            _apiCalls = CreateTestProbe();
            _region = CreateTestProbe();
            registry.Register<IPollRegion>(_region.Ref);
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new ForwardingActor(_apiCalls.Ref)), "budget"));
        });

    private IActorRef StartGateway()
    {
        IReadOnlyDictionary<DomainKind, IDomainClient> clients = new IDomainClient[] { _weather, _airQuality, _flood, _marine, _seasonal, _satellite, _ensemble }
            .ToDictionary(c => c.Domain);
        return Sys.ActorOf(
            Props.Create(() => new OpenMeteoGateway(clients, TimeProvider.System, _budget, FetchTimeouts.Default)),
            $"gateway-{Guid.NewGuid():N}");
    }

    private (IActorRef Pipeline, Akka.TestKit.TestProbe Attached, Akka.TestKit.TestProbe Forecasts) RegisterPipeline(bool answers = true)
    {
        var attached = CreateTestProbe();
        var forecasts = CreateTestProbe();
        var pipeline = Sys.ActorOf(Props.Create(() => new FakeForecastPipeline(attached.Ref, forecasts.Ref, answers)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);
        return (pipeline, attached, forecasts);
    }

    private static FetchDomain Fetch() => new(new Location("lucerne", 47.05, 8.31), DomainKind.Forecast);

    [Fact(Timeout = 30000)]
    public async Task A_request_before_the_pipeline_input_is_attached_is_rejected_as_pipeline_unavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, attached, _) = RegisterPipeline(answers: false);
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        gateway.Tell(Fetch());

        var rejected = await _region.ExpectMsgAsync<FetchDomainRejected>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(FetchRejectionReason.PipelineUnavailable, rejected.Reason);
    }

    [Fact(Timeout = 30000)]
    public async Task The_gateway_attaches_to_the_pipeline_only_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, attached, _) = RegisterPipeline();
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        gateway.Tell(Fetch());
        await _region.ExpectMsgAsync<FetchDomainCompleted>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        await attached.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = 30000)]
    public async Task The_gateway_restarts_and_attaches_again_after_the_pipeline_terminated()
    {
        var ct = TestContext.Current.CancellationToken;
        var (first, firstAttached, _) = RegisterPipeline();
        var gateway = StartGateway();
        await firstAttached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        var (_, secondAttached, secondForecasts) = RegisterPipeline();
        await first.GracefulStop(TimeSpan.FromSeconds(5));
        await secondAttached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        gateway.Tell(Fetch());

        await _region.ExpectMsgAsync<FetchDomainCompleted>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        var received = await secondForecasts.ExpectMsgAsync<DomainDataReceived>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal("lucerne", received.Location.Name);
    }

    [Fact(Timeout = 30000)]
    public async Task A_high_weight_request_succeeds_when_burst_is_raised_to_call_weight()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, attached, _) = RegisterPipeline();
        _budget.CostPerMinute = 6000;
        _budget.MaxBurst = 2;
        _weather.CallWeight = 2;
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        gateway.Tell(Fetch());

        var completed = await _region.ExpectMsgAsync<FetchDomainCompleted>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(DomainKind.Forecast, completed.Domain);
    }

    [Fact(Timeout = 30000)]
    public async Task Budget_gate_delays_consecutive_heavy_requests_without_rejection()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, attached, _) = RegisterPipeline();
        _budget.CostPerMinute = 6000;
        _budget.MaxBurst = 18;
        _weather.CallWeight = 18;
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        gateway.Tell(Fetch());
        gateway.Tell(Fetch());

        var first = await _region.ExpectMsgAsync<FetchDomainCompleted>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(DomainKind.Forecast, first.Domain);

        var second = await _region.ExpectMsgAsync<FetchDomainCompleted>(TimeSpan.FromSeconds(10), cancellationToken: ct);
        Assert.Equal(DomainKind.Forecast, second.Domain);
    }
}
