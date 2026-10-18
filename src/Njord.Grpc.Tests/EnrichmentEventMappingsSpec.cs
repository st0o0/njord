using Njord.Consensus;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using DomainAlert = Njord.Messages.Alerts.Alert;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainHorizonConsensus = Njord.Consensus.HorizonConsensus;
using DomainHorizonDerived = Njord.Messages.Derived.HorizonDerived;
using DomainParameterConsensus = Njord.Consensus.ParameterConsensus;
using DomainParameterTrend = Njord.Messages.Trends.ParameterTrend;
using DomainScalarDerived = Njord.Messages.Derived.ScalarDerived;

namespace Njord.Grpc.Tests;

public sealed class EnrichmentEventMappingsSpec
{
    [Fact]
    public void ToEnrichmentEvent_should_wrap_alert_result_in_enrichment_event()
    {
        var alertResult = new AlertResult(new LocationId("lucerne"),
        [
            new DomainAlert(DomainAlertType.Heat, DomainAlertSeverity.Yellow, 0.75,
                NoDetails.Instance),
        ]);
        var updatedAt = TestTimeConstants.BudgetEpoch;

        var evt = EnrichmentEventMappings.ToEnrichmentEvent(new PipelineEvent.AlertComputed(alertResult, updatedAt));

        Assert.NotNull(evt);
        Assert.Equal("lucerne", evt.Location);
        Assert.Equal("alerts", evt.TypeName);
        Assert.Equal(updatedAt, evt.UpdatedAt.ToDateTimeOffset());
        Assert.NotNull(evt.Alerts);
        Assert.Single(evt.Alerts.Alerts);
    }

    [Fact]
    public void ToEnrichmentEvent_should_wrap_consensus_result_in_enrichment_event()
    {
        var param = new ParameterDef("temperature_2m", "C", ParameterGroup.Weather, ParameterGranularity.Hourly);
        var horizonConsensus = new DomainHorizonConsensus(
            Median: 20.5, TrimmedMean: 20.3, Spread: 2.1, Iqr: 1.5, Agreement: 0.85,
            Outlier: null, ConfidenceInterval: null,
            AvailableModels: [new WeatherModel("icon_d2"), new WeatherModel("ecmwf_ifs025")]);
        var hourlyParam = new DomainParameterConsensus(param,
            new Dictionary<Horizon, DomainHorizonConsensus> { [Horizon.Hours(3)] = horizonConsensus });

        var dailyParam = new DomainParameterConsensus(
            ParameterRegistry.GetByApiName("temperature_2m_max")!,
            new Dictionary<Horizon, DomainHorizonConsensus> { [Horizon.Days(0)] = horizonConsensus });

        var result = new ConsensusResult([hourlyParam], [dailyParam]);
        var updatedAt = TestTimeConstants.BudgetEpoch;

        var consensus = new LocationConsensus(new LocationId("lucerne"),
            new HourlyConsensus([hourlyParam], 72), new DailyConsensus([dailyParam], 4), updatedAt);
        var evt = EnrichmentEventMappings.ToEnrichmentEvent(new PipelineEvent.ConsensusComputed(consensus, updatedAt));

        Assert.NotNull(evt);
        Assert.Equal("lucerne", evt.Location);
        Assert.Equal("consensus", evt.TypeName);
        Assert.NotNull(evt.Consensus);
        Assert.Single(evt.Consensus.HourlyParameters);
        Assert.Single(evt.Consensus.DailyParameters);
    }

    [Fact]
    public void ToEnrichmentEvent_should_return_null_for_non_analysis_event()
    {
        var evt = EnrichmentEventMappings.ToEnrichmentEvent(
            new PipelineEvent.ForecastReceived(new LocationId("lucerne"), new WeatherModel("icon_d2"),
                new Forecast(new WeatherModel("icon_d2"), "lucerne", default,
                    new ForecastSeries([]), DailyForecastSeries.Empty)));

        Assert.Null(evt);
    }

    [Fact]
    public void LatestAnalysisResult_should_fill_every_available_enrichment_field()
    {
        var lucerne = new LocationId("lucerne");
        var computedAt = TestTimeConstants.BudgetEpoch;
        var result = new LatestAnalysisResult(lucerne,
            Consensus: EnrichmentEvents.Consensus(lucerne, computedAt),
            Alerts: EnrichmentEvents.Alerts(lucerne, computedAt.AddMinutes(1)),
            Derived: EnrichmentEvents.Derived(lucerne, computedAt.AddMinutes(2)),
            Trends: EnrichmentEvents.Trends(lucerne, computedAt.AddMinutes(3)),
            Indices: EnrichmentEvents.Indices(lucerne, computedAt.AddMinutes(4)));

        var proto = EnrichmentEventMappings.ToProto((LatestAnalysisResponse)result);

        Assert.Multiple(
            () => Assert.Equal("lucerne", proto.Location),
            () => Assert.Equal(new V2.ConsensusUpdate(), proto.Consensus),
            () => Assert.Equal(computedAt, proto.ConsensusUpdatedAt?.ToDateTimeOffset()),
            () => Assert.Equal(new V2.AlertUpdate(), proto.Alerts),
            () => Assert.Equal(4.5, proto.Derived?.Scalars.DiurnalAmplitude),
            () => Assert.Equal(3, proto.Trends?.PrecipStartsInHours),
            () => Assert.Equal(14, proto.Indices?.Frost.HoursUntilFrost));
    }

    [Fact]
    public void LatestAnalysisResult_should_leave_missing_enrichment_fields_unset()
    {
        var lucerne = new LocationId("lucerne");
        var computedAt = TestTimeConstants.BudgetEpoch;
        var result = new LatestAnalysisResult(lucerne,
            Consensus: null,
            Alerts: EnrichmentEvents.Alerts(lucerne, computedAt),
            Derived: null,
            Trends: EnrichmentEvents.Trends(lucerne, computedAt),
            Indices: null);

        var proto = EnrichmentEventMappings.ToProto((LatestAnalysisResponse)result);

        Assert.Multiple(
            () => Assert.NotNull(proto.Alerts),
            () => Assert.Equal(3, proto.Trends?.PrecipStartsInHours),
            () => Assert.Null(proto.Consensus),
            () => Assert.Null(proto.ConsensusUpdatedAt),
            () => Assert.Null(proto.Derived),
            () => Assert.Null(proto.Indices));
    }
}
