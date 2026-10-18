using System.Collections.Immutable;
using System.Threading.Channels;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Consensus;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Derived.Configuration;
using Njord.Indices;
using Njord.Indices.Configuration;
using Njord.Indices.Scores;
using Njord.Messages.Config;
using Njord.Messages.Indices;
using Njord.Messages.Pipeline;
using Njord.Messages.Sensors;
using Njord.Pipeline.Configuration;
using Njord.Sensors;
using Njord.Tests.Shared;
using Njord.Trends.Configuration;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineSensorSpec : NjordActorSpec
{
    private const double LiveIndoorTemperature = 24.5;
    private const int EventsPerForecast = 4;

    // Bound for one batch of forecasts inside an AwaitAssert retry; the analysis itself takes milliseconds.
    private static readonly TimeSpan EventWait = TimeSpan.FromSeconds(5);

    protected override bool UseTestPersistence => false;

    private static readonly AnalysisSettings Settings = AnalysisSettingsFactory.Create(
        new NjordOptions
        {
            Locations = [new LocationOptions { Name = AnalysisFixture.Lucerne }, new LocationOptions { Name = AnalysisFixture.Bern }],
        },
        AppSettingsDefaults.Options<ConsensusOptions>(), AppSettingsDefaults.Options<AlertOptions>(), AppSettingsDefaults.Options<IndexOptions>(o => o.Enabled = true), AppSettingsDefaults.Options<TrendOptions>(), AppSettingsDefaults.Options<DerivedOptions>());

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_pushed_indoor_temperature_of_the_forecast_location_reaches_the_indices()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, pipeline, queue, events) = await StartPipelineAsync(ct);

        pipeline.Tell(Push(new LocationId(AnalysisFixture.Lucerne)));

        var expectedLive = 0;
        var expectedConfigured = 0;
        var actual = 0;
        await AwaitAssertAsync(async () =>
        {
            var (consensus, index) = await AnalyzeAsync(fixture, queue, events, AnalysisFixture.Lucerne, ct);
            expectedLive = ExpectedNightVentilation(fixture, consensus, AnalysisFixture.Lucerne, LiveIndoorTemperature);
            expectedConfigured = ExpectedNightVentilation(fixture, consensus, AnalysisFixture.Lucerne, ConfiguredIndoorTemp(AnalysisFixture.Lucerne));
            actual = index.Days[0].NightVentilation;
            Assert.Equal(expectedLive, actual);
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(100), cancellationToken: ct);

        Assert.NotEqual(expectedConfigured, expectedLive);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_started_pipeline_asks_the_sensor_hub_for_the_current_map()
    {
        var ct = TestContext.Current.CancellationToken;
        var current = Push(new LocationId(AnalysisFixture.Lucerne));
        ActorRegistry.Register<ISensorHub>(
            Sys.ActorOf(Props.Create(() => new FakeSensorHub(current)), $"sensor-hub-{Guid.NewGuid():N}"), overwrite: true);
        var (fixture, _, queue, events) = await StartPipelineAsync(ct);

        // Nothing is pushed: only the answer to the pipeline's start request carries the live reading.
        await AwaitAssertAsync(async () =>
        {
            var (consensus, index) = await AnalyzeAsync(fixture, queue, events, AnalysisFixture.Lucerne, ct);
            Assert.Equal(
                ExpectedNightVentilation(fixture, consensus, AnalysisFixture.Lucerne, LiveIndoorTemperature),
                index.Days[0].NightVentilation);
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(100), cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Sensor_readings_of_another_location_are_not_used()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, pipeline, queue, events) = await StartPipelineAsync(ct);

        pipeline.Tell(Push(new LocationId(AnalysisFixture.Bern)));

        // Bern's index reflecting the live value proves the pushed map is in effect.
        await AwaitAssertAsync(async () =>
        {
            var (bernConsensus, bernIndex) = await AnalyzeAsync(fixture, queue, events, AnalysisFixture.Bern, ct);
            Assert.Equal(
                ExpectedNightVentilation(fixture, bernConsensus, AnalysisFixture.Bern, LiveIndoorTemperature),
                bernIndex.Days[0].NightVentilation);
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(100), cancellationToken: ct);

        var (consensus, index) = await AnalyzeAsync(fixture, queue, events, AnalysisFixture.Lucerne, ct);
        Assert.Equal(
            ExpectedNightVentilation(fixture, consensus, AnalysisFixture.Lucerne, ConfiguredIndoorTemp(AnalysisFixture.Lucerne)),
            index.Days[0].NightVentilation);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_forecast_is_analyzed_with_configured_values_before_any_push()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, _, queue, events) = await StartPipelineAsync(ct);

        var received = await ReceiveAsync(fixture, queue, events, AnalysisFixture.Lucerne, ct);

        var consensus = received.OfType<PipelineEvent.ConsensusComputed>().Last().Consensus;
        var index = received.OfType<PipelineEvent.IndexComputed>().Last().Result;
        var forecasts = LocationForecasts(fixture, AnalysisFixture.Lucerne).Count;
        Assert.Multiple(
            () => Assert.Equal(forecasts, received.OfType<PipelineEvent.ForecastReceived>().Count()),
            () => Assert.Equal(forecasts, received.OfType<PipelineEvent.AlertComputed>().Count()),
            () => Assert.Equal(forecasts, received.OfType<PipelineEvent.IndexComputed>().Count()),
            () => Assert.Equal(
                ExpectedNightVentilation(fixture, consensus, AnalysisFixture.Lucerne, ConfiguredIndoorTemp(AnalysisFixture.Lucerne)),
                index.Days[0].NightVentilation));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_pushed_location_key_matches_the_forecast_location_case_insensitively()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, pipeline, queue, events) = await StartPipelineAsync(ct);

        pipeline.Tell(Push(new LocationId(AnalysisFixture.Lucerne.ToUpperInvariant())));

        await AwaitAssertAsync(async () =>
        {
            var (consensus, index) = await AnalyzeAsync(fixture, queue, events, AnalysisFixture.Lucerne, ct);
            Assert.Equal(
                ExpectedNightVentilation(fixture, consensus, AnalysisFixture.Lucerne, LiveIndoorTemperature),
                index.Days[0].NightVentilation);
        }, TestTimeouts.AwaitAssertMax, TimeSpan.FromMilliseconds(100), cancellationToken: ct);
    }

    private sealed class FakeSensorHub : ReceiveActor
    {
        public FakeSensorHub(LocationSensorReadings current)
        {
            Receive<RequestSensorReadings>(_ => Sender.Tell(current));
        }
    }

    private static LocationSensorReadings Push(LocationId location) =>
        new(ImmutableDictionary<LocationId, SensorReadings>.Empty.Add(location, new SensorReadings(
            location.Name,
            new Dictionary<SensorKind, AggregatedReading>
            {
                [SensorKind.IndoorTemperature] = new(LiveIndoorTemperature, 1, AnalysisFixture.Now),
            })));

    private static List<WeatherForecast> LocationForecasts(AnalysisFixture fixture, string location) =>
        fixture.Forecasts.Where(f => f.Location == location).ToList();

    private static double ConfiguredIndoorTemp(string location) =>
        Settings.Indices.Preferences[(new LocationId(location), IndexKind.NightVentilation)].IndoorTemp;

    private static int ExpectedNightVentilation(AnalysisFixture fixture, LocationConsensus consensus, string location, double indoorTemp)
    {
        var preferences = Settings.Indices.Preferences[(new LocationId(location), IndexKind.NightVentilation)] with { IndoorTemp = indoorTemp };
        var today = DaySlicer.Slice(consensus, fixture.Time.GetUtcNow()).First();
        var night = ScoreInputs.From(today.NightMeans, today.SunshinePercent);
        return NightVentilationScore.Compute(night.Temperature, night.Humidity, night.Wind, night.RainProbability, preferences);
    }

    // Offers every forecast of the location; the last one completes the model set, so its consensus has a scorable day.
    private static async Task<(LocationConsensus Consensus, IndexResult Index)> AnalyzeAsync(
        AnalysisFixture fixture, ISourceQueueWithComplete<WeatherForecast> queue, ChannelReader<PipelineEvent> events,
        string location, CancellationToken ct)
    {
        var received = await ReceiveAsync(fixture, queue, events, location, ct);
        var consensus = received.OfType<PipelineEvent.ConsensusComputed>().Last().Consensus;
        var index = received.OfType<PipelineEvent.IndexComputed>().Last().Result;
        Assert.NotEmpty(index.Days);
        return (consensus, index);
    }

    private static async Task<IReadOnlyList<PipelineEvent>> ReceiveAsync(
        AnalysisFixture fixture, ISourceQueueWithComplete<WeatherForecast> queue, ChannelReader<PipelineEvent> events,
        string location, CancellationToken ct)
    {
        var forecasts = LocationForecasts(fixture, location);
        foreach (var forecast in forecasts)
        {
            await queue.OfferAsync(forecast, ct);
        }

        var count = EventsPerForecast * forecasts.Count;
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

    private async Task<(AnalysisFixture Fixture, IActorRef Pipeline, ISourceQueueWithComplete<WeatherForecast> Queue, ChannelReader<PipelineEvent> Events)>
        StartPipelineAsync(CancellationToken ct)
    {
        var fixture = AnalysisFixture.Create();
        var pipeline = Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            fixture.Time,
            new ConsensusBuilder(AnalysisFixture.Parameters, fixture.Time),
            Options.Create(AppSettingsDefaults.Options<ConsensusOptions>()),
            FeatureServices.Alerts(fixture.Time),
            DisabledAnalyzers.Derived(),
            DisabledAnalyzers.Trend(),
            FeatureServices.Indices(fixture.Time),
            Settings)), $"pipeline-{Guid.NewGuid():N}");

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(new RequestPipelineOutput(1), TestTimeouts.RealTimeWaitMax, ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var events = Channel.CreateUnbounded<PipelineEvent>();
        completed.SourceRef.Source.To(Sink.ForEach<PipelineEvent>(e => events.Writer.TryWrite(e))).Run(Sys.Materializer());
        var queue = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        return (fixture, pipeline, queue, events.Reader);
    }
}
