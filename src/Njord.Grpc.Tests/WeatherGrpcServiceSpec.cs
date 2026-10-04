using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Actors;
using Njord.Analysis;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Grpc;
using Njord.Grpc.V2;
using Njord.Messages.Snapshots;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class WeatherGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
    private static readonly DateTimeOffset Anchor = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _time = new(Anchor);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

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

        ActorRegistry.Register<IForecastSnapshotActor>(
            forecastActor ?? Sys.ActorOf(Props.Create(() => new EmptyForecastActor())), overwrite: true);
        ActorRegistry.Register<IEnrichmentSnapshotActor>(
            enrichmentActor ?? Sys.ActorOf(Props.Create(() => new EmptyEnrichmentActor())), overwrite: true);

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
        var actor = Sys.ActorOf(Props.Create(() => new FakeForecastActor(forecast)));
        var service = CreateService(forecastActor: actor);

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

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_WithConsensusResult_without_ComputedAt_falls_back_to_wall_clock()
    {
        var timeProvider = new FakeTimeProvider(Anchor);
        var consensus = new ConsensusResult([]);
        IReadOnlyList<(string TypeName, object Result)> results = [("consensus", (object)consensus)];
        var actor = Sys.ActorOf(Props.Create(() => new FakeEnrichmentActor(results)));
        var service = CreateService(enrichmentActor: actor, timeProvider: timeProvider);

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.NotNull(response.ConsensusUpdatedAt);
        Assert.Equal(Anchor, response.ConsensusUpdatedAt.ToDateTimeOffset());
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
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
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.NotNull(response.ConsensusUpdatedAt);
        Assert.Equal(computationTime, response.ConsensusUpdatedAt.ToDateTimeOffset());
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_WithoutConsensusResult_LeavesConsensusUpdatedAtUnset()
    {
        var service = CreateService();

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

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

    [Fact(Timeout = 5000)]
    public async Task StreamForecasts_throws_unavailable_when_model_state_source_request_fails()
    {
        ActorRegistry.Register<IModelStateActor>(
            Sys.ActorOf(FailingRefProvider.Props(CreateTestProbe())), overwrite: true);
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.StreamForecasts(
            new StreamForecastsRequest(), new DiscardingStreamWriter<ForecastUpdate>(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact(Timeout = 5000)]
    public async Task StreamEnrichments_throws_unavailable_when_enrichment_source_request_fails()
    {
        ActorRegistry.Register<IEnrichmentActor>(
            Sys.ActorOf(FailingRefProvider.Props(CreateTestProbe())), overwrite: true);
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.StreamEnrichments(
            new StreamEnrichmentsRequest(), new DiscardingStreamWriter<EnrichmentEvent>(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    private sealed class DiscardingStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message) => Task.CompletedTask;
    }

    private sealed class EmptyForecastActor : ReceiveActor
    {
        public EmptyForecastActor()
        {
            Receive<QueryForecast>(msg => Sender.Tell(new ForecastNotFound(msg.Location + "|" + msg.ModelId), Self));
            Receive<QueryAllForecasts>(_ => Sender.Tell(
                new QueryAllForecastsResult(new Dictionary<(string, string), ModelForecast>()), Self));
        }
    }

    private sealed class FakeForecastActor : ReceiveActor
    {
        public FakeForecastActor(ModelForecast forecast)
        {
            Receive<QueryForecast>(_ => Sender.Tell(new ForecastFound(forecast), Self));
        }
    }

    private sealed class EmptyEnrichmentActor : ReceiveActor
    {
        public EmptyEnrichmentActor()
        {
            Receive<QueryAllEnrichments>(_ => Sender.Tell(
                new QueryAllEnrichmentsResult([]), Self));
        }
    }

    private sealed class FakeEnrichmentActor : ReceiveActor
    {
        public FakeEnrichmentActor(IReadOnlyList<(string TypeName, object Result)> results)
        {
            Receive<QueryAllEnrichments>(_ => Sender.Tell(new QueryAllEnrichmentsResult(results), Self));
        }
    }
}
