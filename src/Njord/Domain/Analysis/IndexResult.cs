using Newtonsoft.Json;
using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record ScoreEnvelope(
    [property: JsonProperty("min")] int Min,
    [property: JsonProperty("max")] int Max,
    [property: JsonProperty("confidence")] double Confidence);

public sealed record FrostProtectionInfo(
    [property: JsonProperty("hoursUntilFrost")] int HoursUntilFrost,
    [property: JsonProperty("confidence")] double Confidence);

public sealed record VpdInfo(
    [property: JsonProperty("category")] string Category,
    [property: JsonProperty("vpd")] double Vpd);

public sealed record IndexResult(
    [property: JsonProperty("location")] string Location,
    [property: JsonProperty("laundry")] int Laundry,
    [property: JsonProperty("outdoor")] int Outdoor,
    [property: JsonProperty("running")] int Running,
    [property: JsonProperty("cycling")] int Cycling,
    [property: JsonProperty("bbq")] int Bbq,
    [property: JsonProperty("irrigation")] int Irrigation,
    [property: JsonProperty("hdd")] double Hdd,
    [property: JsonProperty("cdd")] double Cdd,
    [property: JsonProperty("solar")] int Solar,
    [property: JsonProperty("ventilation")] int Ventilation,
    [property: JsonProperty("frostProtection")] FrostProtectionInfo? FrostProtection,
    [property: JsonProperty("vpd")] VpdInfo? Vpd,
    [property: JsonProperty("laundryEnvelope")] ScoreEnvelope? LaundryEnvelope = null,
    [property: JsonProperty("outdoorEnvelope")] ScoreEnvelope? OutdoorEnvelope = null,
    [property: JsonProperty("runningEnvelope")] ScoreEnvelope? RunningEnvelope = null,
    [property: JsonProperty("cyclingEnvelope")] ScoreEnvelope? CyclingEnvelope = null,
    [property: JsonProperty("bbqEnvelope")] ScoreEnvelope? BbqEnvelope = null,
    [property: JsonProperty("irrigationEnvelope")] ScoreEnvelope? IrrigationEnvelope = null,
    [property: JsonProperty("solarEnvelope")] ScoreEnvelope? SolarEnvelope = null,
    [property: JsonProperty("ventilationEnvelope")] ScoreEnvelope? VentilationEnvelope = null)
{
    public static IndexResult Compute(
        ConsensusSnapshot consensus,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IndexOptions options)
    {
        var tempParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Temperature2m));
        var humidityParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.RelativeHumidity2m));
        var windParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.WindSpeed10m));
        var precipProbParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.PrecipitationProbability));
        var cloudParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.CloudCover));
        var radiationParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.ShortwaveRadiation));
        var etParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Et0FaoEvapotranspiration));
        var sunshineDurationParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.SunshineDuration));
        var isDayParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.IsDay));

        var cutoffHour = Math.Min(consensus.Hourly.CutoffHour, 24);

        var meanTemp = Mean24hFromConsensus(tempParam, cutoffHour);
        var meanHumidity = Mean24hFromConsensus(humidityParam, cutoffHour);
        var meanWind = Mean24hFromConsensus(windParam, cutoffHour);
        var meanRainProb = Mean24hFromConsensus(precipProbParam, cutoffHour);
        var meanCloud = Mean24hFromConsensus(cloudParam, cutoffHour);
        var meanRadiation = Mean24hFromConsensus(radiationParam, cutoffHour);
        var meanEt = Mean24hFromConsensus(etParam, cutoffHour);

        double? sunshinePct = null;
        if (sunshineDurationParam is not null && isDayParam is not null)
        {
            var totalSunshine = 0.0;
            var totalDaylight = 0.0;
            for (var h = 0; h <= cutoffHour; h++)
            {
                var key = $"h{h}";
                var sunMedian = sunshineDurationParam.ByHorizon.GetValueOrDefault(key)?.Median;
                var dayMedian = isDayParam.ByHorizon.GetValueOrDefault(key)?.Median;
                if (sunMedian.HasValue && dayMedian is > 0.5)
                {
                    totalSunshine += sunMedian.Value;
                    totalDaylight += 3600.0;
                }
            }
            if (totalDaylight > 0)
            {
                sunshinePct = Math.Round(totalSunshine / totalDaylight * 100, 1);
            }
        }

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

        var frost = ComputeFrostFromConsensus(
            FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Temperature2m)),
            consensus.Hourly.CutoffHour);

        var vpd = IndexScorer.VpdCategory(meanTemp, meanHumidity);

        var envelopes = ComputeEnvelopes(
            consensus, parameters, cutoffHour, options);

        return new IndexResult(consensus.Location, laundry, outdoor, running, cycling, bbq, irrigation,
            hdd, cdd, solar, ventilation, frost, vpd,
            envelopes.Laundry, envelopes.Outdoor, envelopes.Running, envelopes.Cycling,
            envelopes.Bbq, envelopes.Irrigation, envelopes.Solar, envelopes.Ventilation);
    }

    private record struct EnvelopeSet(
        ScoreEnvelope? Laundry, ScoreEnvelope? Outdoor, ScoreEnvelope? Running, ScoreEnvelope? Cycling,
        ScoreEnvelope? Bbq, ScoreEnvelope? Irrigation, ScoreEnvelope? Solar, ScoreEnvelope? Ventilation);

    private static EnvelopeSet ComputeEnvelopes(
        ConsensusSnapshot consensus, ResolvedParameterSet parameters,
        int cutoffHour, IndexOptions options)
    {
        var tempParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Temperature2m));
        var humidityParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.RelativeHumidity2m));
        var windParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.WindSpeed10m));
        var precipProbParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.PrecipitationProbability));
        var cloudParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.CloudCover));
        var radiationParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.ShortwaveRadiation));
        var etParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Et0FaoEvapotranspiration));

        var hasEnoughData = tempParam is not null &&
            tempParam.ByHorizon.Values.Any(hc => hc.AvailableModels.Count >= 2);

        if (!hasEnoughData)
        {
            return default;
        }

        var pessimisticMeans = ComputePessimisticMeans(
            tempParam, humidityParam, windParam, precipProbParam,
            cloudParam, radiationParam, etParam, cutoffHour);
        var optimisticMeans = ComputeOptimisticMeans(
            tempParam, humidityParam, windParam, precipProbParam,
            cloudParam, radiationParam, etParam, cutoffHour);

        var pessScores = ComputeScoresFromMeans(pessimisticMeans, options);
        var optScores = ComputeScoresFromMeans(optimisticMeans, options);

        var avgAgreement = ComputeAverageAgreement(
            tempParam, humidityParam, windParam, precipProbParam,
            cloudParam, radiationParam, cutoffHour);

        return new EnvelopeSet(
            BuildEnvelopeFromBounds(pessScores.Laundry, optScores.Laundry, avgAgreement),
            BuildEnvelopeFromBounds(pessScores.Outdoor, optScores.Outdoor, avgAgreement),
            BuildEnvelopeFromBounds(pessScores.Running, optScores.Running, avgAgreement),
            BuildEnvelopeFromBounds(pessScores.Cycling, optScores.Cycling, avgAgreement),
            BuildEnvelopeFromBounds(pessScores.Bbq, optScores.Bbq, avgAgreement),
            BuildEnvelopeFromBounds(pessScores.Irrigation, optScores.Irrigation, avgAgreement),
            BuildEnvelopeFromBounds(pessScores.Solar, optScores.Solar, avgAgreement),
            BuildEnvelopeFromBounds(pessScores.Ventilation, optScores.Ventilation, avgAgreement));
    }

    private record struct MeanSet(
        double? Temp, double? Humidity, double? Wind, double? RainProb,
        double? Cloud, double? Radiation, double? Et);

    private record struct ScoreSet(
        int Laundry, int Outdoor, int Running, int Cycling,
        int Bbq, int Irrigation, int Solar, int Ventilation);

    private static MeanSet ComputePessimisticMeans(
        ParameterConsensus? tempParam, ParameterConsensus? humidityParam,
        ParameterConsensus? windParam, ParameterConsensus? precipProbParam,
        ParameterConsensus? cloudParam, ParameterConsensus? radiationParam,
        ParameterConsensus? etParam, int cutoffHour)
    {
        return new MeanSet(
            MeanCiBound(tempParam, cutoffHour, lower: true),
            MeanCiBound(humidityParam, cutoffHour, lower: false),
            MeanCiBound(windParam, cutoffHour, lower: false),
            MeanCiBound(precipProbParam, cutoffHour, lower: false),
            MeanCiBound(cloudParam, cutoffHour, lower: false),
            MeanCiBound(radiationParam, cutoffHour, lower: true),
            MeanCiBound(etParam, cutoffHour, lower: true));
    }

    private static MeanSet ComputeOptimisticMeans(
        ParameterConsensus? tempParam, ParameterConsensus? humidityParam,
        ParameterConsensus? windParam, ParameterConsensus? precipProbParam,
        ParameterConsensus? cloudParam, ParameterConsensus? radiationParam,
        ParameterConsensus? etParam, int cutoffHour)
    {
        return new MeanSet(
            MeanCiBound(tempParam, cutoffHour, lower: false),
            MeanCiBound(humidityParam, cutoffHour, lower: true),
            MeanCiBound(windParam, cutoffHour, lower: true),
            MeanCiBound(precipProbParam, cutoffHour, lower: true),
            MeanCiBound(cloudParam, cutoffHour, lower: true),
            MeanCiBound(radiationParam, cutoffHour, lower: false),
            MeanCiBound(etParam, cutoffHour, lower: false));
    }

    private static ScoreSet ComputeScoresFromMeans(MeanSet means, IndexOptions options)
    {
        return new ScoreSet(
            IndexScorer.LaundryDrying(means.Temp, means.Humidity, means.Wind, means.RainProb, null),
            IndexScorer.OutdoorScore(means.Temp, means.RainProb, means.Wind, means.Cloud),
            IndexScorer.RunningComfort(means.Temp, means.Humidity, means.Wind, means.RainProb),
            IndexScorer.CyclingComfort(means.Temp, means.Humidity, means.Wind, means.RainProb),
            IndexScorer.BbqWeather(means.Temp, means.Humidity, means.Wind, means.RainProb),
            IndexScorer.IrrigationNeed(means.RainProb, means.Temp, means.Humidity, means.Et),
            IndexScorer.SolarYield(means.Radiation, means.Cloud, means.Temp),
            IndexScorer.Ventilation(means.Temp, options.IndoorTemp, means.Humidity, means.Wind, means.RainProb));
    }

    private static double ComputeAverageAgreement(
        ParameterConsensus? tempParam, ParameterConsensus? humidityParam,
        ParameterConsensus? windParam, ParameterConsensus? precipProbParam,
        ParameterConsensus? cloudParam, ParameterConsensus? radiationParam,
        int cutoffHour)
    {
        var allParams = new[] { tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam };
        double totalAgreement = 0;
        var count = 0;

        foreach (var param in allParams)
        {
            if (param is null)
            {
                continue;
            }

            for (var h = 0; h <= cutoffHour; h++)
            {
                var agreement = param.ByHorizon.GetValueOrDefault($"h{h}")?.Agreement;
                if (agreement.HasValue)
                {
                    totalAgreement += agreement.Value;
                    count++;
                }
            }
        }

        return count > 0 ? Math.Round(totalAgreement / count, 3) : 0;
    }

    private static ScoreEnvelope BuildEnvelopeFromBounds(int score1, int score2, double confidence)
    {
        var min = Math.Min(score1, score2);
        var max = Math.Max(score1, score2);
        return new ScoreEnvelope(min, max, confidence);
    }

    internal static ScoreEnvelope BuildEnvelope(List<int> scores)
    {
        var min = scores.Min();
        var max = scores.Max();
        var sorted = scores.OrderBy(s => s).ToList();
        var median = sorted[sorted.Count / 2];
        var tolerance = Math.Max(median * 0.1, 5.0);
        var agreeing = scores.Count(s => Math.Abs(s - median) <= tolerance);
        var confidence = (double)agreeing / scores.Count;
        return new ScoreEnvelope(min, max, Math.Round(confidence, 3));
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

    private static FrostProtectionInfo? ComputeFrostFromConsensus(
        ParameterConsensus? tempParam, int cutoffHour)
    {
        if (tempParam is null)
        {
            return null;
        }

        var maxHour = Math.Min(cutoffHour, 48);
        int? firstFrostHours = null;
        var hoursWithFrost = 0;
        var totalHours = 0;

        for (var h = 0; h <= maxHour; h++)
        {
            var hc = tempParam.ByHorizon.GetValueOrDefault($"h{h}");
            if (hc?.Median is not { } median)
            {
                continue;
            }

            totalHours++;
            if (median <= 0)
            {
                firstFrostHours ??= h;
                hoursWithFrost++;
            }
        }

        if (firstFrostHours is null)
        {
            return null;
        }

        var h3Hc = tempParam.ByHorizon.GetValueOrDefault("h3");
        var confidence = h3Hc?.Agreement ?? (totalHours > 0 ? 1.0 : 0.0);
        return new FrostProtectionInfo(firstFrostHours.Value, Math.Round(confidence, 2));
    }

    private static ParameterConsensus? FindParam(IReadOnlyList<ParameterConsensus> parameters, ParameterDef? param)
        => param is null ? null : parameters.FirstOrDefault(p => p.Parameter == param);
}
