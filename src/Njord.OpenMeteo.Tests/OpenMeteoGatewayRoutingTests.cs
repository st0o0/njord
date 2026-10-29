using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Polling;
using Njord.Core.Seasonal;
using Njord.Core.Solar;
using Njord.Messages.Forecast;
using Njord.OpenMeteo.Gateway;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoGatewayRoutingTests : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new FakeTimeProvider(TestTimeConstants.PipelineEpoch);
    private readonly FakeForecastClient _weather = new();
    private readonly FakeAirQualityClient _airQuality = new();
    private readonly FakeFloodClient _flood = new();
    private readonly FakeMarineClient _marine = new();
    private readonly FakeSeasonalClient _seasonal = new();
    private readonly FakeSatelliteRadiationClient _satellite = new();
    private readonly FakeEnsembleClient _ensemble = new();
    private Akka.TestKit.TestProbe _attached = null!;
    private Akka.TestKit.TestProbe _forecasts = null!;
    private Akka.TestKit.TestProbe _region = null!;

    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithActors((system, registry) =>
        {
            _attached = CreateTestProbe();
            _forecasts = CreateTestProbe();
            _region = CreateTestProbe();
            registry.Register<IPollRegion>(_region.Ref);
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new ForwardingActor(CreateTestProbe().Ref)), "budget"));
            registry.Register<IForecastPipeline>(system.ActorOf(
                Props.Create(() => new FakeForecastPipeline(_attached.Ref, _forecasts.Ref, true)), "pipeline"));
            registry.Register<IForecastGateway>(system.ActorOf(
                Props.Create(() => new OpenMeteoGateway(Clients(), _time, new SwitchableBudgetProvider(), FetchTimeouts.Default)),
                "gateway"));
        });
    }

    private IReadOnlyDictionary<DomainKind, IDomainClient> Clients(params DomainKind[] without) =>
        new IDomainClient[] { _weather, _airQuality, _flood, _marine, _seasonal, _satellite, _ensemble }
            .Where(c => !without.Contains(c.Domain))
            .ToDictionary(c => c.Domain);

    private IActorRef Gateway => ActorRegistry.Get<IForecastGateway>();

    private async Task<(FetchDomainCompleted Completed, DomainDataReceived Received)> FetchAsync(DomainKind domain, CancellationToken ct)
    {
        await _attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Gateway.Tell(new FetchDomain(Lucerne, domain));
        var completed = await _region.ExpectMsgAsync<FetchDomainCompleted>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        var received = await _forecasts.ExpectMsgAsync<DomainDataReceived>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        return (completed, received);
    }

    [Fact(Timeout = 30000)]
    public async Task A_weather_fetch_uses_the_weather_client_and_delivers_weather_points()
    {
        var (completed, received) = await FetchAsync(DomainKind.Forecast, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(DomainKind.Forecast, completed.Domain),
            () => Assert.Equal(received.Data.GetHashCode(), completed.Hash),
            () => Assert.Equal("lucerne", received.Location.Name),
            () => Assert.Equal(47.05, received.Location.Latitude),
            () => Assert.IsType<DomainForecast<ForecastPoint>>(received.Data),
            () => Assert.Equal(1, _weather.Calls),
            () => Assert.Equal(0, _airQuality.Calls + _flood.Calls + _marine.Calls));
    }

    [Fact(Timeout = 30000)]
    public async Task An_air_quality_fetch_uses_the_air_quality_client_and_delivers_air_quality_points()
    {
        var (completed, received) = await FetchAsync(DomainKind.AirQuality, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(DomainKind.AirQuality, completed.Domain),
            () => Assert.Equal(8.0, Assert.Single(Assert.IsType<DomainForecast<AirQualityPoint>>(received.Data).Points).Pm25),
            () => Assert.Equal(1, _airQuality.Calls),
            () => Assert.Equal(0, _weather.Calls + _flood.Calls + _marine.Calls));
    }

    [Fact(Timeout = 30000)]
    public async Task A_flood_fetch_uses_the_flood_client_and_delivers_daily_flood_points()
    {
        var (completed, received) = await FetchAsync(DomainKind.Flood, TestContext.Current.CancellationToken);

        var forecast = Assert.IsType<DomainForecast<FloodPoint>>(received.Data);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, completed.Domain),
            () => Assert.Equal(TimeResolution.Daily, forecast.Resolution),
            () => Assert.Equal(120.0, Assert.Single(forecast.Points).Discharge),
            () => Assert.Equal(1, _flood.Calls),
            () => Assert.Equal(0, _weather.Calls + _airQuality.Calls + _marine.Calls));
    }

    [Fact(Timeout = 30000)]
    public async Task A_marine_fetch_uses_the_marine_client_and_delivers_hourly_marine_points()
    {
        var (completed, received) = await FetchAsync(DomainKind.Marine, TestContext.Current.CancellationToken);

        var forecast = Assert.IsType<DomainForecast<MarinePoint>>(received.Data);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Marine, completed.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(0.4, Assert.Single(forecast.Points).WaveHeight),
            () => Assert.Equal(1, _marine.Calls),
            () => Assert.Equal(0, _weather.Calls + _airQuality.Calls + _flood.Calls));
    }

    [Fact(Timeout = 30000)]
    public async Task A_seasonal_fetch_uses_the_seasonal_client_and_delivers_monthly_points()
    {
        var (completed, received) = await FetchAsync(DomainKind.Seasonal, TestContext.Current.CancellationToken);

        var forecast = Assert.IsType<DomainForecast<SeasonalPoint>>(received.Data);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Seasonal, completed.Domain),
            () => Assert.Equal(TimeResolution.Monthly, forecast.Resolution),
            () => Assert.Equal(1.0, forecast.Points[0].TemperatureAnomaly),
            () => Assert.Equal(1, _seasonal.Calls),
            () => Assert.Equal(0, _weather.Calls + _airQuality.Calls + _flood.Calls + _marine.Calls));
    }

    [Fact(Timeout = 30000)]
    public async Task A_satellite_fetch_uses_the_satellite_client_and_delivers_hourly_points()
    {
        var (completed, received) = await FetchAsync(DomainKind.Satellite, TestContext.Current.CancellationToken);

        var forecast = Assert.IsType<DomainForecast<SatelliteRadiationPoint>>(received.Data);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Satellite, completed.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(350.0, forecast.Points[0].GTI),
            () => Assert.Equal(1, _satellite.Calls),
            () => Assert.Equal(0, _weather.Calls + _airQuality.Calls + _flood.Calls + _marine.Calls + _seasonal.Calls));
    }

    [Fact(Timeout = 30000)]
    public async Task A_client_without_data_is_answered_with_malformed_payload_and_nothing_reaches_the_pipeline()
    {
        var ct = TestContext.Current.CancellationToken;
        await _attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        _flood.Fails = true;

        Gateway.Tell(new FetchDomain(Lucerne, DomainKind.Flood));

        var failed = await _region.ExpectMsgAsync<FetchDomainFailed>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, failed.Domain),
            () => Assert.Equal(PollFailureReason.MalformedPayload, failed.Reason));
        await _forecasts.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), ct);
    }

    [Fact(Timeout = 30000)]
    public async Task A_throwing_client_is_answered_with_a_transport_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        await _attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        _weather.Throws = true;

        Gateway.Tell(new FetchDomain(Lucerne, DomainKind.Forecast));

        var failed = await _region.ExpectMsgAsync<FetchDomainFailed>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Forecast, failed.Domain),
            () => Assert.Equal(PollFailureReason.Transport, failed.Reason));
        await _forecasts.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), ct);
    }

    [Fact(Timeout = 30000)]
    public async Task A_domain_without_a_client_is_rejected_without_a_fetch()
    {
        var ct = TestContext.Current.CancellationToken;
        await _attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        var gateway = Sys.ActorOf(
            Props.Create(() => new OpenMeteoGateway(Clients(DomainKind.Flood), _time, new SwitchableBudgetProvider(), FetchTimeouts.Default)),
            "gateway-without-flood");
        await _attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        gateway.Tell(new FetchDomain(Lucerne, DomainKind.Flood));

        var rejected = await _region.ExpectMsgAsync<FetchDomainRejected>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(FetchRejectionReason.PipelineUnavailable, rejected.Reason),
            () => Assert.Equal(DomainKind.Flood, rejected.Domain),
            () => Assert.Equal(0, _flood.Calls));
    }

    [Fact(Timeout = 30000)]
    public async Task The_reply_carries_the_requested_location()
    {
        var (completed, _) = await FetchAsync(DomainKind.Forecast, TestContext.Current.CancellationToken);

        Assert.Equal(Lucerne, completed.Location);
    }

    [Fact(Timeout = 30000)]
    public async Task A_failure_reply_carries_the_requested_location()
    {
        var ct = TestContext.Current.CancellationToken;
        await _attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        _weather.Throws = true;

        Gateway.Tell(new FetchDomain(Lucerne, DomainKind.Forecast));

        var failed = await _region.ExpectMsgAsync<FetchDomainFailed>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(Lucerne, failed.Location);
    }

    [Fact(Timeout = 30000)]
    public async Task A_full_request_queue_rejects_the_overflow_as_queue_full()
    {
        var ct = TestContext.Current.CancellationToken;
        await _attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        _weather.Hold();

        try
        {
            for (var i = 0; i < 100; i++)
            {
                Gateway.Tell(new FetchDomain(Lucerne, DomainKind.Forecast));
            }

            var rejected = await _region.ExpectMsgAsync<FetchDomainRejected>(TimeSpan.FromSeconds(5), cancellationToken: ct);
            Assert.Equal(FetchRejectionReason.QueueFull, rejected.Reason);
        }
        finally
        {
            _weather.Release();
        }
    }

    [Fact(Timeout = 30000)]
    public async Task A_multi_model_ensemble_fetch_offers_every_model_before_the_primary_forecast()
    {
        var ct = TestContext.Current.CancellationToken;
        _ensemble.ModelIds = ["ukmo", "ecmwf"];
        await _attached.ExpectMsgAsync<string>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        Gateway.Tell(new FetchDomain(Lucerne, DomainKind.Ensemble));
        var completed = await _region.ExpectMsgAsync<FetchDomainCompleted>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        var first = await _forecasts.ExpectMsgAsync<DomainDataReceived>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        var second = await _forecasts.ExpectMsgAsync<DomainDataReceived>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        var primary = await _forecasts.ExpectMsgAsync<DomainDataReceived>(TimeSpan.FromSeconds(5), cancellationToken: ct);

        Assert.Multiple(
            () => Assert.Equal(DomainKind.Ensemble, completed.Domain),
            () => Assert.Equal(["ecmwf", "ukmo"], new[] { first.ModelId, second.ModelId }.Order()),
            () => Assert.Null(primary.ModelId),
            () => Assert.Equal(primary.Data.GetHashCode(), completed.Hash));
    }
}
