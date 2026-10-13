using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Forecast;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Grpc.Tests;

public sealed class WeatherGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch(Anchor);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    private WeatherGrpcService CreateService(
        NjordOptions? options = null,
        IActorRef? consumerActor = null,
        IActorRef? enrichmentActor = null,
        TimeProvider? timeProvider = null)
    {
        options ??= new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.55 },
            ],
            Models = ["icon_d2", "ecmwf_ifs025"],
        };

        ActorRegistry.Register<IGrpcConsumer>(
            consumerActor ?? Sys.ActorOf(Props.Create(() => new EmptyConsumerActor())), overwrite: true);
        return new WeatherGrpcService(
            Options.Create(options),
            ActorRegistry,
            Sys,
            timeProvider ?? _time);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetCatalog_returns_all_locations_with_resolved_models()
    {
        var service = CreateService();

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(2, response.Locations.Count);

        var lucerne = response.Locations[0];
        Assert.Equal("lucerne", lucerne.Name);
        Assert.Equal(47.05, lucerne.Latitude);
        Assert.Equal(8.31, lucerne.Longitude);
        Assert.Equal(["icon_d2", "ecmwf_ifs025"], lucerne.Models);

        var zurich = response.Locations[1];
        Assert.Equal("zurich", zurich.Name);
        Assert.Equal(["icon_d2", "ecmwf_ifs025"], zurich.Models);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetCatalog_deduplicates_model_info_across_locations()
    {
        var options = new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.55 },
            ],
            Models = ["icon_d2"],
        };
        var service = CreateService(options);

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(2, response.Locations.Count);
        var model = Assert.Single(response.Models);
        Assert.Equal("icon_d2", model.Id);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetForecast_returns_forecast_with_timestamps()
    {
        var forecast = CreateForecast();
        var actor = Sys.ActorOf(Props.Create(() => new FakeConsumerActor(forecast)));
        var service = CreateService(consumerActor: actor);

        var response = await service.GetForecast(
            new GetForecastRequest { Location = "lucerne", Model = "icon_d2" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal("lucerne", response.Location);
        Assert.Equal("icon_d2", response.Model);
        Assert.NotNull(response.UpdatedAt);
        Assert.True(response.UpdatedAt.ToDateTimeOffset() > DateTimeOffset.MinValue);

        var hourly = Assert.Single(response.Hourly);
        Assert.NotNull(hourly.ValidAt);
        Assert.Equal(Anchor.AddHours(3), hourly.ValidAt.ToDateTimeOffset());
        Assert.Equal(28.8, hourly.Temperature);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetForecast_throws_not_found_for_unknown_location()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetForecast(
                new GetForecastRequest { Location = "unknown", Model = "icon_d2" },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    private static Forecast CreateForecast(string model = "icon_d2")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 }),
        };
        return new Forecast(new WeatherModel(model), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private sealed class EmptyConsumerActor : ReceiveActor
    {
        public EmptyConsumerActor()
        {
            Receive<QueryForecast>(msg => Sender.Tell(new ForecastNotAvailable(msg.Location + "|" + msg.ModelId), Self));
        }
    }

    private sealed class FakeConsumerActor : ReceiveActor
    {
        public FakeConsumerActor(Forecast forecast)
        {
            Receive<QueryForecast>(_ => Sender.Tell(new ForecastResult(forecast), Self));
        }
    }

}
