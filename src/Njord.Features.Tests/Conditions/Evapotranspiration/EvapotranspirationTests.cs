using Microsoft.Extensions.Options;
using Njord.Features.Conditions.Evapotranspiration;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.Evapotranspiration;

public sealed class EvapotranspirationTests
{
    private static Features.Conditions.Evapotranspiration.Evapotranspiration Rule(
        double low = 2.0, double high = 5.0) =>
        new(Options.Create(new EvapotranspirationConfig { Enabled = true, LowMm = low, HighMm = high }));

    [Fact]
    public void High_level_when_daily_sum_exceeds_high_threshold()
    {
        var weather = Day((_, p) => p with { Evapotranspiration = 0.3 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        var condition = Assert.IsType<EvapotranspirationResult>(result);
        Assert.Multiple(
            () => Assert.Equal(EvapotranspirationLevel.High, condition.Level),
            () => Assert.Equal(7.2, condition.DailyMm));
    }

    [Fact]
    public void Low_level_when_daily_sum_below_low_threshold()
    {
        var weather = Day((_, p) => p with { Evapotranspiration = 0.05 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        var condition = Assert.IsType<EvapotranspirationResult>(result);
        Assert.Equal(EvapotranspirationLevel.Low, condition.Level);
    }

    [Fact]
    public void Moderate_level_when_daily_sum_between_thresholds()
    {
        var weather = Day((_, p) => p with { Evapotranspiration = 0.15 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        var condition = Assert.IsType<EvapotranspirationResult>(result);
        Assert.Equal(EvapotranspirationLevel.Moderate, condition.Level);
    }

    [Fact]
    public void Returns_null_when_evapotranspiration_data_missing()
    {
        var weather = Day((_, p) => p with { Evapotranspiration = null });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        Assert.Null(result);
    }
}
