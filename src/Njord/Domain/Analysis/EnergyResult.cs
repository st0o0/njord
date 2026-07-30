using Newtonsoft.Json;
using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record CopOptimalEntry(
    [property: JsonProperty("hoursFromNow")] int HoursFromNow,
    [property: JsonProperty("cop")] double Cop);

public sealed record EnergyResult(
    [property: JsonProperty("location")] string Location,
    [property: JsonProperty("heatingDemand")] int HeatingDemand,
    [property: JsonProperty("copEstimate")] double? CopEstimate,
    [property: JsonProperty("copOptimal")] IReadOnlyList<CopOptimalEntry> CopOptimal,
    [property: JsonProperty("shading")] int Shading,
    [property: JsonProperty("batteryStrategy")] string BatteryStrategy,
    [property: JsonProperty("nightCooling")] int NightCooling,
    [property: JsonProperty("heatingDemandMax")] int HeatingDemandMax = 0,
    [property: JsonProperty("copEstimateMin")] double? CopEstimateMin = null,
    [property: JsonProperty("copOptimalConservative")] IReadOnlyList<int>? CopOptimalConservative = null)
{
    public static EnergyResult Compute(
        ConsensusSnapshot consensus,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        EnergyOptions options)
    {
        var tempParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Temperature2m));
        var windParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.WindSpeed10m));
        var cloudParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.CloudCover));
        var radiationParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.ShortwaveRadiation));
        var isDayParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.IsDay));
        var humidityParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.RelativeHumidity2m));
        var rainProbParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.PrecipitationProbability));

        var cutoffHour = Math.Min(consensus.Hourly.CutoffHour, 24);

        var meanTemp = Mean24hFromConsensus(tempParam, cutoffHour);
        var meanWind = Mean24hFromConsensus(windParam, cutoffHour);
        var meanCloud = Mean24hFromConsensus(cloudParam, cutoffHour);
        var meanRadiation = Mean24hFromConsensus(radiationParam, cutoffHour);
        var meanIsDay = Mean24hFromConsensus(isDayParam, cutoffHour);

        var heatingDemand = EnergyForecaster.HeatingDemand(meanTemp, meanWind, meanCloud, options.HeatingBaseTemp);
        var copEst = EnergyForecaster.CopEstimate(meanTemp, options.FlowTemp, options.CarnotEfficiency);

        var copOptimal = ComputeCopOptimalFromConsensus(
            tempParam, cutoffHour, options.FlowTemp, options.CarnotEfficiency, options.CopOptimalHours);

        var shading = EnergyForecaster.ShadingScore(meanRadiation, meanIsDay, meanTemp);

        var solarYield = IndexScorer.SolarYield(meanRadiation, meanCloud, meanTemp);
        var batteryStrategy = EnergyForecaster.BatteryStrategy(solarYield, meanIsDay);

        var nightCooling = ComputeNightCoolingFromConsensus(
            tempParam, humidityParam, windParam, rainProbParam,
            consensus.Hourly.CutoffHour, options.IndoorTemp, timeProvider);

        var (hdMax, copMin, copConservative) = ComputeEnvelope(
            tempParam, windParam, cloudParam, cutoffHour,
            heatingDemand, copEst, options);

        return new EnergyResult(consensus.Location, heatingDemand, copEst, copOptimal, shading, batteryStrategy, nightCooling,
            hdMax, copMin, copConservative);
    }

    private static IReadOnlyList<CopOptimalEntry> ComputeCopOptimalFromConsensus(
        ParameterConsensus? tempParam, int cutoffHour,
        double flowTemp, double carnotEfficiency, int count)
    {
        if (tempParam is null)
        {
            return [];
        }

        var candidates = new List<(int HoursFromNow, double Cop)>();

        for (var h = 0; h <= cutoffHour; h++)
        {
            var median = tempParam.ByHorizon.GetValueOrDefault($"h{h}")?.Median;
            var cop = EnergyForecaster.CopEstimate(median, flowTemp, carnotEfficiency);
            if (cop is not { } c)
            {
                continue;
            }

            candidates.Add((h, c));
        }

        return candidates
            .OrderByDescending(c => c.Cop)
            .Take(count)
            .Select(c => new CopOptimalEntry(c.HoursFromNow, c.Cop))
            .ToList();
    }

    private static int ComputeNightCoolingFromConsensus(
        ParameterConsensus? tempParam, ParameterConsensus? humidityParam,
        ParameterConsensus? windParam, ParameterConsensus? rainProbParam,
        int cutoffHour, double indoorTemp, TimeProvider timeProvider)
    {
        if (tempParam is null || humidityParam is null ||
            windParam is null || rainProbParam is null)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var bestScore = 0;
        var maxHour = Math.Min(cutoffHour, 48);

        for (var h = 0; h <= maxHour; h++)
        {
            var forecastHour = now.AddHours(h);
            var hourOfDay = forecastHour.Hour;
            if (hourOfDay is >= 6 and < 22)
            {
                continue;
            }

            var key = $"h{h}";
            var temp = tempParam.ByHorizon.GetValueOrDefault(key)?.Median;
            var humidity = humidityParam.ByHorizon.GetValueOrDefault(key)?.Median;
            var wind = windParam.ByHorizon.GetValueOrDefault(key)?.Median;
            var rainProb = rainProbParam.ByHorizon.GetValueOrDefault(key)?.Median;

            if (temp is not { } t || humidity is not { } rh || wind is not { } w || rainProb is not { } rp)
            {
                continue;
            }

            if (t >= indoorTemp || rp > 50)
            {
                continue;
            }

            var tempDelta = indoorTemp - t;
            var score = (int)Math.Round(Math.Clamp(
                0.5 * Math.Clamp(tempDelta / 10 * 100, 0, 100) +
                0.2 * Math.Clamp((100 - rh) / 100 * 100, 0, 100) +
                0.2 * Math.Clamp(w / 5 * 100, 0, 100) +
                0.1 * Math.Clamp((100 - rp) / 100 * 100, 0, 100),
                0, 100));

            bestScore = Math.Max(bestScore, score);
        }

        return bestScore;
    }

    private static (int HeatingDemandMax, double? CopEstimateMin, IReadOnlyList<int> CopOptimalConservative) ComputeEnvelope(
        ParameterConsensus? tempParam, ParameterConsensus? windParam, ParameterConsensus? cloudParam,
        int cutoffHour,
        int fallbackHd, double? fallbackCop,
        EnergyOptions options)
    {
        if (tempParam is null ||
            !tempParam.ByHorizon.Values.Any(hc => hc.AvailableModels.Count >= 2))
        {
            return (fallbackHd, fallbackCop, []);
        }

        var pessimisticTemp = MeanCiBound(tempParam, cutoffHour, lower: true);
        var pessimisticWind = MeanCiBound(windParam, cutoffHour, lower: false);
        var pessimisticCloud = MeanCiBound(cloudParam, cutoffHour, lower: false);

        var hdMax = EnergyForecaster.HeatingDemand(pessimisticTemp, pessimisticWind, pessimisticCloud, options.HeatingBaseTemp);
        var copMin = EnergyForecaster.CopEstimate(pessimisticTemp, options.FlowTemp, options.CarnotEfficiency);

        var conservative = new List<int>();
        for (var h = 0; h <= cutoffHour; h++)
        {
            var hc = tempParam.ByHorizon.GetValueOrDefault($"h{h}");
            if (hc is null)
            {
                continue;
            }

            var coldestTemp = hc.ConfidenceInterval?.Lower
                              ?? (hc.Median.HasValue && hc.Spread.HasValue
                                  ? hc.Median.Value - hc.Spread.Value / 2
                                  : hc.Median);

            var cop = EnergyForecaster.CopEstimate(coldestTemp, options.FlowTemp, options.CarnotEfficiency);
            if (cop is { } c && c >= options.FlowTemp / (options.FlowTemp - (coldestTemp ?? 0)) * options.CarnotEfficiency * 0.8)
            {
                conservative.Add(h);
            }
        }

        return (hdMax, copMin, conservative.OrderBy(h => h).ToList());
    }

    private static double? Mean24hFromConsensus(ParameterConsensus? param, int cutoffHour)
    {
        if (param is null)
        {
            return null;
        }

        double sum = 0;
        var count = 0;

        for (var h = 0; h <= cutoffHour; h++)
        {
            var median = param.ByHorizon.GetValueOrDefault($"h{h}")?.Median;
            if (median is not { } v)
            {
                continue;
            }

            sum += v;
            count++;
        }

        return count > 0 ? sum / count : null;
    }

    private static double? MeanCiBound(ParameterConsensus? param, int cutoffHour, bool lower)
    {
        if (param is null)
        {
            return null;
        }

        double sum = 0;
        var count = 0;

        for (var h = 0; h <= cutoffHour; h++)
        {
            var hc = param.ByHorizon.GetValueOrDefault($"h{h}");
            if (hc is null)
            {
                continue;
            }

            double? val = null;
            if (hc.ConfidenceInterval is { } ci)
            {
                val = lower ? ci.Lower : ci.Upper;
            }
            else if (hc.Median.HasValue && hc.Spread.HasValue)
            {
                val = lower ? hc.Median.Value - hc.Spread.Value / 2 : hc.Median.Value + hc.Spread.Value / 2;
            }
            else
            {
                val = hc.Median;
            }

            if (val is not { } v)
            {
                continue;
            }

            sum += v;
            count++;
        }

        return count > 0 ? sum / count : null;
    }

    private static ParameterConsensus? FindParam(IReadOnlyList<ParameterConsensus> parameters, ParameterDef? param)
        => param is null ? null : parameters.FirstOrDefault(p => p.Parameter == param);
}
