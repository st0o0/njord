using System.Threading.Channels;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Compute.Consensus;
using Njord.Compute.Settings;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
using Njord.Derived;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline.Tests;

public sealed class ForecastPipelineConsensusToggleSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static AnalysisSettings Settings(AlertOptions? alerts = null) =>
        AnalysisSettingsFactory.Create(
            new NjordOptions
            {
                Locations = [new LocationOptions { Name = AnalysisFixture.Lucerne }],
                Horizons = [.. AnalysisFixture.Horizons],
            },
            AppSettingsDefaults.Options<ConsensusOptions>(), alerts ?? AppSettingsDefaults.Options<AlertOptions>(), AnalysisFixture.IndexOptions,
            AppSettingsDefaults.Options<TrendOptions>(o => o.Enabled = true), new DerivedOptions { Enabled = true });

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Disabled_consensus_emits_no_consensus_but_every_other_analysis()
    {
        var ct = TestContext.Current.CancellationToken;
        var (forecasts, _, queue, events) = await StartPipelineAsync(consensusEnabled: false, ct);

        foreach (var forecast in forecasts)
        {
            await queue.OfferAsync(forecast, ct);
        }

        var received = await ReceiveUntilIndicesAsync(events, forecasts.Count, ct);

        Assert.Multiple(
            () => Assert.DoesNotContain(received, e => e is PipelineEvent.ConsensusComputed),
            () => Assert.Equal(forecasts.Count, received.OfType<PipelineEvent.ForecastReceived>().Count()),
            () => Assert.Equal(forecasts.Count, received.OfType<PipelineEvent.AlertComputed>().Count()),
            () => Assert.Equal(forecasts.Count, received.OfType<PipelineEvent.DerivedComputed>().Count()),
            () => Assert.Equal(forecasts.Count, received.OfType<PipelineEvent.IndexComputed>().Count()));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Disabled_consensus_still_feeds_trends_the_previous_snapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        var (forecasts, _, queue, events) = await StartPipelineAsync(consensusEnabled: false, ct);

        foreach (var forecast in forecasts)
        {
            await queue.OfferAsync(forecast, ct);
        }

        var received = await ReceiveUntilIndicesAsync(events, forecasts.Count, ct);

        var trends = received.OfType<PipelineEvent.TrendComputed>().ToList();
        Assert.Equal(forecasts.Count - 1, trends.Count);
        Assert.All(trends, t => Assert.Equal(AnalysisFixture.Lucerne, t.Result.Location.Name));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Enabled_consensus_emits_a_consensus_event_for_every_forecast()
    {
        var ct = TestContext.Current.CancellationToken;
        var (forecasts, _, queue, events) = await StartPipelineAsync(consensusEnabled: true, ct);

        foreach (var forecast in forecasts)
        {
            await queue.OfferAsync(forecast, ct);
        }

        var received = await ReceiveUntilIndicesAsync(events, forecasts.Count, ct);

        var consensus = received.OfType<PipelineEvent.ConsensusComputed>().ToList();
        Assert.Equal(forecasts.Count, consensus.Count);
        Assert.All(consensus, c => Assert.Equal(AnalysisFixture.Lucerne, c.Consensus.Location.Name));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_runtime_settings_change_does_not_bring_back_disabled_consensus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (forecasts, pipeline, queue, events) = await StartPipelineAsync(consensusEnabled: false, ct);

        await queue.OfferAsync(forecasts[0], ct);
        var beforeChange = await ReceiveUntilIndicesAsync(events, 1, ct);

        pipeline.Tell(new AnalysisSettingsChanged(Settings(AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [-50, -60, -70]))));
        for (var i = 1; i < forecasts.Count; i++)
        {
            await queue.OfferAsync(forecasts[i], ct);
        }

        var afterChange = await ReceiveUntilIndicesAsync(events, forecasts.Count - 1, ct);

        Assert.Multiple(
            () => Assert.DoesNotContain(beforeChange, e => e is PipelineEvent.ConsensusComputed),
            () => Assert.DoesNotContain(afterChange, e => e is PipelineEvent.ConsensusComputed),
            () => Assert.Equal(forecasts.Count - 1, afterChange.OfType<PipelineEvent.AlertComputed>().Count()));
    }

    private async Task<(IReadOnlyList<WeatherForecast> Forecasts, IActorRef Pipeline,
        ISourceQueueWithComplete<WeatherForecast> Queue, ChannelReader<PipelineEvent> Events)>
        StartPipelineAsync(bool consensusEnabled, CancellationToken ct)
    {
        var fixture = AnalysisFixture.Create();
        var pipeline = Sys.ActorOf(Props.Create(() => new ForecastPipeline(
            fixture.Time,
            new ConsensusBuilder(AnalysisFixture.Parameters, fixture.Time),
            Options.Create(new ConsensusOptions { Enabled = consensusEnabled }),
            FeatureServices.Alerts(fixture.Time),
            new DerivedAnalyzer(Options.Create(new DerivedOptions { Enabled = true })),
            new TrendAnalyzer(Options.Create(new TrendOptions { Enabled = true })),
            FeatureServices.Indices(fixture.Time),
            Settings(null))), $"pipeline-{Guid.NewGuid():N}");

        var output = await pipeline.Ask<RequestPipelineOutputResponse>(
            new RequestPipelineOutput(1), TestTimeouts.RealTimeWaitMax, ct);
        var completed = Assert.IsType<RequestPipelineOutputCompleted>(output);
        var events = Channel.CreateUnbounded<PipelineEvent>();
        completed.SourceRef.Source.To(Sink.ForEach<PipelineEvent>(e => events.Writer.TryWrite(e))).Run(Sys.Materializer());
        var queue = await PipelineProducer.AttachAsync(pipeline, Sys, ct);

        var forecasts = fixture.Forecasts.Where(f => f.Location == AnalysisFixture.Lucerne).ToList();
        return (forecasts, pipeline, queue, events.Reader);
    }

    // The index event is the last one the pipeline emits per forecast, so everything a forecast produced
    // has arrived once its index event has.
    private static async Task<IReadOnlyList<PipelineEvent>> ReceiveUntilIndicesAsync(
        ChannelReader<PipelineEvent> events, int forecasts, CancellationToken ct)
    {
        var received = new List<PipelineEvent>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TestTimeouts.RealTimeWaitMax);
        try
        {
            while (received.OfType<PipelineEvent.IndexComputed>().Count() < forecasts)
            {
                received.Add(await events.ReadAsync(deadline.Token));
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Assert.Fail(
                $"Expected the events of {forecasts} forecasts within {TestTimeouts.RealTimeWaitMax}, got " +
                $"[{string.Join(", ", received.Select(e => e.GetType().Name))}].");
        }

        return received;
    }
}
