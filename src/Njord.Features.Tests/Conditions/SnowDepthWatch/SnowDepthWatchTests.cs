using Microsoft.Extensions.Options;
using Njord.Features.Conditions.SnowDepthWatch;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.SnowDepthWatch;

public sealed class SnowDepthWatchTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static SnowDepthLevel? Level(double depthMeters)
    {
        var rule = new Features.Conditions.SnowDepthWatch.SnowDepthWatch(Opts(new SnowDepthWatchConfig { Enabled = true }));
        var weather = Uniform(p => p with { SnowDepth = depthMeters });
        var result = Run(rule, Now, ForecastOnly(weather));
        return result is SnowDepthWatchResult r ? r.Level : null;
    }

    [Fact]
    public void Below_light_threshold_returns_null() =>
        Assert.Null(Level(0.01));

    [Fact]
    public void Light_snow_depth() =>
        Assert.Equal(SnowDepthLevel.Light, Level(0.05));

    [Fact]
    public void Moderate_snow_depth() =>
        Assert.Equal(SnowDepthLevel.Moderate, Level(0.15));

    [Fact]
    public void Heavy_snow_depth() =>
        Assert.Equal(SnowDepthLevel.Heavy, Level(0.30));

    [Fact]
    public void Extreme_snow_depth() =>
        Assert.Equal(SnowDepthLevel.Extreme, Level(0.60));

    [Fact]
    public void Result_carries_depth_in_cm()
    {
        var rule = new Features.Conditions.SnowDepthWatch.SnowDepthWatch(Opts(new SnowDepthWatchConfig { Enabled = true }));
        var weather = Uniform(p => p with { SnowDepth = 0.15 });
        var result = Run(rule, Now, ForecastOnly(weather));
        var condition = Assert.IsType<SnowDepthWatchResult>(result);
        Assert.Equal(15.0, condition.DepthCm);
    }

    [Fact]
    public void Returns_null_when_no_snow_depth()
    {
        var rule = new Features.Conditions.SnowDepthWatch.SnowDepthWatch(Opts(new SnowDepthWatchConfig { Enabled = true }));
        var weather = Uniform(p => p with { SnowDepth = null });
        var result = Run(rule, Now, ForecastOnly(weather));
        Assert.Null(result);
    }
}
