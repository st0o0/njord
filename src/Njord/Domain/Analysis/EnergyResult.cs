using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record EnergyResult(
    string Location,
    int HeatingDemand,
    double? CopEstimate,
    IReadOnlyList<(int HoursFromNow, double Cop)> CopOptimal,
    int Shading,
    string BatteryStrategy,
    int NightCooling)
{
    public static EnergyResult Compute(
        ModelSnapshot snapshot,
        string location,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        EnergyOptions options)
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddHours(24);

        var tempParam = parameters.Get(ParameterRegistry.Temperature2m);
        var windParam = parameters.Get(ParameterRegistry.WindSpeed10m);
        var cloudParam = parameters.Get(ParameterRegistry.CloudCover);
        var radiationParam = parameters.Get(ParameterRegistry.ShortwaveRadiation);
        var isDayParam = parameters.Get(ParameterRegistry.IsDay);
        var humidityParam = parameters.Get(ParameterRegistry.RelativeHumidity2m);
        var rainProbParam = parameters.Get(ParameterRegistry.PrecipitationProbability);

        var forecasts = snapshot.Entries
            .Where(e => e.Key.Location == location)
            .Select(e => e.Value)
            .ToList();

        var meanTemp = Mean24h(forecasts, tempParam, now, cutoff);
        var meanWind = Mean24h(forecasts, windParam, now, cutoff);
        var meanCloud = Mean24h(forecasts, cloudParam, now, cutoff);
        var meanRadiation = Mean24h(forecasts, radiationParam, now, cutoff);
        var meanIsDay = Mean24h(forecasts, isDayParam, now, cutoff);

        var heatingDemand = EnergyForecaster.HeatingDemand(meanTemp, meanWind, meanCloud, options.HeatingBaseTemp);
        var copEst = EnergyForecaster.CopEstimate(meanTemp, options.FlowTemp, options.CarnotEfficiency);

        IReadOnlyList<(int, double)> copOptimal = [];
        if (tempParam is not null && forecasts.Count > 0)
        {
            copOptimal = EnergyForecaster.CopOptimalHours(
                forecasts[0].Hourly, tempParam, options.FlowTemp,
                options.CarnotEfficiency, options.CopOptimalHours, now);
        }

        var shading = EnergyForecaster.ShadingScore(meanRadiation, meanIsDay, meanTemp);

        var solarYield = IndexScorer.SolarYield(meanRadiation, meanCloud, meanTemp);
        var batteryStrategy = EnergyForecaster.BatteryStrategy(solarYield, meanIsDay);

        var nightCooling = 0;
        if (tempParam is not null && humidityParam is not null &&
            windParam is not null && rainProbParam is not null && forecasts.Count > 0)
        {
            nightCooling = EnergyForecaster.NightCoolingPotential(
                forecasts[0].Hourly, tempParam, humidityParam, windParam, rainProbParam,
                options.IndoorTemp, now);
        }

        return new EnergyResult(location, heatingDemand, copEst, copOptimal, shading, batteryStrategy, nightCooling);
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
