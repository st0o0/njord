using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Messages.Egress;
using Njord.Messages.Pipeline;
using Njord.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Egress.Tests;

public sealed class ModelStateActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor().AddFastRetryBackoff();
    }

    private static readonly DateTimeOffset Anchor = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreateModelStateActor()
    {
        var options = DefaultOptions();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return Sys.ActorOf(Props.Create(() => new ModelStateActor(
            Microsoft.Extensions.Options.Options.Create(options),
            parameters)));
    }

    [Fact(Timeout = 15000)]
    public async Task Requests_pipeline_source_on_startup()
    {
        var registry = ActorRegistry;
        var mat = Sys.Materializer();
        var pipelineProbe = CreateTestProbe();

        var fakePipeline = Sys.ActorOf(FakePipelineSource.Props(mat, requestProbe: pipelineProbe));

        registry.Register<IPipelineActor>(fakePipeline, overwrite: true);

        CreateModelStateActor();

        await pipelineProbe.ExpectMsgAsync<RequestPipelineSource>(cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 5000)]
    public async Task Re_requests_pipeline_source_after_pipeline_source_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var pipelineProbe = CreateTestProbe();
        ActorRegistry.Register<IPipelineActor>(Sys.ActorOf(FailingRefProvider.Props(pipelineProbe)), overwrite: true);

        CreateModelStateActor();

        await pipelineProbe.ExpectMsgAsync<RequestPipelineSource>(cancellationToken: ct);
        await pipelineProbe.ExpectMsgAsync<RequestPipelineSource>(cancellationToken: ct);
    }

    [Fact(Timeout = 15000)]
    public async Task First_fetch_emits_capability_learned_into_broadcast()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var forecast = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = Sys.ActorOf(FeedingPipelineSource.Props(mat, forecast));

        registry.Register<IPipelineActor>(fakePipeline, overwrite: true);

        var actor = CreateModelStateActor();

        var sourceResponse = await actor.Ask<ModelStateSourceResponse>(
            new RequestModelStateSource(0), TimeSpan.FromSeconds(5), ct);

        var eventProbe = CreateTestProbe();
        sourceResponse.SourceRef.Source
            .RunWith(Sink.ActorRef<EgressEvent>(eventProbe, "completed", ex => new Akka.Actor.Status.Failure(ex)), mat);

        var msg1 = await eventProbe.ExpectMsgAsync<EgressEvent>(cancellationToken: ct);
        var msg2 = await eventProbe.ExpectMsgAsync<EgressEvent>(cancellationToken: ct);
        var events = new[] { msg1, msg2 };

        var capEvent = events.OfType<EgressEvent.CapabilityLearned>().Single();
        Assert.Equal("lucerne", capEvent.Location);
        Assert.Equal("icon_d2", capEvent.Model.Id);
        Assert.NotEmpty(capEvent.SupportedParameters);
        Assert.DoesNotContain(72, capEvent.ApplicableHorizons);

        Assert.Single(events.OfType<EgressEvent.PerModelUpdate>());
    }

    [Fact(Timeout = 15000)]
    public async Task Unchanged_capability_set_does_not_re_emit()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var forecast1 = CreateForecast("icon_d2", withNullParams: false);
        var forecast2 = CreateForecast("icon_d2", withNullParams: false, tempOffset: 1.0);
        var fakePipeline = Sys.ActorOf(FeedingPipelineSource.Props(mat, forecast1, forecast2));

        registry.Register<IPipelineActor>(fakePipeline, overwrite: true);

        var actor = CreateModelStateActor();

        var sourceResponse = await actor.Ask<ModelStateSourceResponse>(
            new RequestModelStateSource(0), TimeSpan.FromSeconds(5), ct);

        var eventProbe = CreateTestProbe();
        sourceResponse.SourceRef.Source
            .RunWith(Sink.ActorRef<EgressEvent>(eventProbe, "completed", ex => new Akka.Actor.Status.Failure(ex)), mat);

        var events = new List<EgressEvent>();
        for (var i = 0; i < 3; i++)
        {
            events.Add(await eventProbe.ExpectMsgAsync<EgressEvent>(cancellationToken: ct));
        }

        Assert.Single(events.OfType<EgressEvent.CapabilityLearned>());
    }

    [Fact(Timeout = 15000)]
    public async Task Expanded_capability_set_triggers_update()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var forecast1 = CreateForecast("icon_d2", withNullParams: true);
        var forecast2 = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = Sys.ActorOf(FeedingPipelineSource.Props(mat, forecast1, forecast2));

        registry.Register<IPipelineActor>(fakePipeline, overwrite: true);

        var actor = CreateModelStateActor();

        var sourceResponse = await actor.Ask<ModelStateSourceResponse>(
            new RequestModelStateSource(0), TimeSpan.FromSeconds(5), ct);

        var eventProbe = CreateTestProbe();
        sourceResponse.SourceRef.Source
            .RunWith(Sink.ActorRef<EgressEvent>(eventProbe, "completed", ex => new Akka.Actor.Status.Failure(ex)), mat);

        var events = new List<EgressEvent>();
        for (var i = 0; i < 4; i++)
        {
            events.Add(await eventProbe.ExpectMsgAsync<EgressEvent>(cancellationToken: ct));
        }

        var capEvents = events.OfType<EgressEvent.CapabilityLearned>()
            .OrderBy(m => m.SupportedParameters.Count).ToList();
        Assert.Equal(2, capEvents.Count);
        Assert.True(capEvents[1].SupportedParameters.Count > capEvents[0].SupportedParameters.Count);
    }

    [Fact(Timeout = 15000)]
    public async Task Horizon_capping_excludes_72h_for_icon_d2()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = ActorRegistry;
        var mat = Sys.Materializer();

        var forecast = CreateForecast("icon_d2", withNullParams: false);
        var fakePipeline = Sys.ActorOf(FeedingPipelineSource.Props(mat, forecast));

        registry.Register<IPipelineActor>(fakePipeline, overwrite: true);

        var actor = CreateModelStateActor();

        var sourceResponse = await actor.Ask<ModelStateSourceResponse>(
            new RequestModelStateSource(0), TimeSpan.FromSeconds(5), ct);

        var eventProbe = CreateTestProbe();
        sourceResponse.SourceRef.Source
            .RunWith(Sink.ActorRef<EgressEvent>(eventProbe, "completed", ex => new Akka.Actor.Status.Failure(ex)), mat);

        var cap = (EgressEvent.CapabilityLearned)await eventProbe.FishForMessageAsync(msg => msg is EgressEvent.CapabilityLearned, cancellationToken: ct);
        Assert.Contains(3, cap.ApplicableHorizons);
        Assert.Contains(24, cap.ApplicableHorizons);
        Assert.Contains(48, cap.ApplicableHorizons);
        Assert.DoesNotContain(72, cap.ApplicableHorizons);
        Assert.Equal(3, cap.ApplicableDayOffsets.Count);
    }

    private static ModelForecast CreateForecast(string modelId, bool withNullParams, double tempOffset = 0.0)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;

        var points = Enumerable.Range(0, 60)
            .Select(i => new ForecastPoint(
                Anchor.AddHours(i + 1),
                new Dictionary<ParameterDef, double?>
                {
                    [temp] = 20.0 + i + tempOffset,
                    [wind] = withNullParams ? null : 5.0 + i * 0.1,
                }))
            .ToList();

        return new ModelForecast(
            new WeatherModel(modelId), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    // -- fakes ---------------------------------------------------------------

    private sealed class FakePipelineSource : ReceiveActor
    {
        public FakePipelineSource(IMaterializer mat, IActorRef? requestProbe = null)
        {
            Receive<RequestPipelineSource>(msg =>
            {
                requestProbe?.Tell(msg);
                Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new PipelineSourceResponse(msg.RequestId, sr),
                        _ => null!);
            });
        }

        public static Props Props(IMaterializer mat, IActorRef? requestProbe = null) =>
            Akka.Actor.Props.Create(() => new FakePipelineSource(mat, requestProbe));
    }

    private sealed class FeedingPipelineSource : ReceiveActor
    {
        public FeedingPipelineSource(IMaterializer mat, params ModelForecast[] forecasts)
        {
            Receive<RequestPipelineSource>(msg =>
            {
                var outcomes = forecasts.Select(f => (FetchOutcome)new FetchOutcome.Success(f));
                Source.From(outcomes)
                    .Concat(Source.Never<FetchOutcome>())
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new PipelineSourceResponse(msg.RequestId, sr),
                        _ => null!);
            });
        }

        public static Props Props(IMaterializer mat, params ModelForecast[] forecasts) =>
            Akka.Actor.Props.Create(() => new FeedingPipelineSource(mat, forecasts));
    }
}
