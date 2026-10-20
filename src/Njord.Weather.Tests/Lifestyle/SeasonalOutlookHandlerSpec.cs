using System.Collections.Immutable;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Seasonal;
using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class SeasonalOutlookHandlerSpec
{
    private static readonly FakeTimeProvider October = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private static readonly SeasonalOutlookHandler Handler = new();

    private static SeasonalPoint Month(int year, int month, double temp = 1.0, double? precip = null, double? mean = null) =>
        new(new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero), temp, precip, mean);

    private static LocationForecast With(params SeasonalPoint[] points) =>
        new(Lucerne, Seasonal: new DomainForecast<SeasonalPoint>(
            DomainKind.Seasonal, October.GetUtcNow(), TimeResolution.Monthly, [.. points]));

    [Fact]
    public void Handle_emits_the_months_after_the_current_one_with_rounded_anomalies()
    {
        var events = Handler.Handle(
            With(Month(2026, 10), Month(2026, 11, 1.74, 2.4, 99.5), Month(2026, 12, -0.5, -20, 80)), null, October);

        var outlook = Assert.IsType<PipelineEvent.SeasonalOutlookComputed>(Assert.Single(events));
        Assert.Equal(
            [
                new MonthlyOutlook("2026-11", 1.7, 2.5, OutlookConfidence.High),
                new MonthlyOutlook("2026-12", -0.5, -20, OutlookConfidence.Medium),
            ],
            outlook.Months);
    }

    [Theory]
    [InlineData(1, OutlookConfidence.High)]
    [InlineData(2, OutlookConfidence.Medium)]
    [InlineData(3, OutlookConfidence.Low)]
    public void Handle_lowers_the_confidence_with_the_lead_time(int lead, OutlookConfidence expected)
    {
        var month = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(lead);
        var point = Month(month.Year, month.Month);

        var events = Handler.Handle(With(point), null, October);

        var outlook = Assert.IsType<PipelineEvent.SeasonalOutlookComputed>(Assert.Single(events));
        Assert.Equal(expected, Assert.Single(outlook.Months).Confidence);
    }

    [Fact]
    public void Handle_reports_zero_precipitation_anomaly_when_the_precipitation_series_is_missing()
    {
        var events = Handler.Handle(With(Month(2026, 11)), null, October);

        var outlook = Assert.IsType<PipelineEvent.SeasonalOutlookComputed>(Assert.Single(events));
        Assert.Equal(0, Assert.Single(outlook.Months).PrecipAnomaly);
    }

    [Fact]
    public void Handle_returns_nothing_without_seasonal_data()
    {
        Assert.Empty(Handler.Handle(new LocationForecast(Lucerne), null, October));
    }

    [Fact]
    public void Handle_returns_nothing_when_only_the_current_month_is_known()
    {
        Assert.Empty(Handler.Handle(With(Month(2026, 10)), null, October));
    }

    [Fact]
    public void The_feature_is_off_unless_configured_and_reads_the_seasonal_domain()
    {
        Assert.Multiple(
            () => Assert.Empty(FeatureResolution.EnabledHandlers(new FeatureOptions(), [Handler])),
            () => Assert.Single(FeatureResolution.EnabledHandlers(new FeatureOptions { [FeatureKind.SeasonalOutlook] = true }, [Handler])),
            () => Assert.Equal(
                [DomainKind.Seasonal],
                FeatureResolution.RequiredDomains(new FeatureOptions { [FeatureKind.SeasonalOutlook] = true }, [Handler])));
    }
}
