using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Domain.Options;
using Njord.Domain.Weather;
using Njord.Enrichment.Features;
using Njord.Mqtt;
using Njord.Mqtt.Presentation;

namespace Njord.Enrichment.Tests;

public sealed class EnrichmentFeatureContractSpec
{
    private static readonly FakeTimeProvider Time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));

    private static IReadOnlyList<IEnrichmentFeature> CreateAllFeatures(
        EnrichmentOptions? enrichment = null)
    {
        var njordOptions = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
            Enrichment = enrichment ?? new EnrichmentOptions(),
        };
        var optionsWrapped = Options.Create(njordOptions);
        var enrichmentWrapped = Options.Create(njordOptions.Enrichment);
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return
        [
            new AlertEnrichment(enrichmentWrapped, Time),
            new DerivedEnrichment(optionsWrapped, enrichmentWrapped, new DerivedResultComputer(parameters)),
            new TrendEnrichment(enrichmentWrapped, new TrendComputer()),
            new IndexEnrichment(optionsWrapped, enrichmentWrapped, new IndexComputer(parameters, Time)),
            new HistoryEnrichment(optionsWrapped, enrichmentWrapped, parameters, Time,
                new HistoryComputer(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<HistoryEnrichment>.Instance),
        ];
    }

    [Fact]
    public void All_features_have_unique_type_names()
    {
        var features = CreateAllFeatures();
        var names = features.Select(f => f.TypeName).ToList();

        Assert.Equal(5, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Consensus_is_not_in_the_feature_registry()
    {
        var features = CreateAllFeatures();

        Assert.DoesNotContain(features, f => f.TypeName == "consensus");
    }

    [Fact]
    public void Type_names_are_kebab_case_identifiers()
    {
        var features = CreateAllFeatures();

        foreach (var feature in features)
        {
            Assert.Matches("^[a-z]+$", feature.TypeName);
        }
    }

    [Fact]
    public void Enabled_reflects_options_for_default_enabled_features()
    {
        var features = CreateAllFeatures();

        Assert.True(features.Single(f => f.TypeName == "alerts").Enabled);
        Assert.True(features.Single(f => f.TypeName == "derived").Enabled);
    }

    [Fact]
    public void Enabled_reflects_options_for_default_disabled_features()
    {
        var features = CreateAllFeatures();

        Assert.False(features.Single(f => f.TypeName == "trends").Enabled);
        Assert.False(features.Single(f => f.TypeName == "indices").Enabled);
        Assert.False(features.Single(f => f.TypeName == "history").Enabled);
    }

    [Fact]
    public void Disabled_feature_becomes_enabled_when_option_is_set()
    {
        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = true } };
        var features = CreateAllFeatures(enrichment);

        Assert.True(features.Single(f => f.TypeName == "trends").Enabled);
    }

    private static IReadOnlyList<IEnrichmentPresenter> CreateAllPresenters()
    {
        var optionsWrapped = Options.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        });

        return
        [
            new ConsensusPresenter(optionsWrapped, ParameterRegistry.Resolve(["Weather"], [], [])),
            new AlertPresenter(optionsWrapped),
            new DerivedPresenter(optionsWrapped),
            new TrendPresenter(optionsWrapped),
            new IndexPresenter(optionsWrapped),
            new HistoryPresenter(optionsWrapped),
        ];
    }

    [Fact]
    public void Device_id_follows_enrichment_pattern()
    {
        var presenters = CreateAllPresenters();

        foreach (var presenter in presenters)
        {
            var deviceId = presenter.DeviceId("lucerne");
            Assert.StartsWith("njord_lucerne_", deviceId);
            Assert.EndsWith(presenter.TypeName, deviceId);
        }
    }

    [Fact]
    public void Presenter_set_matches_feature_set_plus_consensus()
    {
        var expected = CreateAllFeatures().Select(f => f.TypeName).Append("consensus").Order().ToList();
        var presenterNames = CreateAllPresenters().Select(p => p.TypeName).Order().ToList();

        Assert.Equal(expected, presenterNames);
    }

    [Fact]
    public void Consensus_presenter_is_registered_first_and_always_enabled()
    {
        var disabled = new NjordOptions
        {
            Enrichment = new EnrichmentOptions { Consensus = new ConsensusOptions { Enabled = false } },
        };
        var presenter = new ConsensusPresenter(Options.Create(disabled), ParameterRegistry.Resolve(["Weather"], [], []));

        var presenters = CreateAllPresenters();
        Assert.True(presenters.Count > 0, "Expected at least one presenter");
        Assert.Equal("consensus", presenters[0].TypeName);
        Assert.True(presenter.Enabled);
    }

    [Fact]
    public void Presenter_enabled_matches_feature_enabled()
    {
        var enrichment = new EnrichmentOptions { Trends = new TrendOptions { Enabled = true } };
        var options = Options.Create(new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
            Enrichment = enrichment,
        });
        var features = CreateAllFeatures(enrichment);
        IReadOnlyList<IEnrichmentPresenter> presenters =
        [
            new AlertPresenter(options), new DerivedPresenter(options), new TrendPresenter(options),
            new IndexPresenter(options), new HistoryPresenter(options),
        ];

        foreach (var feature in features)
        {
            Assert.Equal(feature.Enabled, presenters.Single(p => p.TypeName == feature.TypeName).Enabled);
        }
    }

    [Fact]
    public void Feature_interfaces_expose_no_mqtt_surface()
    {
        var featureInterfaces = new[]
        {
            typeof(IEnrichmentFeature), typeof(IStatelessEnrichment),
            typeof(IStatefulEnrichment), typeof(IActorEnrichment),
        };

        foreach (var featureInterface in featureInterfaces)
        {
            foreach (var method in featureInterface.GetMethods())
            {
                Assert.DoesNotContain(typeof(MqttMessage), method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType));
                Assert.DoesNotContain(typeof(DiscoveryContext), method.GetParameters().Select(p => p.ParameterType));
            }
        }

        Assert.Equal(["Enabled", "TypeName"], typeof(IEnrichmentFeature).GetProperties().Select(p => p.Name).Order());
        Assert.DoesNotContain(typeof(IEnrichmentFeature).GetMethods(), m => !m.IsSpecialName);
    }

    [Fact]
    public void Stateless_features_implement_IStatelessEnrichment()
    {
        var features = CreateAllFeatures();
        var stateless = new[] { "alerts", "derived", "indices" };

        foreach (var name in stateless)
        {
            var feature = features.Single(f => f.TypeName == name);
            Assert.IsAssignableFrom<IStatelessEnrichment>(feature);
        }
    }

    [Fact]
    public void Trend_feature_implements_IStatefulEnrichment()
    {
        var features = CreateAllFeatures();
        var trend = features.Single(f => f.TypeName == "trends");

        Assert.IsAssignableFrom<IStatefulEnrichment>(trend);
    }

    [Fact]
    public void History_feature_implements_IActorEnrichment()
    {
        var features = CreateAllFeatures();
        var history = features.Single(f => f.TypeName == "history");

        Assert.IsAssignableFrom<IActorEnrichment>(history);
    }
}
