using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerStateSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    private static PipelineEvent.AlertRaised Raise(AlertType type, AlertSeverity severity) =>
        new(DomainData.Lucerne, new Alert(type, severity, 0.5, NoDetails.Instance), Anchor);

    [Fact]
    public void A_domain_event_replaces_the_previous_one_of_the_same_location_and_domain()
    {
        var older = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor, 2));
        var newer = new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(Anchor.AddHours(1), 2));

        var state = GrpcConsumerState.Empty.Apply(older).Apply(newer);

        Assert.Multiple(
            () => Assert.Equal(newer, state.GetDomainData(DomainData.Lucerne, DomainKind.Weather)),
            () => Assert.Null(state.GetDomainData(DomainData.Lucerne, DomainKind.Flood)),
            () => Assert.Empty(state.Alerts));
    }

    [Fact]
    public void An_alert_of_the_same_type_replaces_the_earlier_one_and_alerts_are_ordered_by_type()
    {
        var heat = Raise(AlertType.Heat, AlertSeverity.Orange);
        var frost = Raise(AlertType.Frost, AlertSeverity.Yellow);
        var frostRed = Raise(AlertType.Frost, AlertSeverity.Red);

        var state = GrpcConsumerState.Empty.Apply(heat).Apply(frost).Apply(frostRed);

        var result = state.GetLatestAlerts(DomainData.Lucerne);
        Assert.NotNull(result);
        Assert.Equal([frostRed.Alert, heat.Alert], result.Alerts);
    }

    [Theory]
    [InlineData(AlertType.Pm25)]
    [InlineData(AlertType.Pollen)]
    [InlineData(AlertType.FloodRisk)]
    [InlineData(AlertType.FloodRain)]
    [InlineData(AlertType.Frost)]
    public void Alerts_of_every_domain_are_cached(AlertType type)
    {
        var raised = Raise(type, AlertSeverity.Yellow);

        var state = GrpcConsumerState.Empty.Apply(raised);

        var result = state.GetLatestAlerts(DomainData.Lucerne);
        Assert.NotNull(result);
        Assert.Equal([raised.Alert], result.Alerts);
    }

    [Fact]
    public void Alerts_are_kept_per_location()
    {
        var lucerne = Raise(AlertType.Frost, AlertSeverity.Yellow);
        var bern = lucerne with { Location = DomainData.Bern };

        var state = GrpcConsumerState.Empty.Apply(lucerne).Apply(bern);

        Assert.Multiple(
            () => Assert.Equal(2, state.Alerts.Count),
            () => Assert.Null(state.GetLatestAlerts(new Location("Nowhere", 0, 0))));
    }
}
