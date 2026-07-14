using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Enrichment.Features;

namespace Njord.Tests.Enrichment;

public sealed class EnrichmentFeatureContractSpec
{
    private static IReadOnlyList<IEnrichmentFeature> CreateAllFeatures(
        EnrichmentOptions? enrichment = null)
    {
        enrichment ??= new EnrichmentOptions();
        var njordOptions = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var optionsWrapped = Options.Create(njordOptions);
        var enrichmentWrapped = Options.Create(enrichment);
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return
        [
            new ConsensusEnrichment(optionsWrapped, enrichmentWrapped, parameters, TimeProvider.System),
            new AlertEnrichment(enrichmentWrapped, TimeProvider.System),
            new DerivedEnrichment(optionsWrapped, enrichmentWrapped, parameters, TimeProvider.System),
            new TrendEnrichment(optionsWrapped, enrichmentWrapped, parameters, TimeProvider.System),
            new IndexEnrichment(enrichmentWrapped, parameters, TimeProvider.System),
            new EnergyEnrichment(enrichmentWrapped, parameters, TimeProvider.System),
            new HistoryEnrichment(optionsWrapped, enrichmentWrapped, parameters, TimeProvider.System),
        ];
    }

    [Fact(Timeout = 5000)]
    public void All_features_have_unique_type_names()
    {
        var features = CreateAllFeatures();
        var names = features.Select(f => f.TypeName).ToList();

        Assert.Equal(7, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact(Timeout = 5000)]
    public void Type_names_are_kebab_case_identifiers()
    {
        var features = CreateAllFeatures();

        foreach (var feature in features)
        {
            Assert.Matches("^[a-z]+$", feature.TypeName);
        }
    }

    [Fact(Timeout = 5000)]
    public void Enabled_reflects_options_for_default_enabled_features()
    {
        var features = CreateAllFeatures();

        Assert.True(features.Single(f => f.TypeName == "consensus").Enabled);
        Assert.True(features.Single(f => f.TypeName == "alerts").Enabled);
        Assert.True(features.Single(f => f.TypeName == "derived").Enabled);
    }

    [Fact(Timeout = 5000)]
    public void Enabled_reflects_options_for_default_disabled_features()
    {
        var features = CreateAllFeatures();

        Assert.False(features.Single(f => f.TypeName == "trends").Enabled);
        Assert.False(features.Single(f => f.TypeName == "indices").Enabled);
        Assert.False(features.Single(f => f.TypeName == "energy").Enabled);
        Assert.False(features.Single(f => f.TypeName == "history").Enabled);
    }

    [Fact(Timeout = 5000)]
    public void Disabled_feature_becomes_enabled_when_option_is_set()
    {
        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = true } };
        var features = CreateAllFeatures(enrichment);

        Assert.True(features.Single(f => f.TypeName == "trends").Enabled);
    }

    [Fact(Timeout = 5000)]
    public void Device_id_follows_enrichment_pattern()
    {
        var features = CreateAllFeatures();

        foreach (var feature in features)
        {
            var deviceId = feature.DeviceId("lucerne");
            Assert.StartsWith("njord_lucerne_", deviceId);
            Assert.EndsWith(feature.TypeName, deviceId);
        }
    }

    [Fact(Timeout = 5000)]
    public void Stateless_features_implement_IStatelessEnrichment()
    {
        var features = CreateAllFeatures();
        var stateless = new[] { "consensus", "alerts", "derived", "indices", "energy" };

        foreach (var name in stateless)
        {
            var feature = features.Single(f => f.TypeName == name);
            Assert.True(feature.GetType().GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStatelessEnrichment<>)));
        }
    }

    [Fact(Timeout = 5000)]
    public void Trend_feature_implements_IStatefulEnrichment()
    {
        var features = CreateAllFeatures();
        var trend = features.Single(f => f.TypeName == "trends");

        Assert.True(trend.GetType().GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStatefulEnrichment<>)));
    }

    [Fact(Timeout = 5000)]
    public void History_feature_implements_IActorEnrichment()
    {
        var features = CreateAllFeatures();
        var history = features.Single(f => f.TypeName == "history");

        Assert.IsAssignableFrom<IActorEnrichment>(history);
    }
}
