using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Alerts.Configuration;
using Njord.Consensus;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Messages.Alerts;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Pipeline.Configuration;
using Njord.Tests.Shared;
using Njord.Trends.Configuration;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineSettingsSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static AnalysisSettings SettingsWith(AlertOptions alerts) =>
        AnalysisSettingsFactory.Create(
            new NjordOptions { Locations = [new LocationOptions { Name = AnalysisFixture.Lucerne }] },
            AppSettingsDefaults.Options<ConsensusOptions>(), alerts, AppSettingsDefaults.Options<IndexOptions>(), AppSettingsDefaults.Options<TrendOptions>(), AppSettingsDefaults.Options<DerivedOptions>());

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_settings_change_applies_from_the_next_forecast_on()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixture = AnalysisFixture.Create();
        var forecasts = fixture.Forecasts.Where(f => f.Location == AnalysisFixture.Lucerne).ToList();
        var pipeline = Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            fixture.Time,
            new ConsensusBuilder(AnalysisFixture.Parameters, fixture.Time),
            Options.Create(AppSettingsDefaults.Options<ConsensusOptions>()),
            FeatureServices.Alerts(fixture.Time),
            DisabledAnalyzers.Derived(),
            DisabledAnalyzers.Trend(),
            DisabledAnalyzers.Index(),
            SettingsWith(AppSettingsDefaults.Options<AlertOptions>()))), $"pipeline-{Guid.NewGuid():N}");

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(new RequestPipelineOutput(1), TimeSpan.FromSeconds(9), ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var probe = CreateTestProbe();
        completed.SourceRef.Source
            .Where(e => e is PipelineEvent.AlertComputed)
            .Select(e => (PipelineEvent.AlertComputed)e)
            .RunWith(Sink.ActorRef<PipelineEvent.AlertComputed>(probe.Ref, "done", ex => ex), Sys.Materializer());
        var queue = await PipelineProducer.AttachAsync(pipeline, Sys, ct);

        await queue.OfferAsync(forecasts[0], ct);
        await queue.OfferAsync(forecasts[1], ct);
        await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        var beforeChange = await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        Assert.NotEqual(AlertSeverity.None, FrostSeverity(beforeChange));

        pipeline.Tell(new AnalysisSettingsChanged(SettingsWith(AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [-50, -60, -70]))));

        await AwaitAssertAsync(async () =>
        {
            await queue.OfferAsync(forecasts[2], ct);
            var next = await probe.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
            Assert.Equal(AlertSeverity.None, FrostSeverity(next));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_started_pipeline_analyzes_with_the_current_settings_of_the_config_manager()
    {
        var ct = TestContext.Current.CancellationToken;
        // Every forecast is far below these thresholds; the startup thresholds (0, -5, -15) never reach red.
        var current = SettingsWith(AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [60, 55, 50]));
        ActorRegistry.Register<IConfigManager>(
            Sys.ActorOf(Props.Create(() => new FakeConfigManager(current)), $"config-{Guid.NewGuid():N}"), overwrite: true);
        var (forecasts, queue, alerts) = await StartAsync(ct);

        // No AnalysisSettingsChanged is sent: only the answer to the pipeline's start query can raise the thresholds.
        var index = 0;
        await AwaitAssertAsync(async () =>
        {
            await queue.OfferAsync(forecasts[index++ % forecasts.Count], ct);
            var next = await alerts.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
            Assert.Equal(AlertSeverity.Red, FrostSeverity(next));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_pipeline_without_a_config_manager_analyzes_with_the_startup_settings()
    {
        var ct = TestContext.Current.CancellationToken;
        var (forecasts, queue, alerts) = await StartAsync(ct);

        await queue.OfferAsync(forecasts[0], ct);
        await queue.OfferAsync(forecasts[1], ct);

        await alerts.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        var second = await alerts.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        Assert.NotEqual(AlertSeverity.None, FrostSeverity(second));
    }

    private async Task<(List<WeatherForecast> Forecasts, ISourceQueueWithComplete<WeatherForecast> Queue, Akka.TestKit.TestProbe Alerts)>
        StartAsync(CancellationToken ct)
    {
        var fixture = AnalysisFixture.Create();
        var forecasts = fixture.Forecasts.Where(f => f.Location == AnalysisFixture.Lucerne).ToList();
        var pipeline = Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            fixture.Time,
            new ConsensusBuilder(AnalysisFixture.Parameters, fixture.Time),
            Options.Create(AppSettingsDefaults.Options<ConsensusOptions>()),
            FeatureServices.Alerts(fixture.Time),
            DisabledAnalyzers.Derived(),
            DisabledAnalyzers.Trend(),
            DisabledAnalyzers.Index(),
            SettingsWith(AppSettingsDefaults.Options<AlertOptions>()))), $"pipeline-{Guid.NewGuid():N}");

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(new RequestPipelineOutput(1), TimeSpan.FromSeconds(9), ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var probe = CreateTestProbe();
        completed.SourceRef.Source
            .Where(e => e is PipelineEvent.AlertComputed)
            .Select(e => (PipelineEvent.AlertComputed)e)
            .RunWith(Sink.ActorRef<PipelineEvent.AlertComputed>(probe.Ref, "done", ex => ex), Sys.Materializer());
        var queue = await PipelineProducer.AttachAsync(pipeline, Sys, ct);
        return (forecasts, queue, probe);
    }

    private sealed class FakeConfigManager : ReceiveActor
    {
        public FakeConfigManager(AnalysisSettings current)
        {
            Receive<QueryAnalysisSettings>(_ => Sender.Tell(new AnalysisSettingsResult(current)));
        }
    }

    private static AlertSeverity FrostSeverity(PipelineEvent.AlertComputed computed) =>
        computed.Result.Alerts.Single(a => a.Type == AlertType.Frost).Severity;
}
