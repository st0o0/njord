using Njord.Core;
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
    private static readonly DateTimeOffset _anchor = TestTimeConstants.BudgetEpoch;
    private static readonly Location _lucerne = DomainData.Lucerne;

    [Fact]
    public void Weather_data_is_kept_separately_per_model()
    {
        var primary = new ForecastDataReceived(_lucerne, DomainData.Forecast(_anchor, 2));
        var secondary = new ForecastDataReceived(_lucerne, DomainData.Forecast(_anchor, 3), "ukmo_global_ensemble_mean_20km");

        var state = GrpcConsumerState.Empty.Apply(primary).Apply(secondary);

        Assert.Multiple(
            () => Assert.Equal(primary, state.GetDomainData(_lucerne, DomainKind.Forecast)),
            () => Assert.Equal(secondary, state.GetDomainData(_lucerne, DomainKind.Forecast, "ukmo_global_ensemble_mean_20km")));
    }

    [Fact]
    public void Domain_data_is_kept_as_the_latest_event_per_location_and_domain()
    {
        var older = new ForecastDataReceived(_lucerne, DomainData.Forecast(_anchor, 2));
        var newer = new ForecastDataReceived(_lucerne, DomainData.Forecast(_anchor.AddHours(1), 2));
        var flood = new FloodDataReceived(_lucerne, DomainData.Flood(_anchor));

        var state = GrpcConsumerState.Empty.Apply(older).Apply(flood).Apply(newer);

        Assert.Multiple(
            () => Assert.Equal(newer, state.GetDomainData(_lucerne, DomainKind.Forecast)),
            () => Assert.Equal(flood, state.GetDomainData(_lucerne, DomainKind.Flood)));
    }

    [Fact]
    public void Seasonal_data_is_kept_as_the_latest_event()
    {
        var older = new SeasonalDataReceived(_lucerne, Seasonal(_anchor));
        var newer = new SeasonalDataReceived(_lucerne, Seasonal(_anchor.AddDays(1)));

        var state = GrpcConsumerState.Empty.Apply(older).Apply(newer);

        Assert.Equal(newer, state.GetDomainData(_lucerne, DomainKind.Seasonal));
    }

    private static DomainForecast<SeasonalPoint> Seasonal(DateTimeOffset run) =>
        new(DomainKind.Seasonal, run, TimeResolution.Monthly, [new SeasonalPoint(run, 1.0)]);

    [Fact]
    public void Domain_data_of_another_location_or_domain_is_not_available()
    {
        var state = GrpcConsumerState.Empty.Apply(
            new ForecastDataReceived(_lucerne, DomainData.Forecast(_anchor, 2)));

        Assert.Multiple(
            () => Assert.Null(state.GetDomainData(DomainData.Bern, DomainKind.Forecast)),
            () => Assert.Null(state.GetDomainData(_lucerne, DomainKind.Marine)));
    }

    [Fact]
    public void A_result_replaces_the_result_of_the_same_feature()
    {
        var first = Heat(AlertSeverity.Yellow, 0.5, _anchor);
        var second = Heat(AlertSeverity.Red, 0.9, _anchor.AddHours(1));

        var state = GrpcConsumerState.Empty.Apply(first).Apply(second);

        Assert.Equal<FeatureResult>([second], state.GetLatestResults(_lucerne));
    }

    [Fact]
    public void Results_of_different_features_accumulate_ordered_by_wire_key()
    {
        var frost = new FrostAlertResult(_lucerne, AlertSeverity.Orange, 0.8, -3.0, -2.0, null, null, null, ConfidenceLevel.High, -3.0, null);
        var heat = Heat(AlertSeverity.Yellow, 0.7, _anchor);
        var laundry = new LaundryScoreResult(_lucerne, 85, _anchor, 5);

        var state = GrpcConsumerState.Empty.Apply(laundry).Apply(heat).Apply(frost);

        Assert.Equal<FeatureResult>([frost, heat, laundry], state.GetLatestResults(_lucerne));
    }

    [Fact]
    public void Results_are_kept_per_location_and_absent_before_the_first_result()
    {
        var state = GrpcConsumerState.Empty.Apply(Heat(AlertSeverity.Yellow, 0.7, _anchor));

        Assert.Multiple(
            () => Assert.Single(state.GetLatestResults(_lucerne)),
            () => Assert.Empty(state.GetLatestResults(DomainData.Bern)));
    }

    [Fact]
    public void Condition_results_are_kept_like_any_other_result()
    {
        var commute = new CommuteSafetyResult(_lucerne, CommuteLevel.Caution, [CommuteHazard.Ice]);

        var state = GrpcConsumerState.Empty.Apply(commute);

        Assert.Equal<FeatureResult>([commute], state.GetLatestResults(_lucerne));
    }

    private static HeatAlertResult Heat(AlertSeverity severity, double confidence, DateTimeOffset at) =>
        new(_lucerne, severity, confidence, 32.0, 30.0, null, null, null, ConfidenceLevel.Medium, 33.0);
}
