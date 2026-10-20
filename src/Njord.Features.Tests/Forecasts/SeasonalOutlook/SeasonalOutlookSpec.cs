using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Forecasts.SeasonalOutlook;
using Njord.Forecast;
using Njord.Forecast.Seasonal;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Forecasts;

public sealed class SeasonalOutlookSpec
{
    private static readonly DateTimeOffset October = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static SeasonalOutlook Feature() => new(Options.Create(new SeasonalOutlookConfig { Enabled = true }));

    private static SeasonalPoint Month(int year, int month, double temp = 1.0, double? precip = null, double? mean = null) =>
        new(new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero), temp, precip, mean);

    private static LocationForecast With(params SeasonalPoint[] points) =>
        WeatherOnly(Day()) with
        {
            Seasonal = new DomainForecast<SeasonalPoint>(DomainKind.Seasonal, October, TimeResolution.Monthly, [.. points]),
        };

    [Fact]
    public void The_months_after_the_current_one_are_emitted_with_rounded_anomalies()
    {
        var outlook = Assert.IsType<SeasonalOutlookResult>(Assert.Single(Run(
            Feature(), October, With(Month(2026, 10), Month(2026, 11, 1.74, 2.4, 99.5), Month(2026, 12, -0.5, -20, 80)))));

        Assert.Multiple(
            () => Assert.Equal(["2026-11", "2026-12"], outlook.Entries.Select(e => e.Label)),
            () => Assert.Equal(
                new Dictionary<string, string> { ["temp_anomaly"] = "1.7", ["precip_anomaly"] = "2.5", ["confidence"] = "high" },
                outlook.Entries[0].Values),
            () => Assert.Equal(
                new Dictionary<string, string> { ["temp_anomaly"] = "-0.5", ["precip_anomaly"] = "-20", ["confidence"] = "medium" },
                outlook.Entries[1].Values));
    }

    [Theory]
    [InlineData(1, "high")]
    [InlineData(2, "medium")]
    [InlineData(3, "low")]
    public void The_confidence_drops_with_the_lead_time(int lead, string expected)
    {
        var month = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(lead);

        var outlook = Assert.IsType<SeasonalOutlookResult>(
            Assert.Single(Run(Feature(), October, With(Month(month.Year, month.Month)))));

        Assert.Equal(expected, Assert.Single(outlook.Entries).Values["confidence"]);
    }

    [Fact]
    public void A_missing_precipitation_series_reports_a_zero_anomaly()
    {
        var outlook = Assert.IsType<SeasonalOutlookResult>(Assert.Single(Run(Feature(), October, With(Month(2026, 11)))));

        Assert.Equal("0", Assert.Single(outlook.Entries).Values["precip_anomaly"]);
    }

    [Fact]
    public void Nothing_is_reported_without_seasonal_data()
    {
        Assert.Empty(Run(Feature(), October, WeatherOnly(Day())));
    }

    [Fact]
    public void Nothing_is_reported_when_only_the_current_month_is_known()
    {
        Assert.Empty(Run(Feature(), October, With(Month(2026, 10))));
    }
}
