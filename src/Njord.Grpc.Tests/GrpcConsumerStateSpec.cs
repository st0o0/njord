using Njord.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerStateSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private static readonly Location Lucerne = DomainData.Lucerne;

    [Fact]
    public void Domain_data_is_kept_as_the_latest_event_per_location_and_domain()
    {
        var older = new PipelineEvent.WeatherDataReceived(Lucerne, DomainData.Weather(Anchor, 2));
        var newer = new PipelineEvent.WeatherDataReceived(Lucerne, DomainData.Weather(Anchor.AddHours(1), 2));
        var flood = new PipelineEvent.FloodDataReceived(Lucerne, DomainData.Flood(Anchor));

        var state = GrpcConsumerState.Empty.Apply(older).Apply(flood).Apply(newer);

        Assert.Multiple(
            () => Assert.Equal(newer, state.GetDomainData(Lucerne, DomainKind.Weather)),
            () => Assert.Equal(flood, state.GetDomainData(Lucerne, DomainKind.Flood)));
    }

    [Fact]
    public void Domain_data_of_another_location_or_domain_is_not_available()
    {
        var state = GrpcConsumerState.Empty.Apply(
            new PipelineEvent.WeatherDataReceived(Lucerne, DomainData.Weather(Anchor, 2)));

        Assert.Multiple(
            () => Assert.Null(state.GetDomainData(DomainData.Bern, DomainKind.Weather)),
            () => Assert.Null(state.GetDomainData(Lucerne, DomainKind.Solar)));
    }

    [Fact]
    public void A_raised_alert_replaces_the_alert_of_the_same_type()
    {
        var first = new PipelineEvent.AlertRaised(Lucerne,
            new Alert(AlertType.Heat, AlertSeverity.Yellow, 0.5, NoDetails.Instance), Anchor);
        var second = new PipelineEvent.AlertRaised(Lucerne,
            new Alert(AlertType.Heat, AlertSeverity.Red, 0.9, NoDetails.Instance), Anchor.AddHours(1));

        var state = GrpcConsumerState.Empty.Apply(first).Apply(second);

        var alerts = state.GetLatestAlerts(Lucerne);
        Assert.NotNull(alerts);
        Assert.Equal([second.Alert], alerts.Alerts);
    }

    [Fact]
    public void Alerts_of_different_types_accumulate_ordered_by_type()
    {
        var frost = new Alert(AlertType.Frost, AlertSeverity.Orange, 0.8, NoDetails.Instance);
        var heat = new Alert(AlertType.Heat, AlertSeverity.Yellow, 0.7, NoDetails.Instance);

        var state = GrpcConsumerState.Empty
            .Apply(new PipelineEvent.AlertRaised(Lucerne, heat, Anchor))
            .Apply(new PipelineEvent.AlertRaised(Lucerne, frost, Anchor));

        var alerts = state.GetLatestAlerts(Lucerne);
        Assert.NotNull(alerts);
        Assert.Equal(new[] { heat, frost }.OrderBy(a => a.Type), alerts.Alerts);
    }

    [Fact]
    public void Alerts_are_kept_per_location_and_absent_before_the_first_alert()
    {
        var state = GrpcConsumerState.Empty.Apply(EnrichmentEvents.Alerts(Lucerne, Anchor));

        Assert.Multiple(
            () => Assert.NotNull(state.GetLatestAlerts(Lucerne)),
            () => Assert.Null(state.GetLatestAlerts(DomainData.Bern)));
    }

    [Fact]
    public void Lifestyle_events_keep_the_latest_per_location_and_kind_ordered_by_kind()
    {
        var older = new PipelineEvent.LaundryScoreComputed(Lucerne, 40, Anchor, 2);
        var newer = new PipelineEvent.LaundryScoreComputed(Lucerne, 85, Anchor.AddHours(1), 5);
        var commute = new PipelineEvent.CommuteSafety(Lucerne, "caution", ["ice"]);

        var state = GrpcConsumerState.Empty.Apply(older).Apply(commute).Apply(newer);

        var lifestyle = state.GetLatestLifestyle(Lucerne);
        Assert.Equal(2, lifestyle.Count);
        Assert.Multiple(
            () => Assert.Equal(LifestyleKind.LaundryScore, lifestyle[0].Kind),
            () => Assert.Equal("85", lifestyle[0].Data["score"]),
            () => Assert.Equal(LifestyleKind.CommuteSafety, lifestyle[1].Kind),
            () => Assert.Empty(state.GetLatestLifestyle(DomainData.Bern)));
    }
}
