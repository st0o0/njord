using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Forecast;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoGatewayAttachSpec : NjordActorSpec
{
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch();
    private readonly FakeOpenMeteoClient _client = new();
    private readonly BudgetProvider _budget = new();

    protected override bool UseTestPersistence => false;

    protected override void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry) =>
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new SilentActor()), "budget")));

    private IActorRef StartGateway()
    {
        var options = Options.Create(AppSettingsDefaults.Options<NjordOptions>(o =>
        {
            o.Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }];
        }));
        var parameters = ParameterSelection.Resolve(["Weather"], [], []);
        return Sys.ActorOf(
            Props.Create(() => new OpenMeteoGateway(_client, _time, options, parameters, _budget, FetchTimeouts.Default)),
            $"gateway-{Guid.NewGuid():N}");
    }

    private (IActorRef Pipeline, Akka.TestKit.TestProbe Attached, Akka.TestKit.TestProbe Forecasts) RegisterPipeline(bool answers = true)
    {
        var attached = CreateTestProbe();
        var forecasts = CreateTestProbe();
        var pipeline = Sys.ActorOf(Props.Create(() => new FakePipeline(attached.Ref, forecasts.Ref, answers)),
            $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(pipeline, overwrite: true);
        return (pipeline, attached, forecasts);
    }

    private static FetchDomain Fetch() => new(new Location("lucerne", 0, 0), DomainKind.Weather);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_request_before_the_pipeline_input_is_attached_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, attached, _) = RegisterPipeline(answers: false);
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        gateway.Tell(Fetch());

        var rejected = await ExpectMsgAsync<FetchDomainRejected>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal("lucerne", rejected.Location.Name),
            () => Assert.Equal(DomainKind.Weather, rejected.Domain),
            () => Assert.Equal(FetchRejectionReason.PipelineUnavailable, rejected.Reason));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_gateway_re_attaches_to_a_new_pipeline_after_the_old_one_terminated()
    {
        var ct = TestContext.Current.CancellationToken;
        var reattaches = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Njord.OpenMeteo" && instrument.Name == "njord_openmeteo_pipeline_reattaches_total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref reattaches, value));
        listener.Start();

        var (first, firstAttached, _) = RegisterPipeline();
        var gateway = StartGateway();
        await firstAttached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(0, Interlocked.Read(ref reattaches));

        var (_, secondAttached, secondForecasts) = RegisterPipeline();
        await first.GracefulStop(TestTimeouts.AwaitAssertMax);
        await secondAttached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        gateway.Tell(new FetchDomain(new Location("lucerne", 0, 0), DomainKind.Weather));

        var completed = await ExpectMsgAsync<FetchDomainCompleted>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        var received = await secondForecasts.ExpectMsgAsync<DomainDataReceived>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Weather, completed.Domain),
            () => Assert.Equal("lucerne", received.Location.Name));
        await AwaitAssertAsync(() => Assert.True(Interlocked.Read(ref reattaches) >= 1,
            $"expected the re-attach to be counted, got {Interlocked.Read(ref reattaches)}"), TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_sink_reply_for_another_request_is_ignored()
    {
        var ct = TestContext.Current.CancellationToken;
        var attached = CreateTestProbe();
        var stale = Sys.ActorOf(Props.Create(() => new StalePipeline(attached.Ref)), $"pipeline-{Guid.NewGuid():N}");
        ActorRegistry.Register<IForecastPipeline>(stale, overwrite: true);
        var gateway = StartGateway();
        await attached.ExpectMsgAsync<string>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        gateway.Tell(Fetch());

        var rejected = await ExpectMsgAsync<FetchDomainRejected>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(FetchRejectionReason.PipelineUnavailable, rejected.Reason);
    }

    private sealed class BudgetProvider : IBudgetProvider
    {
        public BudgetRate GetCurrentRate() => new(480, 1_000_000);
    }

    private sealed class SilentActor : ReceiveActor
    {
        public SilentActor()
        {
            ReceiveAny(_ => { });
        }
    }

    // Vends a SinkRef per request (or never answers) and reports every request it received.
    private sealed class FakePipeline : ReceiveActor
    {
        public FakePipeline(IActorRef attached, IActorRef forecasts, bool answers)
        {
            var mat = Context.Materializer();
            ReceiveAsync<RequestForecastSink>(async msg =>
            {
                if (answers)
                {
                    var requester = Sender;
                    var sinkRef = await StreamRefs.SinkRef<DomainDataReceived>()
                        .To(Sink.ActorRef<DomainDataReceived>(forecasts, "completed", _ => "failed"))
                        .Run(mat);
                    requester.Tell(new RequestForecastSinkCompleted(msg.RequestId, sinkRef), Self);
                }

                attached.Tell("requested");
            });
        }
    }

    // Answers with a SinkRef under a request id the gateway never used.
    private sealed class StalePipeline : ReceiveActor
    {
        public StalePipeline(IActorRef attached)
        {
            var mat = Context.Materializer();
            ReceiveAsync<RequestForecastSink>(async msg =>
            {
                var requester = Sender;
                var sinkRef = await StreamRefs.SinkRef<DomainDataReceived>().To(Sink.Ignore<DomainDataReceived>()).Run(mat);
                requester.Tell(new RequestForecastSinkCompleted(msg.RequestId + 1000, sinkRef), Self);
                attached.Tell("answered");
            });
        }
    }
}
