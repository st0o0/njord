using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Forecast;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoGatewayRoutingSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly FakeWeatherClient _weather = new();
    private readonly FakeAirQualityClient _airQuality = new();
    private readonly FakeFloodClient _flood = new();
    private readonly FakeMarineClient _marine = new();
    private Akka.TestKit.TestProbe _attached = null!;
    private Akka.TestKit.TestProbe _forecasts = null!;

    protected override bool UseTestPersistence => false;

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithActors((system, registry) =>
        {
            _attached = CreateTestProbe();
            _forecasts = CreateTestProbe();
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new ForwardingActor(CreateTestProbe().Ref)), "budget"));
            registry.Register<IForecastPipeline>(system.ActorOf(
                Props.Create(() => new FakeForecastPipeline(_attached.Ref, _forecasts.Ref, true)), "pipeline"));
            var options = Options.Create(AppSettingsDefaults.Options<NjordOptions>(o =>
            {
                o.Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }];
            }));
            var weatherOptions = Options.Create(new WeatherOptions());
            registry.Register<IForecastGateway>(system.ActorOf(
                Props.Create(() => new OpenMeteoGateway(_weather, _airQuality, _flood, _marine, _time, options, weatherOptions, new SwitchableBudgetProvider(), FetchTimeouts.Default)),
                "gateway"));
        });
    }

    private IActorRef Gateway => GetActor<IForecastGateway>();

    private async Task<(FetchDomainCompleted Completed, DomainDataReceived Received)> FetchAsync(DomainKind domain, CancellationToken ct)
    {
        await _attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Gateway.Tell(new FetchDomain(new Location("lucerne", 0, 0), domain));
        var completed = await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var received = await _forecasts.ExpectMsgAsync<DomainDataReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        return (completed, received);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_weather_fetch_uses_the_weather_client_and_delivers_weather_points()
    {
        var (completed, received) = await FetchAsync(DomainKind.Weather, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(DomainKind.Weather, completed.Domain),
            () => Assert.Equal(received.Data.GetHashCode(), completed.Hash),
            () => Assert.Equal("lucerne", received.Location.Name),
            () => Assert.Equal(47.05, received.Location.Latitude),
            () => Assert.IsType<DomainForecast<WeatherPoint>>(received.Data),
            () => Assert.Equal(1, _weather.Calls),
            () => Assert.Equal(0, _airQuality.Calls + _flood.Calls + _marine.Calls));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_air_quality_fetch_uses_the_air_quality_client_and_delivers_air_quality_points()
    {
        var (completed, received) = await FetchAsync(DomainKind.AirQuality, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(DomainKind.AirQuality, completed.Domain),
            () => Assert.Equal(8.0, Assert.Single(Assert.IsType<DomainForecast<AirQualityPoint>>(received.Data).Points).Pm25),
            () => Assert.Equal(1, _airQuality.Calls),
            () => Assert.Equal(0, _weather.Calls + _flood.Calls + _marine.Calls));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
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

    [Fact(Timeout = TestTimeouts.Hosted)]
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

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_client_result_is_answered_with_malformed_payload_and_nothing_reaches_the_pipeline()
    {
        var ct = TestContext.Current.CancellationToken;
        await _attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        _flood.Fails = true;

        Gateway.Tell(new FetchDomain(new Location("lucerne", 0, 0), DomainKind.Flood));

        var failed = await ExpectMsgAsync<FetchDomainFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, failed.Domain),
            () => Assert.Equal(PollFailureReason.MalformedPayload, failed.Reason));
        await _forecasts.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unknown_location_fails_without_a_fetch()
    {
        var ct = TestContext.Current.CancellationToken;
        await _attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        Gateway.Tell(new FetchDomain(new Location("atlantis", 0, 0), DomainKind.Weather));

        var failed = await ExpectMsgAsync<FetchDomainFailed>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(PollFailureReason.ModelUnavailable, failed.Reason),
            () => Assert.Equal(0, _weather.Calls));
    }
}
