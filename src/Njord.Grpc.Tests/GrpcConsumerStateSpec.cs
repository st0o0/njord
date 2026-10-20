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
            () => Assert.Null(state.GetDomainData(Lucerne, DomainKind.Marine)));
    }

    [Fact]
    public void A_result_replaces_the_result_of_the_same_feature()
    {
        var first = Heat(AlertSeverity.Yellow, 0.5, Anchor);
        var second = Heat(AlertSeverity.Red, 0.9, Anchor.AddHours(1));

        var state = GrpcConsumerState.Empty.Apply(first).Apply(second);

        Assert.Equal<FeatureResult>([second], state.GetLatestResults(Lucerne));
    }

    [Fact]
    public void Results_of_different_features_accumulate_ordered_by_wire_key()
    {
        var frost = new AlertResult(Lucerne, "frost", new Alert(AlertSeverity.Orange, 0.8, NoDetails.Instance), Anchor);
        var heat = Heat(AlertSeverity.Yellow, 0.7, Anchor);
        var laundry = new ScoreResult(Lucerne, "laundry", 85, Anchor, 5);

        var state = GrpcConsumerState.Empty.Apply(laundry).Apply(heat).Apply(frost);

        Assert.Equal<FeatureResult>([frost, heat, laundry], state.GetLatestResults(Lucerne));
    }

    [Fact]
    public void Results_are_kept_per_location_and_absent_before_the_first_result()
    {
        var state = GrpcConsumerState.Empty.Apply(Heat(AlertSeverity.Yellow, 0.7, Anchor));

        Assert.Multiple(
            () => Assert.Single(state.GetLatestResults(Lucerne)),
            () => Assert.Empty(state.GetLatestResults(DomainData.Bern)));
    }

    [Fact]
    public void Condition_results_are_kept_like_any_other_result()
    {
        var commute = new ConditionResult(Lucerne, "commute", "caution", FeatureData.Of(("risks", "ice")));

        var state = GrpcConsumerState.Empty.Apply(commute);

        Assert.Equal<FeatureResult>([commute], state.GetLatestResults(Lucerne));
    }

    private static AlertResult Heat(AlertSeverity severity, double confidence, DateTimeOffset at) =>
        new(Lucerne, "heat", new Alert(severity, confidence, NoDetails.Instance), at);
}
