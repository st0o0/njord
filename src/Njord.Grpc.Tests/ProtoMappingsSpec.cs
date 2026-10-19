using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Budget;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;
using DomainAlert = Njord.Messages.Alerts.Alert;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;

namespace Njord.Grpc.Tests;

public sealed class ProtoMappingsSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void Alert_results_map_type_severity_and_optional_values()
    {
        var result = new AlertResult(DomainData.Lucerne,
            [new DomainAlert(DomainAlertType.Frost, DomainAlertSeverity.Orange, 0.85, NoDetails.Instance, PeakValue: -4.0)]);

        var alert = Assert.Single(result.ToProto().Alerts);

        Assert.Multiple(
            () => Assert.Equal(V2.AlertType.Frost, alert.Type),
            () => Assert.Equal(V2.AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(-4.0, alert.PeakValue),
            () => Assert.False(alert.HasHoursUntil));
    }

    [Fact]
    public void Catalog_locations_map_name_coordinates_and_models()
    {
        var info = new CatalogLocation(new Location("lucerne", 47.05, 8.31), ["icon_d2", "ecmwf_ifs025"]).ToProto();

        Assert.Multiple(
            () => Assert.Equal("lucerne", info.Name),
            () => Assert.Equal(47.05, info.Latitude),
            () => Assert.Equal(8.31, info.Longitude),
            () => Assert.Equal(["icon_d2", "ecmwf_ifs025"], info.Models));
    }

    [Fact]
    public void Weather_events_map_to_a_domain_data_update_with_the_model_run()
    {
        var evt = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));

        var update = evt.ToDomainDataUpdate();

        Assert.NotNull(update);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", update.Location),
            () => Assert.Equal(Domain.Weather, update.Domain),
            () => Assert.Equal(Anchor, update.ModelRun.ToDateTimeOffset()),
            () => Assert.Equal(DomainDataUpdate.DataOneofCase.Weather, update.DataCase),
            () => Assert.Equal(2, update.Weather.Points.Count));
    }

    [Fact]
    public void Poll_state_entries_map_to_model_status_with_optional_values()
    {
        var entry = new PollStateEntry(DomainData.Lucerne, DomainPollPhase.Steady, Anchor.AddHours(1), null, 2, 3600);

        var status = entry.ToModelStatus();

        Assert.Multiple(
            () => Assert.Equal("Lucerne", status.Location),
            () => Assert.Equal(V2.PollPhase.Steady, status.Phase),
            () => Assert.Equal(Anchor.AddHours(1).ToTimestamp(), status.NextPoll),
            () => Assert.Equal(2, status.MissCount),
            () => Assert.Null(status.LastChange),
            () => Assert.Equal(3600, status.CycleSeconds));
    }

    [Fact]
    public void Budget_status_maps_limits_usage_and_monthly_percent()
    {
        var status = new BudgetStatusResult(75, 5, 300, 100).ToProto();

        Assert.Multiple(
            () => Assert.Equal(75, status.MonthlyUsed),
            () => Assert.Equal(300, status.MonthlyLimit),
            () => Assert.Equal(5, status.DailyUsed),
            () => Assert.Equal(25.0, status.UsagePercent));
    }

    [Fact]
    public void A_raised_alert_maps_to_an_alerts_enrichment_event()
    {
        var evt = EnrichmentEvents.Alerts(DomainData.Lucerne, Anchor).ToEnrichmentEvent();

        Assert.NotNull(evt);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", evt.Location),
            () => Assert.Equal(Anchor.ToTimestamp(), evt.UpdatedAt),
            () => Assert.Equal(V2.AlertType.Heat, Assert.Single(evt.Alerts.Alerts).Type));
    }

    [Fact]
    public void Events_without_an_enrichment_have_no_enrichment_event()
    {
        var weather = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 1));

        Assert.Null(weather.ToEnrichmentEvent());
    }

    [Theory]
    [InlineData(DomainKind.Weather, Domain.Weather)]
    [InlineData(DomainKind.AirQuality, Domain.AirQuality)]
    [InlineData(DomainKind.Flood, Domain.Flood)]
    [InlineData(DomainKind.Solar, Domain.Solar)]
    [InlineData(DomainKind.Marine, Domain.Marine)]
    public void Domain_kinds_map_to_their_proto_domain_and_back(DomainKind kind, Domain expected)
    {
        Assert.Multiple(
            () => Assert.Equal(expected, kind.ToProto()),
            () => Assert.Equal(kind, expected.ToDomain()));
    }

    [Theory]
    [InlineData(DomainPollPhase.Discovery, V2.PollPhase.Discovery)]
    [InlineData(DomainPollPhase.Steady, V2.PollPhase.Steady)]
    public void Poll_phases_map_to_their_proto_phase(DomainPollPhase phase, V2.PollPhase expected) =>
        Assert.Equal(expected, phase.ToProto());
}
