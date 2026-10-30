using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class ActiveModelsTests
{
    [Fact]
    public void Default_primary_is_best_match()
    {
        var models = new ActiveModels();

        Assert.Equal(ActiveModels.BestMatch, models.PrimaryForecastModel);
    }

    [Fact]
    public void Default_lists_are_empty()
    {
        var models = new ActiveModels();

        Assert.Multiple(
            () => Assert.Empty(models.ForecastModels),
            () => Assert.Empty(models.EnsembleModels));
    }

    [Fact]
    public void AllModels_with_defaults_contains_only_best_match()
    {
        var models = new ActiveModels();

        Assert.Equal(["best_match"], models.AllModels);
    }

    [Fact]
    public void AllModels_orders_primary_then_secondary_forecast_then_ensemble()
    {
        var models = new ActiveModels
        {
            PrimaryForecastModel = "icon_d2",
            ForecastModels = ["icon_d2", "icon_eu"],
            EnsembleModels = ["ecmwf_ifs025_ensemble_mean"],
        };

        Assert.Equal(["icon_d2", "icon_eu", "ecmwf_ifs025_ensemble_mean"], models.AllModels);
    }

    [Fact]
    public void AllModels_with_only_ensemble_puts_best_match_first()
    {
        var models = new ActiveModels
        {
            EnsembleModels = ["ecmwf_ifs025_ensemble_mean", "ecmwf_aifs025_ensemble_mean"],
        };

        Assert.Equal(["best_match", "ecmwf_ifs025_ensemble_mean", "ecmwf_aifs025_ensemble_mean"], models.AllModels);
    }

    [Fact]
    public void AllModels_with_single_forecast_model_skips_duplicate()
    {
        var models = new ActiveModels
        {
            PrimaryForecastModel = "icon_d2",
            ForecastModels = ["icon_d2"],
            EnsembleModels = ["ecmwf_ifs025_ensemble_mean"],
        };

        Assert.Equal(["icon_d2", "ecmwf_ifs025_ensemble_mean"], models.AllModels);
    }
}
