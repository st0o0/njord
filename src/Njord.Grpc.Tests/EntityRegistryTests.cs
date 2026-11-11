using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class EntityRegistryTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly Location _berlin = new("Berlin", 52.52, 13.405);
    private static readonly DateTimeOffset _anchor = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void Observation_result_maps_to_the_entity_id_of_its_feature_name()
    {
        var registry = Registry([_vienna], new EntityTestRule("Pm25Observation", OutputKind.Observation));

        Assert.Multiple(
            () => Assert.Equal("vienna_pm25_observation", registry.Find(new ObservationResult(_vienna, "Pm25Observation", 42.5))),
            () => Assert.Equal("vienna_pm25_observation", Assert.Single(registry.All).EntityId));
    }

    [Fact]
    public void Observation_features_of_one_location_get_distinct_entity_ids()
    {
        var registry = Registry(
            [_vienna],
            new EntityTestRule("Pm25Observation", OutputKind.Observation),
            new EntityTestRule("UvIndexObservation", OutputKind.Observation));

        Assert.Multiple(
            () => Assert.Equal("vienna_pm25_observation", registry.Find(new ObservationResult(_vienna, "Pm25Observation", 1))),
            () => Assert.Equal("vienna_uv_index_observation", registry.Find(new ObservationResult(_vienna, "UvIndexObservation", 2))),
            () => Assert.Equal(2, registry.Count));
    }

    [Fact]
    public void Observation_of_a_disabled_or_unknown_feature_has_no_entity()
    {
        var registry = Registry(
            [_vienna],
            new EntityTestRule("Pm25Observation", OutputKind.Observation, enabled: false));

        Assert.Multiple(
            () => Assert.Null(registry.Find(new ObservationResult(_vienna, "Pm25Observation", 1))),
            () => Assert.Null(registry.Find(new ObservationResult(_vienna, "Unknown", 1))));
    }

    [Fact]
    public void Alerts_still_resolve_by_result_type_next_to_observations()
    {
        var registry = Registry(
            [_vienna],
            new EntityTestRule("Heat"),
            new EntityTestRule("Pm25Observation", OutputKind.Observation));

        Assert.Multiple(
            () => Assert.Equal("vienna_heat", registry.Find(EnrichmentEvents.Alerts(_vienna, _anchor))),
            () => Assert.Equal("vienna_pm25_observation", registry.Find(new ObservationResult(_vienna, "Pm25Observation", 3))));
    }

    [Fact]
    public void Builds_entity_per_enabled_feature_per_location()
    {
        var registry = Registry(
            [_vienna, _berlin],
            new EntityTestRule("Frost"),
            new EntityTestRule("Heat"));

        Assert.Equal(4, registry.All.Count);
    }

    [Fact]
    public void Excludes_disabled_features()
    {
        var registry = Registry(
            [_vienna],
            new EntityTestRule("Frost"),
            new EntityTestRule("Heat", enabled: false));

        var entity = Assert.Single(registry.All);
        Assert.Equal("vienna_frost", entity.EntityId);
    }

    [Fact]
    public void Entity_id_uses_snake_case_feature_name()
    {
        var registry = Registry([_vienna], new EntityTestRule("SleepQuality", OutputKind.Score));

        var entity = Assert.Single(registry.All);
        Assert.Equal("vienna_sleep_quality", entity.EntityId);
    }

    [Fact]
    public void Weather_feature_uses_weather_suffix()
    {
        var registry = Registry([_vienna], new EntityTestRule("Weather", OutputKind.Weather));

        var entity = Assert.Single(registry.All);
        Assert.Equal("vienna_weather", entity.EntityId);
    }

    [Fact]
    public void Weather_feature_adds_secondary_models()
    {
        var models = new ActiveModels
        {
            ForecastModels = ["best_match", "ecmwf_ifs025"],
            EnsembleModels = ["ncep_gefs025_ensemble_mean"],
        };
        var registry = Registry([_vienna], models, new EntityTestRule("Weather", OutputKind.Weather));

        Assert.Equal(3, registry.All.Count);
        Assert.Equal("vienna_weather", registry.All[0].EntityId);
        Assert.Equal("vienna_ecmwf_ifs025_weather", registry.All[1].EntityId);
        Assert.Equal("vienna_ncep_gefs025_ensemble_mean_weather", registry.All[2].EntityId);
    }

    [Fact]
    public void Primary_weather_result_resolves_without_model_id()
    {
        var models = new ActiveModels
        {
            ForecastModels = ["best_match", "ecmwf_ifs025"],
            EnsembleModels = ["ncep_gefs025_ensemble_mean"],
        };
        var registry = Registry([_vienna], models, new EntityTestRule("Weather", OutputKind.Weather));

        var result = new WeatherResult(_vienna, 20, 19, 55, 3, 270, 1015, "clear", []);
        Assert.Equal("vienna_weather", registry.Find(result));
    }

    [Fact]
    public void Per_model_weather_result_resolves_with_model_id()
    {
        var models = new ActiveModels
        {
            ForecastModels = ["best_match", "ecmwf_ifs025"],
            EnsembleModels = ["ncep_gefs025_ensemble_mean"],
        };
        var registry = Registry([_vienna], models, new EntityTestRule("Weather", OutputKind.Weather));

        var forecastModel = new WeatherResult(_vienna, 20, 19, 55, 3, 270, 1015, "clear", [], ModelId: "ecmwf_ifs025");
        var ensembleModel = new WeatherResult(_vienna, 20, 19, 55, 3, 270, 1015, "clear", [], ModelId: "ncep_gefs025_ensemble_mean");

        Assert.Multiple(
            () => Assert.Equal("vienna_ecmwf_ifs025_weather", registry.Find(forecastModel)),
            () => Assert.Equal("vienna_ncep_gefs025_ensemble_mean_weather", registry.Find(ensembleModel)));
    }

    [Fact]
    public void Unknown_model_id_returns_null()
    {
        var registry = Registry([_vienna], new EntityTestRule("Weather", OutputKind.Weather));

        var result = new WeatherResult(_vienna, 20, 19, 55, 3, 270, 1015, "clear", [], ModelId: "nonexistent_model");
        Assert.Null(registry.Find(result));
    }

    [Fact]
    public void Slugify_handles_spaces_and_special_chars()
    {
        Assert.Equal("new_york", EntityRegistry.Slugify("New York"));
        Assert.Equal("st_polten", EntityRegistry.Slugify("St. Pölten"));
        Assert.Equal("vienna", EntityRegistry.Slugify("Vienna"));
    }

    [Fact]
    public void Snake_case_converts_pascal_case()
    {
        Assert.Equal("sleep_quality", EntityRegistry.ToSnakeCase("SleepQuality"));
        Assert.Equal("frost", EntityRegistry.ToSnakeCase("Frost"));
        Assert.Equal("pm25", EntityRegistry.ToSnakeCase("Pm25"));
    }

    private static EntityRegistry Registry(Location[] locations, params EntityTestRule[] rules) =>
        new(rules, Options.Create(new NjordOptions { ResolvedLocations = locations }), Options.Create(new ActiveModels()));

    private static EntityRegistry Registry(Location[] locations, ActiveModels models, params EntityTestRule[] rules) =>
        new(rules, Options.Create(new NjordOptions { ResolvedLocations = locations }), Options.Create(models));
}
