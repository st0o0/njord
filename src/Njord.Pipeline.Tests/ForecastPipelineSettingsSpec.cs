using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
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
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineSettingsSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static AnalysisSettings SettingsWith(AlertOptions alerts) =>
        AnalysisSettingsFactory.Create(
            new NjordOptions { Locations = [new LocationOptions { Name = AnalysisFixture.Lucerne }] },
            AppSettingsDefaults.Options<ConsensusOptions>(), alerts, AppSettingsDefaults.Options<IndexOptions>(), AppSettingsDefaults.Options<TrendOptions>(), AppSettingsDefaults.Options<DerivedOptions>());

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_pipeline_analyzes_with_the_settings_it_was_constructed_with()
    {
        var ct = TestContext.Current.CancellationToken;
        var (forecasts, queue, alerts) = await StartAsync(ct);

        await queue.OfferAsync(forecasts[0], ct);
        await queue.OfferAsync(forecasts[1], ct);

        await alerts.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        var second = await alerts.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        Assert.NotEqual(AlertSeverity.None, FrostSeverity(second));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Constructor_injected_thresholds_decide_the_alert_severity()
    {
        var ct = TestContext.Current.CancellationToken;
        // Every forecast is far below these thresholds; the default thresholds (0, -5, -15) never reach red.
        var (forecasts, queue, alerts) = await StartAsync(ct,
            SettingsWith(AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [60, 55, 50])));

        await queue.OfferAsync(forecasts[0], ct);
        await queue.OfferAsync(forecasts[1], ct);

        await alerts.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        var second = await alerts.ExpectMsgAsync<PipelineEvent.AlertComputed>(cancellationToken: ct);
        Assert.Equal(AlertSeverity.Red, FrostSeverity(second));
    }

    private async Task<(List<WeatherForecast> Forecasts, ISourceQueueWithComplete<WeatherForecast> Queue, Akka.TestKit.TestProbe Alerts)>
        StartAsync(CancellationToken ct, AnalysisSettings? settings = null)
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
            settings ?? SettingsWith(AppSettingsDefaults.Options<AlertOptions>()))), $"pipeline-{Guid.NewGuid():N}");

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

    private static AlertSeverity FrostSeverity(PipelineEvent.AlertComputed computed) =>
        computed.Result.Alerts.Single(a => a.Type == AlertType.Frost).Severity;
}
