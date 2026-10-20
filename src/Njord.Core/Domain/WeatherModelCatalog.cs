namespace Njord.Core.Domain;

public sealed record WeatherModelInfo(string Id, string DisplayName, string Provider, string Region, int ResolutionKm, int ForecastDays);

public static class WeatherModelCatalog
{
    public static IReadOnlyList<WeatherModelInfo> All { get; } =
    [
        new("ecmwf_ifs025_ensemble_mean", "ECMWF IFS 0.25 deg Ensemble Mean", "ECMWF", "Global", 25, 15),
        new("ecmwf_aifs025_ensemble_mean", "ECMWF AIFS 0.25 deg Ensemble Mean", "ECMWF", "Global", 25, 15),
        new("ukmo_global_ensemble_mean_20km", "UK Met Office Global Ensemble Mean 20 km", "UK Met Office", "Global", 20, 7),
    ];

    public static WeatherModelInfo? Find(string id) => All.FirstOrDefault(m => m.Id == id);
}
