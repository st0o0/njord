using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Njord.Configuration;
using GrpcStatus = Grpc.Core.Status;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Grpc;
using Njord.Grpc.V1;

namespace Njord.Tests.Grpc;

public sealed class ForecastGrpcServiceSpec : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ActorSystem _system = ActorSystem.Create("grpc-service-spec");

    public void Dispose() => _system.Dispose();

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2", "ecmwf_ifs025"],
    };

    private ForecastGrpcService CreateService(
        NjordOptions? options = null,
        IActorRef? forecastActor = null,
        IActorRef? enrichmentActor = null)
    {
        options ??= DefaultOptions();
        var registry = ActorRegistry.For(_system);

        if (forecastActor is not null)
        {
            registry.Register<ForecastSnapshotActor>(forecastActor, overwrite: true);
        }
        else
        {
            registry.Register<ForecastSnapshotActor>(
                _system.ActorOf(Props.Create(() => new EmptyForecastActor())), overwrite: true);
        }

        if (enrichmentActor is not null)
        {
            registry.Register<EnrichmentSnapshotActor>(enrichmentActor, overwrite: true);
        }
        else
        {
            registry.Register<EnrichmentSnapshotActor>(
                _system.ActorOf(Props.Create(() => new EmptyEnrichmentActor())), overwrite: true);
        }

        return new ForecastGrpcService(
            Microsoft.Extensions.Options.Options.Create(options), registry, _system);
    }

    [Fact(Timeout = 5000)]
    public async Task GetLocations_returns_configured_locations()
    {
        var service = CreateService();

        var response = await service.GetLocations(new GetLocationsRequest(), TestServerCallContext.Create());

        Assert.Single(response.Locations);
        Assert.Equal("lucerne", response.Locations[0]);
    }

    [Fact(Timeout = 5000)]
    public async Task GetModels_returns_resolved_models_for_location()
    {
        var service = CreateService();

        var response = await service.GetModels(
            new GetModelsRequest { Location = "lucerne" }, TestServerCallContext.Create());

        Assert.Equal("lucerne", response.Location);
        Assert.Contains("icon_d2", response.Models);
    }

    [Fact(Timeout = 5000)]
    public async Task GetModels_throws_not_found_for_unknown_location()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetModels(new GetModelsRequest { Location = "unknown" }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = 5000)]
    public async Task GetForecast_returns_data_from_snapshot_actor()
    {
        var forecast = CreateForecast();
        var actor = _system.ActorOf(Props.Create(() => new FakeForecastActor(forecast)));
        var service = CreateService(forecastActor: actor);

        var response = await service.GetForecast(
            new GetForecastRequest { Location = "lucerne", Model = "icon_d2" },
            TestServerCallContext.Create());

        Assert.Equal("lucerne", response.Location);
        Assert.Equal("icon_d2", response.Model);
        Assert.NotEmpty(response.Hourly);
    }

    [Fact(Timeout = 5000)]
    public async Task GetForecast_throws_not_found_when_no_data()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetForecast(
                new GetForecastRequest { Location = "lucerne", Model = "icon_d2" },
                TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = 5000)]
    public async Task GetEnrichments_returns_data_from_snapshot_actor()
    {
        var indexResult = new IndexResult("lucerne", 80, 90, 70, 85, 95, 60, 12.5, 0.5, 88, 75, null, null);
        var actor = _system.ActorOf(Props.Create(() => new FakeEnrichmentActor(indexResult)));
        var service = CreateService(enrichmentActor: actor);

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create());

        Assert.Equal("lucerne", response.Location);
        Assert.NotNull(response.Indices);
        Assert.Equal(80, response.Indices.Laundry);
    }

    [Fact(Timeout = 5000)]
    public async Task GetEnrichments_throws_not_found_for_unknown_location()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetEnrichments(
                new GetEnrichmentsRequest { Location = "unknown" },
                TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    private static ModelForecast CreateForecast()
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Now.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 }),
        };
        return new ModelForecast(new WeatherModel("icon_d2"), "lucerne", new CycleId(Now),
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
        public FakeEnrichmentActor(object result)
        {
            Receive<GetAllEnrichments>(_ => Sender.Tell(
                new AllEnrichmentsResponse([("indices", result)]), Self));
        }
    }

    internal sealed class TestServerCallContext : ServerCallContext
    {
        private TestServerCallContext() { }
        public static TestServerCallContext Create() => new();

        protected override string MethodCore => "test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => [];
        protected override GrpcStatus StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(string.Empty, []);

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotImplementedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
