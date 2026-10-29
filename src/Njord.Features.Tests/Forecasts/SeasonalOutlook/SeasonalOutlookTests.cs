using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Seasonal;
using Njord.Features.Forecasts.SeasonalOutlook;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Forecasts.SeasonalOutlook;

public sealed class SeasonalOutlookTests
{
    private static readonly DateTimeOffset October = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static Features.Forecasts.SeasonalOutlook.SeasonalOutlook Feature() => new(Options.Create(new SeasonalOutlookConfig { Enabled = true }));

    private static SeasonalPoint Month(int year, int month, double temp = 1.0, double? precip = null, double? mean = null) =>
        new(new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero), temp, precip, mean);

    private static LocationForecast With(params SeasonalPoint[] points) =>
        ForecastOnly(Day()) with
        {
            Seasonal = new DomainForecast<SeasonalPoint>(DomainKind.Seasonal, October, TimeResolution.Monthly, [.. points]),
        };

    [Fact]
    public void The_months_after_the_current_one_are_emitted_with_rounded_anomalies()
    {
        var outlook = Assert.IsType<SeasonalOutlookResult>(Run(
            Feature(), October, With(Month(2026, 10), Month(2026, 11, 1.74, 2.4, 99.5), Month(2026, 12, -0.5, -20, 80))));

        Assert.Equal(
            [
                new SeasonalMonth(new DateOnly(2026, 11, 1), 1.7, 2.5, ConfidenceLevel.High),
                new SeasonalMonth(new DateOnly(2026, 12, 1), -0.5, -20.0, ConfidenceLevel.Medium),
            ],
            outlook.Months);
    }

    [Theory]
    [InlineData(1, ConfidenceLevel.High)]
    [InlineData(2, ConfidenceLevel.Medium)]
    [InlineData(3, ConfidenceLevel.Low)]
    public void The_confidence_drops_with_the_lead_time(int lead, ConfidenceLevel expected)
    {
        var month = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(lead);

        var outlook = Assert.IsType<SeasonalOutlookResult>(
            Run(Feature(), October, With(Month(month.Year, month.Month))));

        Assert.Equal(expected, Assert.Single(outlook.Months).Confidence);
    }

    [Fact]
    public void A_missing_precipitation_series_reports_a_zero_anomaly()
    {
        var outlook = Assert.IsType<SeasonalOutlookResult>(Run(Feature(), October, With(Month(2026, 11))));

        Assert.Equal(0.0, Assert.Single(outlook.Months).PrecipitationAnomalyPercent);
    }

    [Fact]
    public void The_outlook_is_emitted_without_weather_data()
    {
        var forecast = new LocationForecast(ForecastOnly(Day()).Location)
        {
            Seasonal = new DomainForecast<SeasonalPoint>(DomainKind.Seasonal, October, TimeResolution.Monthly, [Month(2026, 11, 1.0)]),
        };

        var outlook = Assert.IsType<SeasonalOutlookResult>(Run(Feature(), October, forecast));

        Assert.Equal(new DateOnly(2026, 11, 1), Assert.Single(outlook.Months).Month);
    }

    [Fact]
    public void Nothing_is_reported_without_seasonal_data()
    {
        Assert.Null(Run(Feature(), October, ForecastOnly(Day())));
    }

    [Fact]
    public void Nothing_is_reported_when_only_the_current_month_is_known()
    {
        Assert.Null(Run(Feature(), October, With(Month(2026, 10))));
    }
}
