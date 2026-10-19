using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Model;
using Njord.Pipeline.Configuration;
using Njord.Tests.Shared;
using Njord.Trends.Configuration;

namespace Njord.Pipeline.Tests.Configuration;

public sealed class AnalysisSettingsFactorySpec
{
    [Fact]
    public void Combines_the_section_settings_with_the_forecast_scope()
    {
        var scope = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "Lucerne" }],
            Horizons = [6, 24],
        };

        var settings = AnalysisSettingsFactory.Create(
            scope, AppSettingsDefaults.Options<ConsensusOptions>(o => o.MinQuorum = 3), new AlertOptions { CapeThreshold = 900 },
            AppSettingsDefaults.Options<IndexOptions>(), AppSettingsDefaults.Options<TrendOptions>(), AppSettingsDefaults.Options<DerivedOptions>());

        Assert.Equal(3, settings.Consensus.MinQuorum);
        Assert.Equal(900, settings.Alerts.CapeThreshold);
        Assert.Equal([6, 24], settings.Derived.Horizons);
        Assert.All(settings.Indices.Preferences.Keys, k => Assert.Equal(new LocationId("Lucerne"), k.Location));
    }
}
