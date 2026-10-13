using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
using Njord.Weather;

namespace Njord.Core.Tests.Configuration;

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
            scope, new ConsensusOptions { MinQuorum = 3 }, new AlertOptions { CapeThreshold = 900 },
            new IndexOptions(), new TrendOptions(), new DerivedOptions());

        Assert.Equal(3, settings.Consensus.MinQuorum);
        Assert.Equal(900, settings.Alerts.CapeThreshold);
        Assert.Equal([6, 24], settings.Derived.Horizons);
        Assert.All(settings.Indices.Preferences.Keys, k => Assert.Equal(new LocationId("Lucerne"), k.Location));
    }
}
