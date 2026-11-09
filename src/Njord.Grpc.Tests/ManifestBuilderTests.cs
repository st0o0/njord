using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Grpc.Tests;

public sealed class ManifestBuilderTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly Location _berlin = new("Berlin", 52.52, 13.405);

    [Fact]
    public void Builds_entity_per_enabled_feature_per_location()
    {
        var rules = new IFeatureRule[]
        {
            StubRule("Frost", OutputKind.Alert, enabled: true),
            StubRule("Heat", OutputKind.Alert, enabled: true),
        };
        var builder = Build(rules, [_vienna, _berlin]);

        Assert.Equal(4, builder.Entities.Count);
    }

    [Fact]
    public void Excludes_disabled_features()
    {
        var rules = new IFeatureRule[]
        {
            StubRule("Frost", OutputKind.Alert, enabled: true),
            StubRule("Heat", OutputKind.Alert, enabled: false),
        };
        var builder = Build(rules, [_vienna]);

        var entity = Assert.Single(builder.Entities);
        Assert.Equal("vienna_frost", entity.EntityId);
    }

    [Fact]
    public void Entity_id_uses_snake_case_feature_name()
    {
        var rules = new IFeatureRule[]
        {
            StubRule("SleepQuality", OutputKind.Score, enabled: true),
        };
        var builder = Build(rules, [_vienna]);

        var entity = Assert.Single(builder.Entities);
        Assert.Equal("vienna_sleep_quality", entity.EntityId);
    }

    [Fact]
    public void Weather_feature_uses_weather_suffix()
    {
        var rules = new IFeatureRule[]
        {
            StubRule("Weather", OutputKind.Weather, enabled: true,
                meta: new EntityMetadata(EntityPlatform.Weather)),
        };
        var builder = Build(rules, [_vienna]);

        var entity = Assert.Single(builder.Entities);
        Assert.Equal("vienna_weather", entity.EntityId);
    }

    [Fact]
    public void Weather_feature_adds_secondary_models()
    {
        var rules = new IFeatureRule[]
        {
            StubRule("Weather", OutputKind.Weather, enabled: true,
                meta: new EntityMetadata(EntityPlatform.Weather)),
        };
        var models = new ActiveModels
        {
            ForecastModels = ["best_match", "ecmwf_ifs025"],
            EnsembleModels = ["ncep_gefs025_ensemble_mean"],
        };
        var builder = Build(rules, [_vienna], models);

        Assert.Equal(3, builder.Entities.Count);
        Assert.Equal("vienna_weather", builder.Entities[0].EntityId);
        Assert.Equal("vienna_ecmwf_ifs025_weather", builder.Entities[1].EntityId);
        Assert.Equal("vienna_ncep_gefs025_ensemble_mean_weather", builder.Entities[2].EntityId);
    }

    [Fact]
    public void Maps_entity_metadata_to_definition()
    {
        var rules = new IFeatureRule[]
        {
            StubRule("Pm25", OutputKind.Observation, enabled: true,
                meta: new EntityMetadata(EntityPlatform.Sensor, "pm25", "mdi:molecule", "µg/m³")),
        };
        var builder = Build(rules, [_vienna]);

        var entity = Assert.Single(builder.Entities);
        Assert.Equal(Proto.EntityPlatform.Sensor, entity.Platform);
        Assert.Equal("pm25", entity.DeviceClass);
        Assert.Equal("mdi:molecule", entity.Icon);
        Assert.Equal("µg/m³", entity.Unit);
    }

    [Fact]
    public void Output_is_deterministic()
    {
        var rules = new IFeatureRule[]
        {
            StubRule("Frost", OutputKind.Alert, enabled: true),
            StubRule("Heat", OutputKind.Alert, enabled: true),
        };

        var a = Build(rules, [_vienna, _berlin]);
        var b = Build(rules, [_vienna, _berlin]);

        Assert.Equal(a.Entities.Count, b.Entities.Count);
        for (var i = 0; i < a.Entities.Count; i++)
        {
            Assert.Equal(a.Entities[i].EntityId, b.Entities[i].EntityId);
        }
    }

    [Fact]
    public void Slugify_handles_spaces_and_special_chars()
    {
        Assert.Equal("new_york", ManifestBuilder.Slugify("New York"));
        Assert.Equal("st_polten", ManifestBuilder.Slugify("St. Pölten"));
        Assert.Equal("vienna", ManifestBuilder.Slugify("Vienna"));
    }

    [Fact]
    public void Snake_case_converts_pascal_case()
    {
        Assert.Equal("sleep_quality", ManifestBuilder.ToSnakeCase("SleepQuality"));
        Assert.Equal("frost", ManifestBuilder.ToSnakeCase("Frost"));
        Assert.Equal("pm25", ManifestBuilder.ToSnakeCase("Pm25"));
    }

    private static ManifestBuilder Build(
        IFeatureRule[] rules,
        Location[] locations,
        ActiveModels? models = null)
    {
        var options = new NjordOptions { ResolvedLocations = locations };
        return new ManifestBuilder(rules, options, models ?? new ActiveModels());
    }

    private static StubFeatureRule StubRule(
        string name,
        OutputKind output,
        bool enabled,
        EntityMetadata? meta = null) =>
        new(name, output, enabled, meta ?? new EntityMetadata(EntityPlatform.BinarySensor));

    private sealed class StubFeatureRule(string name, OutputKind output, bool enabled, EntityMetadata meta) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(
            output, FeatureCategory.Forecast, name, new DomainRequirements(),
            EntityMetadata: meta);

        public bool Enabled => enabled;

        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }
}
