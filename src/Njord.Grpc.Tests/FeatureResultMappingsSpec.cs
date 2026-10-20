using System.Collections.Immutable;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class FeatureResultMappingsSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void A_score_flattens_to_score_best_start_and_best_hours()
    {
        var update = new ScoreResult(DomainData.Lucerne, "laundry", 80, Anchor, 4).ToProto();

        Assert.Multiple(
            () => Assert.Equal("laundry", update.Type),
            () => Assert.Equal(V2.OutputKind.Score, update.Output),
            () => Assert.Equal(
                new Dictionary<string, string> { ["score"] = "80", ["best_start"] = FeatureData.Time(Anchor), ["best_hours"] = "4" },
                update.Data));
    }

    [Fact]
    public void A_condition_flattens_to_its_data_plus_the_state()
    {
        var update = new ConditionResult(DomainData.Lucerne, "commute", "caution", FeatureData.Of(("risks", "ice"))).ToProto();

        Assert.Multiple(
            () => Assert.Equal("commute", update.Type),
            () => Assert.Equal(V2.OutputKind.Condition, update.Output),
            () => Assert.Equal(new Dictionary<string, string> { ["risks"] = "ice", ["state"] = "caution" }, update.Data));
    }

    [Fact]
    public void A_trend_flattens_to_its_data_plus_direction_and_magnitude()
    {
        var update = new TrendResult(DomainData.Lucerne, "confidencetrend", "improving", 0.25, FeatureData.Of(("current_avg_std_dev", "1.5"))).ToProto();

        Assert.Multiple(
            () => Assert.Equal("confidencetrend", update.Type),
            () => Assert.Equal(V2.OutputKind.Trend, update.Output),
            () => Assert.Equal(
                new Dictionary<string, string> { ["current_avg_std_dev"] = "1.5", ["direction"] = "improving", ["magnitude"] = "0.25" },
                update.Data));
    }

    [Fact]
    public void A_forecast_flattens_each_entry_to_a_numbered_label_and_prefixed_values()
    {
        ImmutableArray<ForecastEntry> entries =
        [
            new("2026-08", FeatureData.Of(("temp_anomaly", "1.2"))),
            new("2026-09", FeatureData.Of(("temp_anomaly", "-0.4"))),
        ];

        var update = new ForecastResult(DomainData.Lucerne, "seasonaloutlook", entries).ToProto();

        Assert.Multiple(
            () => Assert.Equal("seasonaloutlook", update.Type),
            () => Assert.Equal(V2.OutputKind.Forecast, update.Output),
            () => Assert.Equal(
                new Dictionary<string, string>
                {
                    ["entry_1"] = "2026-08",
                    ["entry_1_temp_anomaly"] = "1.2",
                    ["entry_2"] = "2026-09",
                    ["entry_2_temp_anomaly"] = "-0.4",
                },
                update.Data));
    }

    [Fact]
    public void An_alert_has_no_field_mapping_because_it_has_its_own_message()
    {
        var alert = new AlertResult(DomainData.Lucerne, "heat", new Alert(AlertSeverity.Yellow, 0.7, NoDetails.Instance), Anchor);

        Assert.Throws<ArgumentOutOfRangeException>(() => alert.ToFields());
    }

    [Theory]
    [InlineData(OutputKind.Alert, V2.OutputKind.Alert)]
    [InlineData(OutputKind.Score, V2.OutputKind.Score)]
    [InlineData(OutputKind.Condition, V2.OutputKind.Condition)]
    [InlineData(OutputKind.Trend, V2.OutputKind.Trend)]
    [InlineData(OutputKind.Forecast, V2.OutputKind.Forecast)]
    public void Output_kinds_map_to_their_proto_output_kind(OutputKind kind, V2.OutputKind expected) =>
        Assert.Equal(expected, kind.ToProto());

    [Fact]
    public void An_unknown_output_kind_is_rejected()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((OutputKind)99).ToProto());

        Assert.Equal("output", ex.ParamName);
    }
}
