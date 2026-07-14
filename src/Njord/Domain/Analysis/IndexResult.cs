using System.Text.Json.Nodes;
using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record IndexResult(
    string Location,
    int Laundry,
    int Outdoor,
    int Running,
    int Cycling,
    int Bbq,
    int Irrigation,
    double Hdd,
    double Cdd,
    int Solar,
    int Ventilation,
    (int HoursUntilFrost, double Confidence)? FrostProtection,
    (string Category, double Vpd)? Vpd)
{
    public static IndexResult Compute(
        ModelSnapshot snapshot,
        string location,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IndexOptions options)
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddHours(24);

        var tempParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "temperature_2m");
        var humidityParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "relative_humidity_2m");
        var windParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "wind_speed_10m");
        var precipProbParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "precipitation_probability");
        var cloudParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "cloud_cover");
        var radiationParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "shortwave_radiation");
        var etParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "et0_fao_evapotranspiration");
        var sunshineDurationParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "sunshine_duration");
        var isDayParam = parameters.Hourly.FirstOrDefault(p => p.ApiName == "is_day");

        var forecasts = snapshot.Entries
            .Where(e => e.Key.Location == location)
            .Select(e => e.Value)
            .ToList();

        var meanTemp = Mean24h(forecasts, tempParam, now, cutoff);
        var meanHumidity = Mean24h(forecasts, humidityParam, now, cutoff);
        var meanWind = Mean24h(forecasts, windParam, now, cutoff);
        var meanRainProb = Mean24h(forecasts, precipProbParam, now, cutoff);
        var meanCloud = Mean24h(forecasts, cloudParam, now, cutoff);
        var meanRadiation = Mean24h(forecasts, radiationParam, now, cutoff);
        var meanEt = Mean24h(forecasts, etParam, now, cutoff);

        double? sunshinePct = null;
        if (sunshineDurationParam is not null && isDayParam is not null && forecasts.Count > 0)
            sunshinePct = DerivedComputer.SunshinePercent(forecasts[0].Hourly, sunshineDurationParam, isDayParam, now);

        var laundry = IndexScorer.LaundryDrying(meanTemp, meanHumidity, meanWind, meanRainProb, sunshinePct);
        var outdoor = IndexScorer.OutdoorScore(meanTemp, meanRainProb, meanWind, meanCloud);
        var running = IndexScorer.RunningComfort(meanTemp, meanHumidity, meanWind, meanRainProb);
        var cycling = IndexScorer.CyclingComfort(meanTemp, meanHumidity, meanWind, meanRainProb);
        var bbq = IndexScorer.BbqWeather(meanTemp, meanHumidity, meanWind, meanRainProb);
        var irrigation = IndexScorer.IrrigationNeed(meanRainProb, meanTemp, meanHumidity, meanEt);
        var hdd = meanTemp.HasValue ? IndexScorer.HeatingDegreeDays(meanTemp.Value, options.HeatingBaseTemp) : 0;
        var cdd = meanTemp.HasValue ? IndexScorer.CoolingDegreeDays(meanTemp.Value, options.CoolingBaseTemp) : 0;
        var solar = IndexScorer.SolarYield(meanRadiation, meanCloud, meanTemp);
        var ventilation = IndexScorer.Ventilation(meanTemp, options.IndoorTemp, meanHumidity, meanWind, meanRainProb);

        var frostSeries = forecasts.Select(f => f.Hourly).ToList();
        var frost = tempParam is not null
            ? IndexScorer.FrostProtection(frostSeries, tempParam, now)
            : null;

        var vpd = IndexScorer.VpdCategory(meanTemp, meanHumidity);

        return new IndexResult(location, laundry, outdoor, running, cycling, bbq, irrigation,
            hdd, cdd, solar, ventilation, frost, vpd);
    }

    private static double? Mean24h(
        List<ModelForecast> forecasts, ParameterDef? param,
        DateTimeOffset now, DateTimeOffset cutoff)
    {
        if (param is null || forecasts.Count == 0) return null;

        double sum = 0;
        var count = 0;

        foreach (var forecast in forecasts)
        {
            foreach (var point in forecast.Hourly.Points)
            {
                if (point.ValidAt < now || point.ValidAt > cutoff) continue;
                var val = point.Get(param);
                if (val is not { } v) continue;
                sum += v;
                count++;
            }
        }

        return count > 0 ? sum / count : null;
    }
}
