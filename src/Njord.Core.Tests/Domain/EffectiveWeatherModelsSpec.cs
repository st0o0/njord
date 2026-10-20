using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.Core.Tests.Domain;

public sealed class EffectiveWeatherModelsSpec
{
    private static readonly string[] Three =
        ["ecmwf_aifs025_ensemble_mean", "ecmwf_ifs025_ensemble_mean", "ukmo_global_ensemble_mean_20km"];

    [Fact]
    public void Without_configured_models_the_default_model_is_primary()
    {
        var effective = EffectiveWeatherModels.Resolve(new WeatherOptions(), comparisonEnabled: true);

        Assert.Multiple(
            () => Assert.Equal(["ecmwf_ifs025_ensemble_mean"], effective.Ids),
            () => Assert.Equal("ecmwf_ifs025_ensemble_mean", effective.Primary));
    }

    [Fact]
    public void With_comparison_enabled_all_configured_models_are_effective_in_order()
    {
        var effective = EffectiveWeatherModels.Resolve(new WeatherOptions { Models = Three }, comparisonEnabled: true);

        Assert.Multiple(
            () => Assert.Equal(Three, effective.Ids),
            () => Assert.Equal("ecmwf_aifs025_ensemble_mean", effective.Primary),
            () => Assert.Equal(3, effective.Count));
    }

    [Fact]
    public void With_comparison_disabled_only_the_primary_model_is_effective()
    {
        var options = new WeatherOptions { Models = Three };

        var effective = EffectiveWeatherModels.Resolve(options, comparisonEnabled: false);

        Assert.Multiple(
            () => Assert.Equal(["ecmwf_aifs025_ensemble_mean"], effective.Ids),
            () => Assert.Equal(["ecmwf_ifs025_ensemble_mean", "ukmo_global_ensemble_mean_20km"], effective.Ignored(options)));
    }
}
