using Njord.Core.Domain;

namespace Njord.Core.Tests.Domain;

public sealed class WeatherModelCatalogTests
{
    [Fact]
    public void Ecmwf_ifs_is_a_global_25_km_model_with_15_forecast_days()
    {
        var model = WeatherModelCatalog.Find("ecmwf_ifs025_ensemble_mean");

        Assert.NotNull(model);
        Assert.Equal(new WeatherModelInfo("ecmwf_ifs025_ensemble_mean", "ECMWF IFS 0.25 deg Ensemble Mean", "ECMWF", "Global", 25, 15), model);
    }

    [Fact]
    public void Ecmwf_aifs_is_a_global_25_km_model_with_15_forecast_days()
    {
        var model = WeatherModelCatalog.Find("ecmwf_aifs025_ensemble_mean");

        Assert.NotNull(model);
        Assert.Equal(new WeatherModelInfo("ecmwf_aifs025_ensemble_mean", "ECMWF AIFS 0.25 deg Ensemble Mean", "ECMWF", "Global", 25, 15), model);
    }

    [Fact]
    public void Ukmo_is_a_global_20_km_model_with_7_forecast_days()
    {
        var model = WeatherModelCatalog.Find("ukmo_global_ensemble_mean_20km");

        Assert.NotNull(model);
        Assert.Equal(new WeatherModelInfo("ukmo_global_ensemble_mean_20km", "UK Met Office Global Ensemble Mean 20 km", "UK Met Office", "Global", 20, 7), model);
    }

    [Fact]
    public void The_catalog_holds_exactly_the_three_known_models()
    {
        Assert.Equal(
            ["ecmwf_ifs025_ensemble_mean", "ecmwf_aifs025_ensemble_mean", "ukmo_global_ensemble_mean_20km"],
            WeatherModelCatalog.All.Select(m => m.Id));
    }

    [Fact]
    public void An_unknown_id_is_not_found()
    {
        Assert.Null(WeatherModelCatalog.Find("gfs_ensemble"));
    }
}
