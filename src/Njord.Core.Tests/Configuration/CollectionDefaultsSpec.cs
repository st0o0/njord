using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class CollectionDefaultsSpec
{
    private static ServiceProvider Build(Dictionary<string, string?> entries)
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(entries).Build());
        services.AddOptions<NjordOptions>().BindConfiguration(NjordOptions.SectionName).ReplaceCollectionDefaults();
        services.AddOptions<AlertOptions>().BindConfiguration(AlertOptions.SectionName).ReplaceCollectionDefaults();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void A_configured_list_replaces_its_default()
    {
        using var provider = Build(new() { ["Njord:Horizons:0"] = "6", ["Njord:Horizons:1"] = "24" });

        Assert.Equal([6, 24], provider.GetRequiredService<IOptions<NjordOptions>>().Value.Horizons);
    }

    [Fact]
    public void A_configured_array_replaces_its_default()
    {
        using var provider = Build(new()
        {
            ["Njord:Enrichment:Alerts:HeatThresholds:0"] = "31",
            ["Njord:Enrichment:Alerts:HeatThresholds:1"] = "36",
            ["Njord:Enrichment:Alerts:HeatThresholds:2"] = "41",
        });

        Assert.Equal([31.0, 36.0, 41.0], provider.GetRequiredService<IOptions<AlertOptions>>().Value.HeatThresholds);
    }

    [Fact]
    public void A_configured_list_in_a_nested_object_replaces_its_default()
    {
        using var provider = Build(new() { ["Njord:Parameters:Groups:0"] = "Solar" });

        Assert.Equal(["Solar"], provider.GetRequiredService<IOptions<NjordOptions>>().Value.Parameters.Groups);
    }

    [Fact]
    public void Unconfigured_collections_keep_their_defaults()
    {
        using var provider = Build([]);

        Assert.Equal([3, 6, 12, 24, 48, 72], provider.GetRequiredService<IOptions<NjordOptions>>().Value.Horizons);
        Assert.Equal([30.0, 35.0, 40.0], provider.GetRequiredService<IOptions<AlertOptions>>().Value.HeatThresholds);
    }
}
