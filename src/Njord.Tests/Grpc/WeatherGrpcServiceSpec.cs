using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Grpc;
using Njord.Grpc.V2;

namespace Njord.Tests.Grpc;

public sealed class WeatherGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
    private static readonly DateTimeOffset Anchor = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private WeatherGrpcService CreateService(
        NjordOptions? options = null,
        IActorRef? forecastActor = null,
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

        ActorRegistry.Register<ForecastSnapshotActor>(
            forecastActor ?? Sys.ActorOf(Props.Create(() => new EmptyForecastActor())), overwrite: true);
        ActorRegistry.Register<EnrichmentSnapshotActor>(
            enrichmentActor ?? Sys.ActorOf(Props.Create(() => new EmptyEnrichmentActor())), overwrite: true);

        return new WeatherGrpcService(
            Microsoft.Extensions.Options.Options.Create(options),
            ActorRegistry,
            Sys,
            timeProvider ?? TimeProvider.System);
    }

    [Fact(Timeout = 5000)]
    public async Task GetCatalog_returns_all_locations_with_resolved_models()
    {
        var service = CreateService();

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create());

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

    [Fact(Timeout = 5000)]
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

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create());

        Assert.Equal(2, response.Locations.Count);
        Assert.Single(response.Models);
        Assert.Equal("icon_d2", response.Models[0].Id);
    }

    [Fact(Timeout = 5000)]
    public async Task GetForecast_returns_forecast_with_timestamps()
    {
        var forecast = CreateForecast();
        var actor = Sys.ActorOf(Props.Create(() => new FakeForecastActor(forecast)));
        var service = CreateService(forecastActor: actor);

        var response = await service.GetForecast(
            new GetForecastRequest { Location = "lucerne", Model = "icon_d2" },
            TestServerCallContext.Create());

        Assert.Equal("lucerne", response.Location);
        Assert.Equal("icon_d2", response.Model);
        Assert.NotNull(response.UpdatedAt);
        Assert.True(response.UpdatedAt.ToDateTimeOffset() > DateTimeOffset.MinValue);

        var hourly = Assert.Single(response.Hourly);
        Assert.NotNull(hourly.ValidAt);
        Assert.Equal(Anchor.AddHours(3), hourly.ValidAt.ToDateTimeOffset());
        Assert.Equal(28.8, hourly.Temperature);
    }

    [Fact(Timeout = 5000)]
    public async Task GetForecast_throws_not_found_for_unknown_location()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetForecast(
                new GetForecastRequest { Location = "unknown", Model = "icon_d2" },
                TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = 5000)]
    public async Task GetEnrichments_WithConsensusResult_without_ComputedAt_falls_back_to_wall_clock()
    {
        var timeProvider = new FakeTimeProvider(Anchor);
        var consensus = new ConsensusResult([]);
        IReadOnlyList<(string TypeName, object Result)> results = [("consensus", (object)consensus)];
        var actor = Sys.ActorOf(Props.Create(() => new FakeEnrichmentActor(results)));
        var service = CreateService(enrichmentActor: actor, timeProvider: timeProvider);

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create());

        Assert.NotNull(response.ConsensusUpdatedAt);
        Assert.Equal(Anchor, response.ConsensusUpdatedAt.ToDateTimeOffset());
    }

    [Fact(Timeout = 5000)]
    public async Task GetEnrichments_WithConsensusResult_uses_ComputedAt_not_query_time()
    {
        var computationTime = new DateTimeOffset(2026, 7, 15, 6, 0, 0, TimeSpan.Zero);
        var queryTime = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(queryTime);
        var consensus = new ConsensusResult([], [], computationTime);
        IReadOnlyList<(string TypeName, object Result)> results = [("consensus", (object)consensus)];
        var actor = Sys.ActorOf(Props.Create(() => new FakeEnrichmentActor(results)));
        var service = CreateService(enrichmentActor: actor, timeProvider: timeProvider);

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create());

        Assert.NotNull(response.ConsensusUpdatedAt);
        Assert.Equal(computationTime, response.ConsensusUpdatedAt.ToDateTimeOffset());
    }

    [Fact(Timeout = 5000)]
    public async Task GetEnrichments_WithoutConsensusResult_LeavesConsensusUpdatedAtUnset()
    {
        var service = CreateService();

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create());

        Assert.Null(response.ConsensusUpdatedAt);
    }

    private static ModelForecast CreateForecast(string model = "icon_d2")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 }),
        };
        return new ModelForecast(new WeatherModel(model), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private sealed class EmptyForecastActor : ReceiveActor
    {
        public EmptyForecastActor()
        {
            Receive<Njord.Grpc.GetForecast>(_ => Sender.Tell(new ForecastResponse(null), Self));
            Receive<GetAllForecasts>(_ => Sender.Tell(
                new AllForecastsResponse(new Dictionary<(string, string), ModelForecast>()), Self));
        }
    }

    private sealed class FakeForecastActor : ReceiveActor
    {
        public FakeForecastActor(ModelForecast forecast)
        {
            Receive<Njord.Grpc.GetForecast>(_ => Sender.Tell(new ForecastResponse(forecast), Self));
        }
    }

    private sealed class EmptyEnrichmentActor : ReceiveActor
    {
        public EmptyEnrichmentActor()
        {
            Receive<GetAllEnrichments>(_ => Sender.Tell(
                new AllEnrichmentsResponse([]), Self));
        }
    }

    private sealed class FakeEnrichmentActor : ReceiveActor
    {
        public FakeEnrichmentActor(IReadOnlyList<(string TypeName, object Result)> results)
        {
            Receive<GetAllEnrichments>(_ => Sender.Tell(new AllEnrichmentsResponse(results), Self));
        }
    }
}
