using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Derived.Configuration;
using Njord.Tests.Shared;

namespace Njord.Derived.Tests;

public sealed class DerivedRegistrationSpec
{
    [Fact]
    public void Add_derived_registers_the_analyzer_and_binds_the_shipped_options()
    {
        using var provider = FeatureServices.Build(TestTimeouts.AtEpoch(), services => services.AddDerived(AppSettingsDefaults.Configuration()));

        var analyzer = provider.GetRequiredService<DerivedAnalyzer>();
        var options = provider.GetRequiredService<IOptions<DerivedOptions>>().Value;

        Assert.Multiple(
            () => Assert.Equal(AppSettingsDefaults.Options<DerivedOptions>().Enabled, options.Enabled),
            () => Assert.Equal(options.Enabled, analyzer.Enabled));
    }

    [Fact]
    public void The_analyzer_is_a_singleton()
    {
        using var provider = FeatureServices.Build(TestTimeouts.AtEpoch(), services => services.AddDerived(AppSettingsDefaults.Configuration()));

        Assert.Same(provider.GetRequiredService<DerivedAnalyzer>(), provider.GetRequiredService<DerivedAnalyzer>());
    }
}
