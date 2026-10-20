using System.Globalization;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class ClimateAnomalyHandlerSpec
{
    private static ClimateAnomalyHandler HandlerWith(double meanTemp, double meanPrecip)
    {
        var store = new ClimateBaselineStore();
        store.Replace(new Dictionary<string, MonthlyBaseline[]>
        {
            [Lucerne.Name] = [.. Enumerable.Range(1, 12).Select(month => new MonthlyBaseline(month, meanTemp, meanPrecip, 6.0))],
        });
        return new ClimateAnomalyHandler(store);
    }

    [Fact]
    public void Handle_reports_a_warm_anomaly_against_the_monthly_mean()
    {
        var forecast = WeatherOnly(Uniform(point => point with { Temperature = 28.0, Precipitation = 0.0 }));

        var anomaly = Assert.IsType<PipelineEvent.ClimateAnomalyComputed>(
            Assert.Single(HandlerWith(24.0, 4.0).Handle(forecast, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(4.0, anomaly.TempAnomaly, 6),
            () => Assert.Equal($"1991-2020 {Now.ToString("MMMM", CultureInfo.InvariantCulture)} average", anomaly.BaselinePeriod));
    }

    [Fact]
    public void Handle_reports_a_dry_anomaly_as_minus_one_hundred_percent()
    {
        var forecast = WeatherOnly(Uniform(point => point with { Temperature = 24.0, Precipitation = 0.0 }));

        var anomaly = Assert.IsType<PipelineEvent.ClimateAnomalyComputed>(
            Assert.Single(HandlerWith(24.0, 4.0).Handle(forecast, null, Time)));

        Assert.Equal(-100.0, anomaly.PrecipAnomaly, 6);
    }

    [Fact]
    public void Handle_returns_nothing_without_a_baseline()
    {
        var forecast = WeatherOnly(Uniform(point => point));

        Assert.Empty(new ClimateAnomalyHandler(new ClimateBaselineStore()).Handle(forecast, null, Time));
    }
}
