using System.Diagnostics;
using System.Threading.Channels;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
using Njord.Indices;
using Njord.Indices.Scores;
using Njord.Messages.Pipeline;
using Njord.Messages.Sensors;
using Njord.Sensors;
using Njord.Tests.Shared;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineSensorSpec : NjordActorSpec
{
    private const double LiveIndoorTemperature = 24.5;
    private const int EventsPerForecast = 4;

    // The pipeline's sensor lookup timeout plus a bounded margin for the analysis itself.
    private static readonly TimeSpan EventWait = ForecastPipeline.SensorLookupTimeout + TimeSpan.FromSeconds(5);

    protected override bool UseTestPersistence => false;

    private static readonly AnalysisSettings Settings = AnalysisSettingsFactory.Create(
        new NjordOptions
        {
            Locations = [new LocationOptions { Name = AnalysisFixture.Lucerne }, new LocationOptions { Name = AnalysisFixture.Bern }],
        },
        new ConsensusOptions(), new AlertOptions(), new IndexOptions { Enabled = true }, new TrendOptions(), new DerivedOptions());

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_live_indoor_temperature_of_the_forecast_location_reaches_the_indices()
    {
        var ct = TestContext.Current.CancellationToken;
        var hub = RegisterSensorHub(new Dictionary<string, SensorSnapshot>
        {
            [AnalysisFixture.Lucerne] = IndoorTemperatureSnapshot(AnalysisFixture.Lucerne, LiveIndoorTemperature),
        });
        var (fixture, queue, events) = await StartPipelineAsync(ct);

        var forecasts = LucerneForecasts(fixture);
        foreach (var forecast in forecasts)
        {
            await queue.OfferAsync(forecast, ct);
        }

        var received = await ReceiveEventsAsync(events, EventsPerForecast * forecasts.Count, ct);

        // The last forecast completes the model set, so its consensus has a scorable day.
        var consensus = received.OfType<PipelineEvent.ConsensusComputed>().Last().Consensus;
        var index = received.OfType<PipelineEvent.IndexComputed>().Last().Result;
        Assert.NotEmpty(index.Days);
        var configuredIndoorTemp = NightVentilationPreferences().IndoorTemp;
        var expectedLive = ExpectedNightVentilation(fixture, consensus, LiveIndoorTemperature);
        var expectedConfigured = ExpectedNightVentilation(fixture, consensus, configuredIndoorTemp);
        Assert.Multiple(
            () => Assert.NotEqual(expectedConfigured, expectedLive),
            () => Assert.Equal(expectedLive, index.Days[0].NightVentilation),
            () => Assert.Equal(Enumerable.Repeat(new LocationId(AnalysisFixture.Lucerne), forecasts.Count), hub.Queries));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Sensor_readings_of_another_location_are_not_used()
    {
        var ct = TestContext.Current.CancellationToken;
        RegisterSensorHub(new Dictionary<string, SensorSnapshot>
        {
            [AnalysisFixture.Bern] = IndoorTemperatureSnapshot(AnalysisFixture.Bern, LiveIndoorTemperature),
        });
        var (fixture, queue, events) = await StartPipelineAsync(ct);

        var forecasts = LucerneForecasts(fixture);
        foreach (var forecast in forecasts)
        {
            await queue.OfferAsync(forecast, ct);
        }

        var received = await ReceiveEventsAsync(events, EventsPerForecast * forecasts.Count, ct);

        // The last forecast completes the model set, so its consensus has a scorable day.
        var consensus = received.OfType<PipelineEvent.ConsensusComputed>().Last().Consensus;
        var index = received.OfType<PipelineEvent.IndexComputed>().Last().Result;
        Assert.NotEmpty(index.Days);
        var expectedConfigured = ExpectedNightVentilation(fixture, consensus, NightVentilationPreferences().IndoorTemp);
        Assert.Equal(expectedConfigured, index.Days[0].NightVentilation);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_forecast_is_analyzed_when_no_sensor_hub_is_registered()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, queue, events) = await StartPipelineAsync(ct);

        await queue.OfferAsync(LucerneForecast(fixture), ct);
        var received = await ReceiveEventsAsync(events, EventsPerForecast, ct);

        AssertAllEventsOfOneForecast(received);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_forecast_is_analyzed_when_the_sensor_hub_does_not_reply()
    {
        var ct = TestContext.Current.CancellationToken;
        var hub = RegisterSensorHub(snapshots: null);
        var (fixture, queue, events) = await StartPipelineAsync(ct);

        var stopwatch = Stopwatch.StartNew();
        await queue.OfferAsync(LucerneForecast(fixture), ct);
        var received = await ReceiveEventsAsync(events, EventsPerForecast, ct);
        stopwatch.Stop();

        AssertAllEventsOfOneForecast(received);
        Assert.True(stopwatch.Elapsed < EventWait,
            $"The forecast took {stopwatch.Elapsed} to analyze, more than the lookup timeout plus margin ({EventWait}).");
        Assert.Equal([new LocationId(AnalysisFixture.Lucerne)], hub.Queries);
    }

    private static void AssertAllEventsOfOneForecast(IReadOnlyList<PipelineEvent> received) =>
        Assert.Multiple(
            () => Assert.Single(received.OfType<PipelineEvent.ForecastReceived>()),
            () => Assert.Single(received.OfType<PipelineEvent.ConsensusComputed>()),
            () => Assert.Single(received.OfType<PipelineEvent.AlertComputed>()),
            () => Assert.Single(received.OfType<PipelineEvent.IndexComputed>()));

    private static WeatherForecast LucerneForecast(AnalysisFixture fixture) => LucerneForecasts(fixture)[0];

    private static List<WeatherForecast> LucerneForecasts(AnalysisFixture fixture) =>
        fixture.Forecasts.Where(f => f.Location == AnalysisFixture.Lucerne).ToList();

    private static ResolvedPreferences NightVentilationPreferences() =>
        Settings.Indices.Preferences[(new LocationId(AnalysisFixture.Lucerne), IndexKind.NightVentilation)];

    private static int ExpectedNightVentilation(AnalysisFixture fixture, ConsensusSnapshot consensus, double indoorTemp)
    {
        var preferences = NightVentilationPreferences() with { IndoorTemp = indoorTemp };
        var today = DaySlicer.Slice(consensus, fixture.Time.GetUtcNow()).First();
        var night = ScoreInputs.From(today.NightMeans, today.SunshinePercent);
        return NightVentilationScore.Compute(night.Temperature, night.Humidity, night.Wind, night.RainProbability, preferences);
    }

    private static SensorSnapshot IndoorTemperatureSnapshot(string location, double value) =>
        new(location, new Dictionary<SensorKind, AggregatedReading>
        {
            [SensorKind.IndoorTemperature] = new(value, 1, AnalysisFixture.Now),
        });

    private FakeSensorHub RegisterSensorHub(IReadOnlyDictionary<string, SensorSnapshot>? snapshots)
    {
        var hub = new FakeSensorHub(snapshots);
        ActorRegistry.Register<ISensorHub>(
            Sys.ActorOf(Props.Create(() => new FakeSensorHubActor(hub)), $"sensor-hub-{Guid.NewGuid():N}"),
            overwrite: true);
        return hub;
    }

    private async Task<(AnalysisFixture Fixture, ISourceQueueWithComplete<WeatherForecast> Queue, ChannelReader<PipelineEvent> Events)>
        StartPipelineAsync(CancellationToken ct)
    {
        var fixture = AnalysisFixture.Create();
        var queueReady = new TaskCompletionSource<ISourceQueueWithComplete<WeatherForecast>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ActorRegistry.Register<IForecastGateway>(
            Sys.ActorOf(Props.Create(() => new FakeGateway(queueReady)), $"gateway-{Guid.NewGuid():N}"),
            overwrite: true);

        var pipeline = Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            fixture.Time,
            new ConsensusBuilder(AnalysisFixture.Parameters, fixture.Time),
            FeatureServices.Alerts(fixture.Time),
            DisabledAnalyzers.Derived(),
            DisabledAnalyzers.Trend(),
            FeatureServices.Indices(fixture.Time),
            Settings)), $"pipeline-{Guid.NewGuid():N}");

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(new RequestPipelineOutput(1), TimeSpan.FromSeconds(9), ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var events = Channel.CreateUnbounded<PipelineEvent>();
        completed.SourceRef.Source.To(Sink.ForEach<PipelineEvent>(e => events.Writer.TryWrite(e))).Run(Sys.Materializer());
        var queue = await queueReady.Task.WaitAsync(TimeSpan.FromSeconds(9), ct);
        return (fixture, queue, events.Reader);
    }

    private static async Task<IReadOnlyList<PipelineEvent>> ReceiveEventsAsync(
        ChannelReader<PipelineEvent> events, int count, CancellationToken ct)
    {
        var received = new List<PipelineEvent>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(EventWait);
        try
        {
            while (received.Count < count)
            {
                received.Add(await events.ReadAsync(deadline.Token));
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Assert.Fail(
                $"Expected {count} pipeline events within {EventWait}, got {received.Count}: " +
                $"[{string.Join(", ", received.Select(e => e.GetType().Name))}].");
        }

        return received;
    }

    private sealed class FakeSensorHub(IReadOnlyDictionary<string, SensorSnapshot>? snapshots)
    {
        private readonly List<LocationId> _queries = [];

        public IReadOnlyDictionary<string, SensorSnapshot>? Snapshots { get; } = snapshots;

        public IReadOnlyList<LocationId> Queries
        {
            get
            {
                lock (_queries)
                {
                    return [.. _queries];
                }
            }
        }

        public void Record(LocationId location)
        {
            lock (_queries)
            {
                _queries.Add(location);
            }
        }
    }

    // Answers per location; a hub without snapshots never replies.
    private sealed class FakeSensorHubActor : ReceiveActor
    {
        public FakeSensorHubActor(FakeSensorHub hub)
        {
            Receive<QuerySensorSnapshot>(msg =>
            {
                hub.Record(msg.Location);
                if (hub.Snapshots is null)
                {
                    return;
                }

                Sender.Tell(hub.Snapshots.TryGetValue(msg.Location.Name, out var snapshot)
                    ? new SensorSnapshotResult(snapshot)
                    : new SensorSnapshotNotAvailable(msg.Location));
            });
        }
    }

    private sealed class FakeGateway : ReceiveActor
    {
        public FakeGateway(TaskCompletionSource<ISourceQueueWithComplete<WeatherForecast>> queueReady)
        {
            var mat = Context.Materializer();

            Receive<RequestForecastStream>(msg =>
            {
                var (queue, source) = Source.Queue<WeatherForecast>(16, OverflowStrategy.Backpressure).PreMaterialize(mat);
                queueReady.TrySetResult(queue);
                source.RunWith(StreamRefs.SourceRef<WeatherForecast>(), mat)
                    .PipeTo(Sender,
                        success: sourceRef => new RequestForecastStreamCompleted(msg.RequestId, sourceRef),
                        failure: ex => new RequestForecastStreamFailed(msg.RequestId, ex));
            });
        }
    }
}
