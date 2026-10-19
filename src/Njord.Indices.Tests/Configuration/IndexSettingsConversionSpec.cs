using Njord.Indices.Configuration;
using Njord.Messages.Indices;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Indices.Tests.Configuration;

public sealed class IndexSettingsConversionSpec
{
    [Fact]
    public void Preferences_are_resolved_once_for_every_location_and_index()
    {
        var settings = AppSettingsDefaults.Options<IndexOptions>().ToSettings(["Lucerne", "Bern"]);

        Assert.Equal(2 * IndexKinds.All.Count, settings.Preferences.Count);
        Assert.All(settings.Preferences.Values, p => Assert.Equal(ResolvedPreferences.Default, p));
    }

    [Fact]
    public void The_override_cascade_is_applied()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { IndoorTemp = 20 },
            LocationOverrides = [new LocationIndexOverride { Location = "bern", Preferences = new IndexPreferences { IndoorTemp = 23 } }],
        };

        var settings = options.ToSettings(["Lucerne", "Bern"]);

        var kind = IndexKinds.All[0];
        Assert.Equal(20, settings.Preferences[(new LocationId("Lucerne"), kind)].IndoorTemp);
        Assert.Equal(23, settings.Preferences[(new LocationId("Bern"), kind)].IndoorTemp);
    }
}
