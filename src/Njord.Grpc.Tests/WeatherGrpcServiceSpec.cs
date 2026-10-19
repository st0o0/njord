using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Grpc.Core;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class WeatherGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private static readonly Location ZurichLocation = new("Zurich", 47.37, 8.55);
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch(Anchor);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    private WeatherGrpcService CreateService(
        NjordOptions? options = null,
        IActorRef? consumerActor = null,
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

        var serviceInfo = TestServiceInfo.Create(options);
        ActorRegistry.Register<IGrpcConsumer>(
            consumerActor ?? Sys.ActorOf(Props.Create(() => new EmptyConsumerActor())), overwrite: true);
        return new WeatherGrpcService(
            ActorRegistry,
            Sys,
            timeProvider ?? _time,
            serviceInfo);
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
    public async Task GetDomainData_returns_the_latest_domain_data_with_timestamps()
    {
        var evt = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 3));
        var actor = Sys.ActorOf(Props.Create(() => new FakeConsumerActor(evt)));
        var service = CreateService(consumerActor: actor);

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Weather },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("Lucerne", response.Location),
            () => Assert.Equal(Domain.Weather, response.Domain),
            () => Assert.Equal(Anchor, response.ModelRun.ToDateTimeOffset()),
            () => Assert.Equal(3, response.Weather.Points.Count),
            () => Assert.Equal(Anchor.AddHours(1), response.Weather.Points[1].ValidAt.ToDateTimeOffset()),
            () => Assert.Equal(10.0, response.Weather.Points[0].Temperature),
            () => Assert.Equal(0.5, response.Weather.Points[0].EnsembleStdDev));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_queries_the_configured_location_when_the_request_differs_in_case()
    {
        var evt = new PipelineEvent.FloodDataReceived(DomainData.Lucerne, DomainData.Flood(Anchor));
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new OrdinalGrpcConsumer(evt))));

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "LUCERNE", Domain = Domain.Flood },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal(Domain.Flood, response.Domain),
            () => Assert.Equal(12.5, Assert.Single(response.Flood.Points).Discharge));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_throws_not_found_for_unknown_location()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDomainData(
                new GetDomainDataRequest { Location = "unknown", Domain = Domain.Weather },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_throws_not_found_before_the_first_data_of_the_domain()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDomainData(
                new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Solar },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_rejects_an_unspecified_domain()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDomainData(
                new GetDomainDataRequest { Location = "lucerne" },
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Multiple(
            () => Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode),
            () => Assert.Equal("Domain must be specified", ex.Status.Detail));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetDomainData_fails_with_unavailable_before_the_read_model_is_resolvable()
    {
        var serviceInfo = TestServiceInfo.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        });
        var service = new WeatherGrpcService(ActorRegistry, Sys, _time, serviceInfo);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetDomainData(
                new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Weather },
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
    public async Task GetEnrichments_returns_the_alerts_the_read_model_holds()
    {
        var lucerne = new Location("lucerne", 0, 0);
        var analysis = new LatestAnalysisResult(lucerne, new AlertResult(lucerne, []));
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new FakeGrpcConsumer(analysis, Array.Empty<PipelineEvent>()))));

        var response = await service.GetEnrichments(
            new GetEnrichmentsRequest { Location = "lucerne" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(new GetEnrichmentsResponse { Location = "lucerne", Alerts = new AlertUpdate() }, response);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetEnrichments_queries_the_configured_location_when_the_request_differs_in_case()
    {
        var lucerne = new Location("lucerne", 0, 0);
        var analysis = new LatestAnalysisResult(lucerne, new AlertResult(lucerne, []));
        var service = CreateService(consumerActor: Sys.ActorOf(Props.Create(() => new FakeGrpcConsumer(analysis, Array.Empty<PipelineEvent>()))));

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
            [("lucerne", "alerts"), ("lucerne", "alerts")],
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
            [("lucerne", "alerts"), ("zurich", "alerts"), ("lucerne", "alerts")],
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
    public async Task StreamDomainData_streams_only_the_events_of_the_requested_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<DomainDataUpdate>();

        await service.StreamDomainData(new StreamDomainDataRequest { Location = "ZURICH" }, writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var update = Assert.Single(writer.Messages);
        Assert.Equal(("Zurich", Domain.Weather), (update.Location, update.Domain));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamDomainData_filters_by_domain()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<DomainDataUpdate>();
        var request = new StreamDomainDataRequest();
        request.Domains.Add(Domain.Flood);

        await service.StreamDomainData(request, writer, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var update = Assert.Single(writer.Messages);
        Assert.Equal(("Lucerne", Domain.Flood), (update.Location, update.Domain));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamDomainData_without_filters_streams_every_domain_of_every_location()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<DomainDataUpdate>();

        await service.StreamDomainData(new StreamDomainDataRequest(), writer,
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(
            [("Lucerne", Domain.Weather), ("Zurich", Domain.Weather), ("Lucerne", Domain.Flood)],
            writer.Messages.Select(m => (m.Location, m.Domain)));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task StreamDomainData_rejects_an_unknown_location_with_not_found()
    {
        var service = CreateService(consumerActor: CreateStreamingReadModel());
        var writer = new CollectingStreamWriter<DomainDataUpdate>();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.StreamDomainData(new StreamDomainDataRequest { Location = "atlantis" }, writer,
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
        var lucerne = new Location("lucerne", 0, 0);
        var zurich = new Location("zurich", 0, 0);
        PipelineEvent[] events =
        [
            new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2)),
            EnrichmentEvents.Alerts(lucerne, Anchor),
            new PipelineEvent.WeatherDataReceived(ZurichLocation, DomainData.Weather(Anchor, 2)),
            EnrichmentEvents.Alerts(zurich, Anchor),
            new PipelineEvent.FloodDataReceived(DomainData.Lucerne, DomainData.Flood(Anchor)),
            EnrichmentEvents.Alerts(lucerne, Anchor.AddHours(1)),
        ];
        return Sys.ActorOf(Props.Create(() => new FakeGrpcConsumer(null, events)));
    }

    private sealed class EmptyConsumerActor : ReceiveActor
    {
        public EmptyConsumerActor()
        {
            Receive<QueryDomainData>(msg => Sender.Tell(new DomainDataNotAvailable(msg.Location, msg.Domain), Self));
            Receive<QueryLatestAnalysis>(msg => Sender.Tell(new LatestAnalysisNotAvailable(msg.Location), Self));
        }
    }

    private sealed class FakeConsumerActor : ReceiveActor
    {
        public FakeConsumerActor(PipelineEvent evt)
        {
            Receive<QueryDomainData>(_ => Sender.Tell(new DomainDataResult(evt), Self));
        }
    }

    // Answers only the configured spelling (ordinal), so a raw request spelling finds nothing.
    private sealed class OrdinalGrpcConsumer : ReceiveActor
    {
        public OrdinalGrpcConsumer(PipelineEvent.FloodDataReceived evt)
        {
            Receive<QueryDomainData>(msg => Sender.Tell(
                string.Equals(msg.Location.Name, "lucerne", StringComparison.Ordinal) && msg.Domain == DomainKind.Flood
                    ? new DomainDataResult(evt)
                    : new DomainDataNotAvailable(msg.Location, msg.Domain),
                Self));
        }
    }

    // Answers only the configured spelling (ordinal), so a spec proves the service queries with it.
    private sealed class FakeGrpcConsumer : ReceiveActor
    {
        public FakeGrpcConsumer(LatestAnalysisResult? analysis, IReadOnlyList<PipelineEvent> streamEvents)
        {
            Receive<QueryLatestAnalysis>(msg => Sender.Tell(
                analysis is not null && string.Equals(analysis.Location.Name, msg.Location.Name, StringComparison.Ordinal)
                    ? analysis
                    : new LatestAnalysisNotAvailable(msg.Location),
                Self));
            Receive<RequestGrpcStream>(msg =>
                Source.From(streamEvents)
                    .RunWith(StreamRefs.SourceRef<PipelineEvent>(), Context.Materializer())
                    .PipeTo(Sender, Self,
                        sourceRef => new RequestGrpcStreamCompleted(msg.RequestId, sourceRef),
                        ex => new RequestGrpcStreamFailed(msg.RequestId, ex)));
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
