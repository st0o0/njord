using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record ScoreEnvelope(int Min, int Max, double Confidence);

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
    (string Category, double Vpd)? Vpd,
    ScoreEnvelope? LaundryEnvelope = null,
    ScoreEnvelope? OutdoorEnvelope = null,
    ScoreEnvelope? RunningEnvelope = null,
    ScoreEnvelope? CyclingEnvelope = null,
    ScoreEnvelope? BbqEnvelope = null,
    ScoreEnvelope? IrrigationEnvelope = null,
    ScoreEnvelope? SolarEnvelope = null,
    ScoreEnvelope? VentilationEnvelope = null)
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

        var tempParam = parameters.Get(ParameterRegistry.Temperature2m);
        var humidityParam = parameters.Get(ParameterRegistry.RelativeHumidity2m);
        var windParam = parameters.Get(ParameterRegistry.WindSpeed10m);
        var precipProbParam = parameters.Get(ParameterRegistry.PrecipitationProbability);
        var cloudParam = parameters.Get(ParameterRegistry.CloudCover);
        var radiationParam = parameters.Get(ParameterRegistry.ShortwaveRadiation);
        var etParam = parameters.Get(ParameterRegistry.Et0FaoEvapotranspiration);
        var sunshineDurationParam = parameters.Get(ParameterRegistry.SunshineDuration);
        var isDayParam = parameters.Get(ParameterRegistry.IsDay);

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
        {
            sunshinePct = DerivedComputer.SunshinePercent(forecasts[0].Hourly, sunshineDurationParam, isDayParam, now);
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

        var frostSeries = forecasts.Select(f => f.Hourly).ToList();
        var frost = tempParam is not null
            ? IndexScorer.FrostProtection(frostSeries, tempParam, now)
            : null;

        var vpd = IndexScorer.VpdCategory(meanTemp, meanHumidity);

        var envelopes = ComputeEnvelopes(forecasts, parameters, now, cutoff, options);

        return new IndexResult(location, laundry, outdoor, running, cycling, bbq, irrigation,
            hdd, cdd, solar, ventilation, frost, vpd,
            envelopes.Laundry, envelopes.Outdoor, envelopes.Running, envelopes.Cycling,
            envelopes.Bbq, envelopes.Irrigation, envelopes.Solar, envelopes.Ventilation);
    }

    private record struct PerModelScores(
        int Laundry, int Outdoor, int Running, int Cycling,
        int Bbq, int Irrigation, int Solar, int Ventilation);

    private record struct EnvelopeSet(
        ScoreEnvelope? Laundry, ScoreEnvelope? Outdoor, ScoreEnvelope? Running, ScoreEnvelope? Cycling,
        ScoreEnvelope? Bbq, ScoreEnvelope? Irrigation, ScoreEnvelope? Solar, ScoreEnvelope? Ventilation);

    private static EnvelopeSet ComputeEnvelopes(
        List<ModelForecast> forecasts, ResolvedParameterSet parameters,
        DateTimeOffset now, DateTimeOffset cutoff, IndexOptions options)
    {
        if (forecasts.Count < 2)
            return default;

        var perModel = new List<PerModelScores>();

        foreach (var forecast in forecasts)
        {
            var single = new List<ModelForecast> { forecast };
            var tempParam = parameters.Get(ParameterRegistry.Temperature2m);
            var humidityParam = parameters.Get(ParameterRegistry.RelativeHumidity2m);
            var windParam = parameters.Get(ParameterRegistry.WindSpeed10m);
            var precipProbParam = parameters.Get(ParameterRegistry.PrecipitationProbability);
            var cloudParam = parameters.Get(ParameterRegistry.CloudCover);
            var radiationParam = parameters.Get(ParameterRegistry.ShortwaveRadiation);
            var etParam = parameters.Get(ParameterRegistry.Et0FaoEvapotranspiration);

            var t = Mean24h(single, tempParam, now, cutoff);
            var h = Mean24h(single, humidityParam, now, cutoff);
            var w = Mean24h(single, windParam, now, cutoff);
            var rp = Mean24h(single, precipProbParam, now, cutoff);
            var cl = Mean24h(single, cloudParam, now, cutoff);
            var rad = Mean24h(single, radiationParam, now, cutoff);
            var et = Mean24h(single, etParam, now, cutoff);

            perModel.Add(new PerModelScores(
                IndexScorer.LaundryDrying(t, h, w, rp, null),
                IndexScorer.OutdoorScore(t, rp, w, cl),
                IndexScorer.RunningComfort(t, h, w, rp),
                IndexScorer.CyclingComfort(t, h, w, rp),
                IndexScorer.BbqWeather(t, h, w, rp),
                IndexScorer.IrrigationNeed(rp, t, h, et),
                IndexScorer.SolarYield(rad, cl, t),
                IndexScorer.Ventilation(t, options.IndoorTemp, h, w, rp)));
        }

        return new EnvelopeSet(
            BuildEnvelope(perModel.Select(m => m.Laundry).ToList()),
            BuildEnvelope(perModel.Select(m => m.Outdoor).ToList()),
            BuildEnvelope(perModel.Select(m => m.Running).ToList()),
            BuildEnvelope(perModel.Select(m => m.Cycling).ToList()),
            BuildEnvelope(perModel.Select(m => m.Bbq).ToList()),
            BuildEnvelope(perModel.Select(m => m.Irrigation).ToList()),
            BuildEnvelope(perModel.Select(m => m.Solar).ToList()),
            BuildEnvelope(perModel.Select(m => m.Ventilation).ToList()));
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
