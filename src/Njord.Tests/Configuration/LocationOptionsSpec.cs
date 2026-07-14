using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class LocationOptionsSpec
{
    [Fact(Timeout = 5000)]
    public void Resolve_merges_global_and_location_models()
    {
        var location = new LocationOptions { Models = ["icon_d2"] };

        var resolved = location.ResolveModels(["icon_global", "icon_eu"]);

        Assert.Equal(["icon_global", "icon_eu", "icon_d2"], resolved);
    }

    [Fact(Timeout = 5000)]
    public void Resolve_returns_global_only_when_location_models_is_null()
    {
        var location = new LocationOptions();

        var resolved = location.ResolveModels(["icon_global", "icon_eu"]);

        Assert.Equal(["icon_global", "icon_eu"], resolved);
    }

    [Fact(Timeout = 5000)]
    public void Resolve_deduplicates_models()
    {
        var location = new LocationOptions { Models = ["icon_eu", "icon_d2"] };

        var resolved = location.ResolveModels(["icon_global", "icon_eu"]);

        Assert.Equal(["icon_global", "icon_eu", "icon_d2"], resolved);
    }

    [Fact(Timeout = 5000)]
    public void Resolve_returns_location_only_when_global_is_empty()
    {
        var location = new LocationOptions { Models = ["icon_d2"] };

        var resolved = location.ResolveModels([]);

        Assert.Equal(["icon_d2"], resolved);
    }
}
