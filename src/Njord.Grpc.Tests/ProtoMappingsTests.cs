using Google.Protobuf.WellKnownTypes;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Budget;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;

namespace Njord.Grpc.Tests;

public sealed class ProtoMappingsTests
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void Alert_results_map_key_severity_and_optional_values()
    {
        var result = new HeatAlertResult(DomainData.Lucerne, DomainAlertSeverity.Orange, 0.85, 33.0, 30.0, -4.0, null, null,
            Messages.Alerts.ConfidenceLevel.High, 34.0);

        var evt = result.ToEnrichmentEvent(Anchor);

        Assert.NotNull(evt);
        var alert = evt.Heat.Alert;
        Assert.Multiple(
            () => Assert.Equal(EnrichmentEvent.FeatureOneofCase.Heat, evt.FeatureCase),
            () => Assert.Equal(V2.AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(-4.0, alert.PeakValue),
            () => Assert.False(alert.HasHoursUntil),
            () => Assert.Equal(34.0, evt.Heat.ExpectedMax));
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
        var evt = EnrichmentEvents.Alerts(DomainData.Lucerne, Anchor).ToEnrichmentEvent(Anchor);

        Assert.NotNull(evt);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", evt.Location),
            () => Assert.Equal(Anchor.ToTimestamp(), evt.UpdatedAt),
            () => Assert.Equal(EnrichmentEvent.FeatureOneofCase.Heat, evt.FeatureCase));
    }

    [Fact]
    public void A_lifestyle_event_maps_to_a_lifestyle_enrichment_event_stamped_with_the_given_time()
    {
        var evt = new LaundryScoreResult(DomainData.Lucerne, 80, Anchor, 4).ToEnrichmentEvent(Anchor.AddHours(1));

        Assert.NotNull(evt);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", evt.Location),
            () => Assert.Equal(Anchor.AddHours(1).ToTimestamp(), evt.UpdatedAt),
            () => Assert.Equal(EnrichmentEvent.FeatureOneofCase.Laundry, evt.FeatureCase),
            () => Assert.Equal(80, evt.Laundry.Score.Score),
            () => Assert.Equal(4, evt.Laundry.Score.BestHours));
    }

    [Fact]
    public void Enrichment_locations_cover_alerts_and_lifestyle_but_not_domain_data()
    {
        var weather = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 1));

        Assert.Multiple(
            () => Assert.Equal(DomainData.Lucerne, EnrichmentEvents.Alerts(DomainData.Lucerne, Anchor).ToEnrichmentLocation()),
            () => Assert.Equal(DomainData.Bern, new CommuteSafetyResult(DomainData.Bern, Messages.Pipeline.CommuteLevel.Safe, []).ToEnrichmentLocation()),
            () => Assert.Null(weather.ToEnrichmentLocation()));
    }

    [Fact]
    public void Ensemble_events_map_to_a_domain_data_update_with_spread_fields()
    {
        var points = new[] { new EnsemblePoint(Anchor, 1.5, 0.3, 2.1) };
        var forecast = new DomainForecast<EnsemblePoint>(DomainKind.Ensemble, Anchor, TimeResolution.Hourly, [.. points]);
        var evt = new PipelineEvent.EnsembleDataReceived(DomainData.Lucerne, forecast);

        var update = evt.ToDomainDataUpdate();

        Assert.NotNull(update);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", update.Location),
            () => Assert.Equal(Domain.Ensemble, update.Domain),
            () => Assert.Equal(DomainDataUpdate.DataOneofCase.Ensemble, update.DataCase),
            () => Assert.Single(update.Ensemble.Points),
            () => Assert.Equal(1.5, update.Ensemble.Points[0].TemperatureSpread),
            () => Assert.Equal(0.3, update.Ensemble.Points[0].PrecipitationSpread),
            () => Assert.Equal(2.1, update.Ensemble.Points[0].WindSpeedSpread));
    }

    [Fact]
    public void Events_without_an_enrichment_have_no_enrichment_event()
    {
        var weather = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 1));

        Assert.Null(weather.ToEnrichmentEvent(Anchor));
    }

    [Theory]
    [InlineData(DomainKind.Weather, Domain.Weather)]
    [InlineData(DomainKind.AirQuality, Domain.AirQuality)]
    [InlineData(DomainKind.Flood, Domain.Flood)]
    [InlineData(DomainKind.Marine, Domain.Marine)]
    [InlineData(DomainKind.Satellite, Domain.Satellite)]
    [InlineData(DomainKind.Ensemble, Domain.Ensemble)]
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
