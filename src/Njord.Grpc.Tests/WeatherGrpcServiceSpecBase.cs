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
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public abstract class WeatherGrpcServiceSpecBase : Akka.Hosting.TestKit.TestKit
{
    protected static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    protected static readonly Location ZurichLocation = new("Zurich", 47.37, 8.55);
    protected readonly FakeTimeProvider _time = TestTimeouts.AtEpoch(Anchor);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    protected WeatherGrpcService CreateService(
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

    // A read model whose stream is a finite sequence of events for two locations: the RPC returns once
    // the sequence completes, so the specs assert what was written without waiting.
    protected IActorRef CreateStreamingReadModel()
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

    internal sealed class EmptyConsumerActor : ReceiveActor
    {
        public EmptyConsumerActor()
        {
            Receive<QueryDomainData>(msg => Sender.Tell(new DomainDataNotAvailable(msg.Location, msg.Domain), Self));
            Receive<QueryLatestAnalysis>(msg => Sender.Tell(new LatestAnalysisNotAvailable(msg.Location), Self));
        }
    }

    internal sealed class FakeConsumerActor : ReceiveActor
    {
        public FakeConsumerActor(PipelineEvent evt)
        {
            Receive<QueryDomainData>(_ => Sender.Tell(new DomainDataResult(evt), Self));
        }
    }

    // Answers only the configured spelling (ordinal), so a raw request spelling finds nothing.
    internal sealed class OrdinalGrpcConsumer : ReceiveActor
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
    internal sealed class FakeGrpcConsumer : ReceiveActor
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
}
