using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Trends.Configuration;
using Njord.Tests.Shared;

namespace Njord.Trends.Tests;

public sealed class TrendRegistrationSpec
{
    [Fact]
    public void Add_trends_registers_the_analyzer_and_binds_the_shipped_options()
    {
        using var provider = FeatureServices.Build(TimeProvider.System, services => services.AddTrends(AppSettingsDefaults.Configuration()));

        var analyzer = provider.GetRequiredService<TrendAnalyzer>();
        var options = provider.GetRequiredService<IOptions<TrendOptions>>().Value;

        Assert.Multiple(
            () => Assert.Equal(AppSettingsDefaults.Options<TrendOptions>().Enabled, options.Enabled),
            () => Assert.Equal(options.Enabled, analyzer.Enabled));
    }

    [Fact]
    public void The_analyzer_is_a_singleton()
    {
        using var provider = FeatureServices.Build(TimeProvider.System, services => services.AddTrends(AppSettingsDefaults.Configuration()));

        Assert.Same(provider.GetRequiredService<TrendAnalyzer>(), provider.GetRequiredService<TrendAnalyzer>());
    }
}
