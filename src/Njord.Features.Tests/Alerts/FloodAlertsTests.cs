using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Features.Alerts.FloodRain;
using Njord.Features.Alerts.FloodRisk;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.FloodFixtures;

namespace Njord.Features.Tests.Alerts;

public sealed class FloodAlertsTests
{
    private static FloodRiskAlert Risk() => new(Options.Create(new FloodRiskConfig { Enabled = true }));

    private static FloodRainAlert RainAlert() => new(Options.Create(new FloodRainConfig { Enabled = true }));

    private static LocationForecast Risky(params FloodPoint[] points) =>
        new(Lucerne, Rain(0.0), Flood: FloodForecast(points));

    private static PipelineEvent? RunRain(double hourlyRain, params FloodPoint[] points) =>
        Run(RainAlert(), Now, new LocationForecast(Lucerne, Rain(hourlyRain), Flood: FloodForecast(points)));

    private static FloodPoint WithMembers(int exceeding, int total = 50) =>
        Point(0, 160, median: 100, p75: 150) with
        {
            EnsembleMembers = [.. Enumerable.Range(0, total).Select(i => i < exceeding ? 200.0 : 100.0)],
        };

    [Fact]
    public void Discharge_above_the_p75_percentile_is_orange()
    {
        var alert = Assert.IsType<FloodRiskAlertResult>(Run(Risk(), Now, Risky(Point(0, 160, median: 100, p75: 150))));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(160.0, alert.TriggerValue),
            () => Assert.Equal(150.0, alert.Threshold));
    }

    [Fact]
    public void Discharge_at_the_p75_percentile_raises_nothing()
    {
        Assert.Null(Run(Risk(), Now, Risky(Point(0, 150, median: 100, p75: 150))));
    }

    [Fact]
    public void Discharge_within_twice_the_median_raises_nothing()
    {
        Assert.Null(Run(Risk(), Now, Risky(Point(0, 190, median: 100, p75: 300))));
    }

    [Fact]
    public void Discharge_above_the_historical_maximum_is_red()
    {
        var alert = Assert.IsType<FloodRiskAlertResult>(Run(Risk(), Now, Risky(Point(2, 450, max: 400))));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(400.0, alert.Threshold),
            () => Assert.Equal(36, alert.HoursUntil));
    }

    [Fact]
    public void The_highest_severity_in_the_window_wins()
    {
        var alert = Assert.IsType<FloodRiskAlertResult>(Run(Risk(), Now, Risky(Point(0, 160), Point(3, 500), Point(5, 170))));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(500.0, alert.TriggerValue));
    }

    [Fact]
    public void Days_beyond_the_seven_day_window_are_ignored()
    {
        Assert.Null(Run(Risk(), Now, Risky(Point(10, 900))));
    }

    [Fact]
    public void Most_members_above_the_threshold_give_high_confidence()
    {
        var alert = Assert.IsType<FloodRiskAlertResult>(Run(Risk(), Now, Risky(WithMembers(45))));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(0.9, alert.Confidence),
            () => Assert.Equal(ConfidenceLevel.High, alert.Level));
    }

    [Fact]
    public void A_single_outlier_member_gives_low_confidence()
    {
        var alert = Assert.IsType<FloodRiskAlertResult>(Run(Risk(), Now, Risky(WithMembers(1))));

        Assert.Multiple(
            () => Assert.Equal(0.02, alert.Confidence),
            () => Assert.Equal(ConfidenceLevel.Low, alert.Level));
    }

    [Fact]
    public void Half_of_the_members_above_the_threshold_give_medium_confidence()
    {
        var alert = Assert.IsType<FloodRiskAlertResult>(Run(Risk(), Now, Risky(WithMembers(25))));

        Assert.Multiple(
            () => Assert.Equal(0.5, alert.Confidence),
            () => Assert.Equal(ConfidenceLevel.Medium, alert.Level));
    }

    [Fact]
    public void Without_members_the_alert_keeps_full_confidence()
    {
        var alert = Assert.IsType<FloodRiskAlertResult>(Run(Risk(), Now, Risky(Point(0, 160, median: 100, p75: 150))));

        Assert.Multiple(
            () => Assert.Equal(1.0, alert.Confidence),
            () => Assert.Equal(ConfidenceLevel.High, alert.Level));
    }

    [Fact]
    public void Flood_risk_fires_with_only_flood_data()
    {
        Assert.NotNull(Run(Risk(), Now, new LocationForecast(Lucerne, Flood: FloodForecast(Point(0, 160, median: 100, p75: 150)))));
    }

    [Fact]
    public void Heavy_rain_with_rising_discharge_above_the_median_is_orange()
    {
        var alert = Assert.IsType<FloodRainAlertResult>(RunRain(1.5, Point(0, 110), Point(1, 130)));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(130.0, alert.TriggerValue),
            () => Assert.Equal(100.0, alert.Threshold));
    }

    [Fact]
    public void Heavy_rain_with_rising_discharge_above_p75_is_red()
    {
        var alert = Assert.IsType<FloodRainAlertResult>(RunRain(1.5, Point(0, 110), Point(1, 170)));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(150.0, alert.Threshold));
    }

    [Fact]
    public void Rain_exactly_at_30_mm_in_24_hours_counts_as_heavy()
    {
        var alert = Assert.IsType<FloodRainAlertResult>(RunRain(1.25, Point(0, 110), Point(1, 130)));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Rain_below_30_mm_in_24_hours_raises_nothing()
    {
        Assert.Null(RunRain(1.2, Point(0, 110), Point(1, 170)));
    }

    [Fact]
    public void Falling_discharge_raises_nothing()
    {
        Assert.Null(RunRain(1.5, Point(0, 170), Point(1, 120)));
    }

    [Fact]
    public void Discharge_below_the_median_raises_nothing()
    {
        Assert.Null(RunRain(1.5, Point(0, 50), Point(1, 90)));
    }

    [Fact]
    public void The_flood_features_request_their_discharge_parameters()
    {
        Assert.Multiple(
            () => Assert.Equal(
                new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeMedian | FloodParams.DischargeP75
                    | FloodParams.DischargeMax),
                Risk().Descriptor.Requirements),
            () => Assert.False(Risk().Descriptor.DefaultEnabled),
            () => Assert.False(RainAlert().Descriptor.DefaultEnabled));
    }
}
