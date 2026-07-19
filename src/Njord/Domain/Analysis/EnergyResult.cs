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
    int NightCooling,
    int HeatingDemandMax = 0,
    double? CopEstimateMin = null,
    IReadOnlyList<int>? CopOptimalConservative = null)
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

        var (hdMax, copMin, copConservative) = ComputeEnvelope(
            forecasts, tempParam, windParam, cloudParam, radiationParam,
            isDayParam, heatingDemand, copEst, options, now, cutoff);

        return new EnergyResult(location, heatingDemand, copEst, copOptimal, shading, batteryStrategy, nightCooling,
            hdMax, copMin, copConservative);
    }

    private static (int HeatingDemandMax, double? CopEstimateMin, IReadOnlyList<int> CopOptimalConservative) ComputeEnvelope(
        List<ModelForecast> forecasts,
        ParameterDef? tempParam, ParameterDef? windParam, ParameterDef? cloudParam,
        ParameterDef? radiationParam, ParameterDef? isDayParam,
        int fallbackHd, double? fallbackCop,
        EnergyOptions options, DateTimeOffset now, DateTimeOffset cutoff)
    {
        if (forecasts.Count < 2)
            return (fallbackHd, fallbackCop, []);

        var heatingDemands = new List<int>();
        var copEstimates = new List<double>();
        var copOptimalSets = new List<HashSet<int>>();

        foreach (var forecast in forecasts)
        {
            var single = new List<ModelForecast> { forecast };
            var t = Mean24h(single, tempParam, now, cutoff);
            var w = Mean24h(single, windParam, now, cutoff);
            var cl = Mean24h(single, cloudParam, now, cutoff);

            heatingDemands.Add(EnergyForecaster.HeatingDemand(t, w, cl, options.HeatingBaseTemp));

            var cop = EnergyForecaster.CopEstimate(t, options.FlowTemp, options.CarnotEfficiency);
            if (cop.HasValue)
                copEstimates.Add(cop.Value);

            if (tempParam is not null)
            {
                var optimal = EnergyForecaster.CopOptimalHours(
                    forecast.Hourly, tempParam, options.FlowTemp,
                    options.CarnotEfficiency, options.CopOptimalHours, now);
                copOptimalSets.Add(new HashSet<int>(optimal.Select(o => o.Item1)));
            }
        }

        var hdMax = heatingDemands.Count > 0 ? heatingDemands.Max() : fallbackHd;
        var copMin = copEstimates.Count > 0 ? copEstimates.Min() : fallbackCop;

        IReadOnlyList<int> conservative = [];
        if (copOptimalSets.Count >= 2)
        {
            var intersection = copOptimalSets[0];
            for (var i = 1; i < copOptimalSets.Count; i++)
                intersection.IntersectWith(copOptimalSets[i]);
            conservative = intersection.OrderBy(h => h).ToList();
        }

        return (hdMax, copMin, conservative);
    }

    private static double? Mean24h(
        List<ModelForecast> forecasts, ParameterDef? param,
        DateTimeOffset now, DateTimeOffset cutoff)
    {
        if (param is null || forecasts.Count == 0)
        {
            return null;
        }

        double sum = 0;
        var count = 0;
        foreach (var forecast in forecasts)
        {
            foreach (var point in forecast.Hourly.Points)
            {
                if (point.ValidAt < now || point.ValidAt > cutoff)
                {
                    continue;
                }

                var val = point.Get(param);
                if (val is not { } v)
                {
                    continue;
                }

                sum += v;
                count++;
            }
        }
        return count > 0 ? sum / count : null;
    }
}
