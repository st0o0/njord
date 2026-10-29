using Microsoft.Extensions.Options;
using Njord.Features.Forecasts.GrowingDegree;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Forecasts.GrowingDegree;

public sealed class GrowingDegreeTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static Features.Forecasts.GrowingDegree.GrowingDegree Rule(double baseTemp = 5.0) =>
        new(Opts(new GrowingDegreeConfig { Enabled = true, BaseTemperature = baseTemp }));

    [Fact]
    public void Warm_day_accumulates_gdd()
    {
        var weather = Day((_, p) => p with { Temperature = 20.0 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        Assert.NotNull(result);
        var gdd = Assert.IsType<GrowingDegreeResult>(result, exactMatch: false);
        Assert.True(gdd.CumulativeGdd > 0);
        Assert.NotEmpty(gdd.Days);
    }

    [Fact]
    public void Cold_day_accumulates_zero()
    {
        var weather = Day((_, p) => p with { Temperature = 2.0 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        Assert.NotNull(result);
        var gdd = Assert.IsType<GrowingDegreeResult>(result, exactMatch: false);
        Assert.Equal(0, gdd.CumulativeGdd);
    }

    [Fact]
    public void Custom_base_temperature()
    {
        var weather = Day((_, p) => p with { Temperature = 12.0 });
        var result = Run(Rule(baseTemp: 10.0), Now, ForecastOnly(weather));
        Assert.NotNull(result);
        var gdd = Assert.IsType<GrowingDegreeResult>(result, exactMatch: false);
        Assert.True(gdd.CumulativeGdd > 0);
    }

    [Fact]
    public void Multi_day_accumulation()
    {
        var weather = Hourly(48, (_, p) => p with { Temperature = 20.0 });
        var result = Run(Rule(), Now, ForecastOnly(weather));
        Assert.NotNull(result);
        var gdd = Assert.IsType<GrowingDegreeResult>(result, exactMatch: false);
        Assert.True(gdd.Days.Length >= 2);
        Assert.True(gdd.CumulativeGdd > 0);
    }
}
