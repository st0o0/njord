using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Njord.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Flood;
using Njord.Marine;
using Njord.Tests.Shared;
using Njord.Weather.Alerts;

namespace Njord.Architecture.Tests;

public sealed class FeaturesDataSpec
{
    private static IReadOnlyList<IFeature> RegisteredFeatures()
    {
        using var provider = FeatureServices.Build(TimeProvider.System, services =>
        {
            services.AddAlertOptions(AppSettingsDefaults.Configuration());
            services.AddWeatherDomain().AddAirQualityDomain().AddFloodDomain().AddMarineDomain();
        });
        return FeatureResolution.AllFeatures(provider.GetServices<IDomainHandler>());
    }

    private static string FeaturesJsonPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "data", "features.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "docs", "data", "features.json");
    }

    private static Dictionary<string, JsonElement> Entries()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FeaturesJsonPath()));
        return document.RootElement.EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("key").GetString() ?? string.Empty, entry => entry.Clone());
    }

    [Fact]
    public void Features_json_lists_exactly_the_registered_features()
    {
        var expected = RegisteredFeatures().Select(feature => feature.SectionKey()).Order(StringComparer.Ordinal);

        Assert.Equal(expected, Entries().Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Features_json_entries_match_the_registered_descriptors()
    {
        var entries = Entries();
        var mismatches = new List<string>();

        foreach (var feature in RegisteredFeatures())
        {
            if (!entries.TryGetValue(feature.SectionKey(), out var entry))
            {
                mismatches.Add($"{feature.SectionKey()}: missing");
                continue;
            }

            var descriptor = feature.Feature;
            if (entry.GetProperty("name").GetString() != feature.Name
                || entry.GetProperty("category").GetString() != descriptor.Category.ToString()
                || entry.GetProperty("output").GetString() != descriptor.Output.ToString()
                || entry.GetProperty("enabledByDefault").GetBoolean() != descriptor.DefaultEnabled)
            {
                mismatches.Add(feature.SectionKey());
            }
        }

        Assert.Empty(mismatches);
    }
}
