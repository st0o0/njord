using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Seasonal;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Results.Scores;
using Njord.Messages.Stream;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerStateTests
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private static readonly Location Lucerne = DomainData.Lucerne;

    [Fact]
    public void Weather_data_is_kept_separately_per_model()
    {
        var primary = new ForecastDataReceived(Lucerne, DomainData.Forecast(Anchor, 2));
        var secondary = new ForecastDataReceived(Lucerne, DomainData.Forecast(Anchor, 3), "ukmo_global_ensemble_mean_20km");

        var state = GrpcConsumerState.Empty.Apply(primary).Apply(secondary);

        Assert.Multiple(
            () => Assert.Equal(primary, state.GetDomainData(Lucerne, DomainKind.Forecast)),
            () => Assert.Equal(secondary, state.GetDomainData(Lucerne, DomainKind.Forecast, "ukmo_global_ensemble_mean_20km")));
    }

    [Fact]
    public void Domain_data_is_kept_as_the_latest_event_per_location_and_domain()
    {
        var older = new ForecastDataReceived(Lucerne, DomainData.Forecast(Anchor, 2));
        var newer = new ForecastDataReceived(Lucerne, DomainData.Forecast(Anchor.AddHours(1), 2));
        var flood = new FloodDataReceived(Lucerne, DomainData.Flood(Anchor));

        var state = GrpcConsumerState.Empty.Apply(older).Apply(flood).Apply(newer);

        Assert.Multiple(
            () => Assert.Equal(newer, state.GetDomainData(Lucerne, DomainKind.Forecast)),
            () => Assert.Equal(flood, state.GetDomainData(Lucerne, DomainKind.Flood)));
    }

    [Fact]
    public void Seasonal_data_is_kept_as_the_latest_event()
    {
        var older = new SeasonalDataReceived(Lucerne, Seasonal(Anchor));
        var newer = new SeasonalDataReceived(Lucerne, Seasonal(Anchor.AddDays(1)));

        var state = GrpcConsumerState.Empty.Apply(older).Apply(newer);

        Assert.Equal(newer, state.GetDomainData(Lucerne, DomainKind.Seasonal));
    }

    private static DomainForecast<SeasonalPoint> Seasonal(DateTimeOffset run) =>
        new(DomainKind.Seasonal, run, TimeResolution.Monthly, [new SeasonalPoint(run, 1.0)]);

    [Fact]
    public void Domain_data_of_another_location_or_domain_is_not_available()
    {
        var state = GrpcConsumerState.Empty.Apply(
            new ForecastDataReceived(Lucerne, DomainData.Forecast(Anchor, 2)));

        Assert.Multiple(
            () => Assert.Null(state.GetDomainData(DomainData.Bern, DomainKind.Forecast)),
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
        var frost = new FrostAlertResult(Lucerne, AlertSeverity.Orange, 0.8, -3.0, -2.0, null, null, null, ConfidenceLevel.High, -3.0, null);
        var heat = Heat(AlertSeverity.Yellow, 0.7, Anchor);
        var laundry = new LaundryScoreResult(Lucerne, 85, Anchor, 5);

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
        var commute = new CommuteSafetyResult(Lucerne, CommuteLevel.Caution, [CommuteHazard.Ice]);

        var state = GrpcConsumerState.Empty.Apply(commute);

        Assert.Equal<FeatureResult>([commute], state.GetLatestResults(Lucerne));
    }

    private static HeatAlertResult Heat(AlertSeverity severity, double confidence, DateTimeOffset at) =>
        new(Lucerne, severity, confidence, 32.0, 30.0, null, null, null, ConfidenceLevel.Medium, 33.0);
}
