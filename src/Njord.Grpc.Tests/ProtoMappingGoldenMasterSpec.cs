using System.Text;
using Google.Protobuf;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using static VerifyXunit.Verifier;

namespace Njord.Grpc.Tests;

public sealed class ProtoMappingGoldenMasterSpec
{
    private static readonly AnalysisFixture Fixture = AnalysisFixture.Create();

    private static readonly JsonFormatter Formatter = new(
        JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithIndentation());

    private static readonly JsonFormatter LineFormatter = new(
        JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    [Fact]
    public Task Consensus_events_match_golden_master()
    {
        var text = new StringBuilder();
        foreach (var analysis in Fixture.Analyses)
        {
            var mapped = EnrichmentEventMappings.ToEnrichmentEvent(
                new PipelineEvent.ConsensusComputed(analysis.Consensus, AnalysisFixture.Now));
            Assert.NotNull(mapped);
            text.AppendLine($"## {mapped.Location} {mapped.TypeName} {mapped.UpdatedAt}");
            AppendParameters("hourly", mapped.Consensus.HourlyParameters, text);
            AppendParameters("daily", mapped.Consensus.DailyParameters, text);
        }

        return Verify(text.ToString());
    }

    [Fact]
    public Task Alert_events_match_golden_master() =>
        VerifyEvents(a => new PipelineEvent.AlertComputed(a.Alerts, AnalysisFixture.Now));

    [Fact]
    public Task Derived_events_match_golden_master() =>
        VerifyEvents(a => new PipelineEvent.DerivedComputed(a.Derived, AnalysisFixture.Now));

    [Fact]
    public Task Trend_events_match_golden_master() =>
        VerifyEvents(a => new PipelineEvent.TrendComputed(a.Trends, AnalysisFixture.Now));

    [Fact]
    public Task Index_events_match_golden_master() =>
        VerifyEvents(a => new PipelineEvent.IndexComputed(a.Indices, AnalysisFixture.Now));

    [Fact]
    public Task Forecast_updates_match_golden_master()
    {
        var text = new StringBuilder();
        foreach (var forecast in Fixture.Forecasts.Where(f => f.Model == AnalysisFixture.Gfs))
        {
            var update = ForecastProtoMappings.ToProto(
                new PipelineEvent.ForecastReceived(new LocationId(forecast.Location), forecast.Model, forecast),
                AnalysisFixture.Now);
            text.AppendLine($"## {update.Location} {update.Model} {update.UpdatedAt}");
            foreach (var hourly in update.Hourly)
            {
                text.AppendLine(LineFormatter.Format(hourly));
            }

            foreach (var daily in update.Daily)
            {
                text.AppendLine(LineFormatter.Format(daily));
            }
        }

        return Verify(text.ToString());
    }

    [Fact]
    public void Fixture_maps_every_alert_type_into_the_alert_events()
    {
        var lucerne = Fixture.For(AnalysisFixture.Lucerne);
        var mapped = AlertProtoMappings.ToProto(lucerne.Alerts);

        var types = mapped.Alerts.Select(a => a.Type).ToHashSet();
        var expected = Enum.GetValues<AlertType>().Where(t => t != AlertType.Unspecified);
        Assert.All(expected, t => Assert.Contains(t, types));
        Assert.All(mapped.Alerts, a => Assert.NotEqual(AlertSeverity.None, a.Severity));
    }

    [Fact]
    public void Fixture_maps_every_index_kind_with_an_envelope()
    {
        var days = Fixture.Analyses.SelectMany(a => IndexProtoMappings.ToProto(a.Indices).Days).ToList();

        Assert.NotEmpty(days);
        Assert.All(days, d => Assert.Multiple(
            () => Assert.NotNull(d.LaundryEnvelope),
            () => Assert.NotNull(d.OutdoorEnvelope),
            () => Assert.NotNull(d.RunningEnvelope),
            () => Assert.NotNull(d.CyclingEnvelope),
            () => Assert.NotNull(d.BbqEnvelope),
            () => Assert.NotNull(d.IrrigationEnvelope),
            () => Assert.NotNull(d.SolarEnvelope),
            () => Assert.NotNull(d.NightVentilationEnvelope)));
    }

    private static void AppendParameters(
        string facet, IEnumerable<ParameterConsensus> parameters, StringBuilder text)
    {
        foreach (var parameter in parameters)
        {
            text.AppendLine($"# {facet} {parameter.Parameter} [{parameter.Unit}]");
            foreach (var horizon in parameter.ByHorizon)
            {
                text.AppendLine(LineFormatter.Format(horizon));
            }
        }
    }

    private static Task VerifyEvents(Func<LocationAnalysis, PipelineEvent> build)
    {
        var text = new StringBuilder();
        foreach (var analysis in Fixture.Analyses)
        {
            var mapped = EnrichmentEventMappings.ToEnrichmentEvent(build(analysis));
            Assert.NotNull(mapped);
            text.AppendLine(Formatter.Format(mapped));
        }

        return Verify(text.ToString());
    }
}
