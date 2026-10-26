using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.OpenMeteo.Gateway;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoGatewayAttachSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly FakeWeatherClient _weather = new();
    private readonly FakeAirQualityClient _airQuality = new();
    private readonly FakeFloodClient _flood = new();
    private readonly FakeMarineClient _marine = new();
    private readonly FakeSeasonalClient _seasonal = new();
    private readonly FakeSatelliteRadiationClient _satellite = new();
    private readonly SwitchableBudgetProvider _budget = new();
    private Akka.TestKit.TestProbe _apiCalls = null!;

    protected override bool UseTestPersistence => false;

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry) =>
        {
            _apiCalls = CreateTestProbe();
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new ForwardingActor(_apiCalls.Ref)), "budget"));
        });

    private IActorRef StartGateway()
    {
        var options = Options.Create(AppSettingsDefaults.Options<NjordOptions>(o =>
        {
            o.Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }];
        }));
        IReadOnlyDictionary<DomainKind, IDomainClient> clients = new IDomainClient[] { _weather, _airQuality, _flood, _marine, _seasonal, _satellite }
            .ToDictionary(c => c.Domain);
        return Sys.ActorOf(
            Props.Create(() => new OpenMeteoGateway(clients, _time, options, _budget, FetchTimeouts.Default)),
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

    private static FetchDomain Fetch() => new(new Location("lucerne", 0, 0), DomainKind.Weather);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_request_before_the_pipeline_input_is_attached_is_rejected_as_pipeline_unavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, attached, _) = RegisterPipeline(answers: false);
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        gateway.Tell(Fetch());

        var rejected = await ExpectMsgAsync<FetchDomainRejected>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(FetchRejectionReason.PipelineUnavailable, rejected.Reason);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_gateway_attaches_to_the_pipeline_only_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, attached, _) = RegisterPipeline();
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        gateway.Tell(Fetch());
        await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        await attached.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_gateway_restarts_and_attaches_again_after_the_pipeline_terminated()
    {
        var ct = TestContext.Current.CancellationToken;
        var (first, firstAttached, _) = RegisterPipeline();
        var gateway = StartGateway();
        await firstAttached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        var (_, secondAttached, secondForecasts) = RegisterPipeline();
        await first.GracefulStop(TestTimeouts.AwaitAssertMax);
        await secondAttached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        gateway.Tell(Fetch());

        await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var received = await secondForecasts.ExpectMsgAsync<DomainDataReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal("lucerne", received.Location.Name);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_exhausted_budget_rejects_the_request_without_fetching_or_recording()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, attached, _) = RegisterPipeline();
        _budget.CostPerMinute = 1;
        _budget.MaxBurst = 1;
        _weather.CallWeight = 2;
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        gateway.Tell(Fetch());

        var rejected = await ExpectMsgAsync<FetchDomainRejected>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(FetchRejectionReason.Budget, rejected.Reason),
            () => Assert.Equal(0, _weather.Calls));
        await _apiCalls.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), ct);
    }
}
