using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Grpc.Core;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
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

        var config = TestConfigView.Create(options);
        ActorRegistry.Register<IConfigManager>(
            Sys.ActorOf(Props.Create(() => new FakeConfigManager(config))), overwrite: true);
        ActorRegistry.Register<IForecastReadModel>(
            consumerActor ?? Sys.ActorOf(Props.Create(() => new EmptyConsumerActor())), overwrite: true);
        return new WeatherGrpcService(
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
    public async Task GetForecast_queries_the_configured_location_and_model_when_the_request_differs_in_case()
    {
        var forecast = CreateForecast();
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new OrdinalForecastReadModel(forecast))));

        var response = await service.GetForecast(
            new GetForecastRequest { Location = "LUCERNE", Model = "ICON_D2" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("lucerne", response.Location),
            () => Assert.Equal("icon_d2", response.Model),
            () => Assert.Single(response.Hourly));
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
    public async Task GetForecast_fails_with_unavailable_before_the_read_model_is_resolvable()
    {
        var config = TestConfigView.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        });
        ActorRegistry.Register<IConfigManager>(Sys.ActorOf(Props.Create(() => new FakeConfigManager(config))));
        var service = new WeatherGrpcService(ActorRegistry, Sys, _time);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetForecast(
                new GetForecastRequest { Location = "lucerne", Model = "icon_d2" },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_returns_only_the_location_before_the_first_analysis()
    {
        var service = CreateService();

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(new GetEnrichmentsResponse { Location = "lucerne" }, response);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_returns_every_enrichment_kind_the_read_model_holds()
    {
        var lucerne = new LocationId("lucerne");
        var analysis = new LatestAnalysisResult(lucerne,
            EnrichmentEvents.Consensus(lucerne, Anchor),
            EnrichmentEvents.Alerts(lucerne, Anchor),
            EnrichmentEvents.Derived(lucerne, Anchor),
            EnrichmentEvents.Trends(lucerne, Anchor),
            EnrichmentEvents.Indices(lucerne, Anchor));
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new FakeReadModel(analysis, Array.Empty<PipelineEvent>()))));

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("lucerne", response.Location),
            () => Assert.NotNull(response.Consensus),
            () => Assert.Equal(Anchor, response.ConsensusUpdatedAt?.ToDateTimeOffset()),
            () => Assert.NotNull(response.Alerts),
            () => Assert.Equal(4.5, response.Derived?.Scalars.DiurnalAmplitude),
            () => Assert.Equal(3, response.Trends?.PrecipStartsInHours),
            () => Assert.Equal(14, response.Indices?.Frost.HoursUntilFrost));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_queries_the_configured_location_when_the_request_differs_in_case()
    {
        var lucerne = new LocationId("lucerne");
        var analysis = new LatestAnalysisResult(lucerne, null, EnrichmentEvents.Alerts(lucerne, Anchor), null, null, null);
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new FakeReadModel(analysis, Array.Empty<PipelineEvent>()))));

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "LUCERNE" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("lucerne", response.Location),
            () => Assert.NotNull(response.Alerts));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamEnrichments_streams_only_the_events_of_the_requested_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<EnrichmentEvent>();

        await service.StreamEnrichments(new StreamEnrichmentsRequest { Location = "Lucerne" }, writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(
            [("lucerne", "alerts"), ("lucerne", "trends")],
            writer.Messages.Select(m => (m.Location, m.TypeName)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamEnrichments_with_an_empty_location_streams_every_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<EnrichmentEvent>();

        await service.StreamEnrichments(new StreamEnrichmentsRequest(), writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(
            [("lucerne", "alerts"), ("zurich", "alerts"), ("lucerne", "trends")],
            writer.Messages.Select(m => (m.Location, m.TypeName)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamEnrichments_rejects_an_unknown_location_with_not_found()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<EnrichmentEvent>();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.StreamEnrichments(new StreamEnrichmentsRequest { Location = "atlantis" }, writer,
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Multiple(
            () => Assert.Equal(StatusCode.NotFound, ex.StatusCode),
            () => Assert.Equal("Location 'atlantis' not found", ex.Status.Detail),
            () => Assert.Empty(writer.Messages));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamForecasts_streams_only_the_forecasts_of_the_requested_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<ForecastUpdate>();

        await service.StreamForecasts(new StreamForecastsRequest { Location = "ZURICH" }, writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var update = Assert.Single(writer.Messages);
        Assert.Equal(("zurich", "icon_d2"), (update.Location, update.Model));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamForecasts_rejects_an_unknown_location_with_not_found()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<ForecastUpdate>();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.StreamForecasts(new StreamForecastsRequest { Location = "atlantis" }, writer,
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Multiple(
            () => Assert.Equal(StatusCode.NotFound, ex.StatusCode),
            () => Assert.Equal("Location 'atlantis' not found", ex.Status.Detail),
            () => Assert.Empty(writer.Messages));
    }

    // A read model whose stream is a finite sequence of events for two locations: the RPC returns once
    // the sequence completes, so the specs assert what was written without waiting.
    private IActorRef CreateStreamingReadModel()
    {
        var lucerne = new LocationId("lucerne");
        var zurich = new LocationId("zurich");
        PipelineEvent[] events =
        [
            new PipelineEvent.ForecastReceived(lucerne, new WeatherModel("icon_d2"), CreateForecast()),
            EnrichmentEvents.Alerts(lucerne, Anchor),
            new PipelineEvent.ForecastReceived(zurich, new WeatherModel("icon_d2"), CreateForecast(location: "zurich")),
            EnrichmentEvents.Alerts(zurich, Anchor),
            EnrichmentEvents.Trends(lucerne, Anchor),
        ];
        return Sys.ActorOf(Props.Create(() => new FakeReadModel(null, events)));
    }

    private static Forecast CreateForecast(string model = "icon_d2", string location = "lucerne")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 }),
        };
        return new Forecast(new WeatherModel(model), location, new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private sealed class EmptyConsumerActor : ReceiveActor
    {
        public EmptyConsumerActor()
        {
            Receive<QueryForecast>(msg => Sender.Tell(new ForecastNotAvailable(msg.Location.Name + "|" + msg.ModelId), Self));
            Receive<QueryLatestAnalysis>(msg => Sender.Tell(new LatestAnalysisNotAvailable(msg.Location), Self));
        }
    }

    private sealed class FakeConsumerActor : ReceiveActor
    {
        public FakeConsumerActor(Forecast forecast)
        {
            Receive<QueryForecast>(_ => Sender.Tell(new ForecastResult(forecast), Self));
        }
    }

    // Answers only the configured spelling (ordinal), so a raw request spelling finds nothing.
    private sealed class OrdinalForecastReadModel : ReceiveActor
    {
        public OrdinalForecastReadModel(Forecast forecast)
        {
            Receive<QueryForecast>(msg => Sender.Tell(
                string.Equals(msg.Location.Name, forecast.Location, StringComparison.Ordinal)
                && string.Equals(msg.ModelId, forecast.Model.Id, StringComparison.Ordinal)
                    ? new ForecastResult(forecast)
                    : new ForecastNotAvailable($"{msg.Location.Name}|{msg.ModelId}"),
                Self));
        }
    }

    // Answers only the configured spelling (ordinal), so a spec proves the service queries with it.
    private sealed class FakeReadModel : ReceiveActor
    {
        public FakeReadModel(LatestAnalysisResult? analysis, IReadOnlyList<PipelineEvent> streamEvents)
        {
            Receive<QueryLatestAnalysis>(msg => Sender.Tell(
                analysis is not null && string.Equals(analysis.Location.Name, msg.Location.Name, StringComparison.Ordinal)
                    ? analysis
                    : new LatestAnalysisNotAvailable(msg.Location),
                Self));
            Receive<RequestReadModelStream>(msg =>
                Source.From(streamEvents)
                    .RunWith(StreamRefs.SourceRef<PipelineEvent>(), Context.Materializer())
                    .PipeTo(Sender, Self,
                        sourceRef => new RequestReadModelStreamCompleted(msg.RequestId, sourceRef),
                        ex => new RequestReadModelStreamFailed(msg.RequestId, ex)));
        }
    }

    private sealed class CollectingStreamWriter<T> : IServerStreamWriter<T>
    {
        private readonly ConcurrentQueue<T> _messages = new();

        public WriteOptions? WriteOptions { get; set; }

        public IReadOnlyList<T> Messages => [.. _messages];

        public Task WriteAsync(T message)
        {
            _messages.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task WriteAsync(T message, CancellationToken cancellationToken) => WriteAsync(message);
    }
}
