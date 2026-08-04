using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed class IndexComputer(ResolvedParameterSet parameters, TimeProvider timeProvider)
{
    public IndexResult Compute(
        ConsensusSnapshot consensus,
        IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> resolvedPreferences)
    {
        ResolvedPreferences PrefsFor(string score) =>
            resolvedPreferences.TryGetValue((consensus.Location, score), out var p)
                ? p
                : ResolvedPreferences.Default;

        var slices = TimeSliceAggregator.AggregateDaySlices(consensus, parameters, timeProvider);
        var days = new List<DayScoreSet>();

        foreach (var slice in slices)
        {
            var dayMeans = slice.DayMeans;
            var nightMeans = slice.NightMeans;
            var fullMeans = slice.FullDayMeans;

            var temp = parameters.Get(ParameterRegistry.Temperature2m);
            var humidity = parameters.Get(ParameterRegistry.RelativeHumidity2m);
            var wind = parameters.Get(ParameterRegistry.WindSpeed10m);
            var rainProb = parameters.Get(ParameterRegistry.PrecipitationProbability);
            var cloud = parameters.Get(ParameterRegistry.CloudCover);
            var radiation = parameters.Get(ParameterRegistry.ShortwaveRadiation);
            var et = parameters.Get(ParameterRegistry.Et0FaoEvapotranspiration);
            var sunshineDuration = parameters.Get(ParameterRegistry.SunshineDuration);

            double? sunshinePct = ComputeSunshinePct(consensus, slice);

            var outdoor = IndexScorer.OutdoorScore(
                Get(dayMeans, temp), Get(dayMeans, humidity),
                Get(dayMeans, rainProb), Get(dayMeans, wind),
                Get(dayMeans, cloud), PrefsFor("Outdoor"));

            var running = IndexScorer.RunningComfort(
                Get(dayMeans, temp), Get(dayMeans, humidity),
                Get(dayMeans, wind), Get(dayMeans, rainProb), PrefsFor("Running"));

            var cycling = IndexScorer.CyclingComfort(
                Get(dayMeans, temp), Get(dayMeans, humidity),
                Get(dayMeans, wind), Get(dayMeans, rainProb), PrefsFor("Cycling"));

            var bbq = IndexScorer.BbqWeather(
                Get(dayMeans, temp), Get(dayMeans, humidity),
                Get(dayMeans, wind), Get(dayMeans, rainProb), PrefsFor("Bbq"));

            var solar = IndexScorer.SolarYield(
                Get(dayMeans, radiation), Get(dayMeans, cloud),
                Get(dayMeans, temp), PrefsFor("Solar"));

            var laundry = IndexScorer.LaundryDrying(
                Get(fullMeans, temp), Get(fullMeans, humidity),
                Get(fullMeans, wind), Get(fullMeans, rainProb),
                sunshinePct, PrefsFor("Laundry"));

            var irrigation = IndexScorer.IrrigationNeed(
                Get(fullMeans, rainProb), Get(fullMeans, temp),
                Get(fullMeans, humidity), Get(fullMeans, et), PrefsFor("Irrigation"));

            var nightVent = IndexScorer.NightVentilation(
                Get(nightMeans, temp), Get(nightMeans, humidity),
                Get(nightMeans, wind), Get(nightMeans, rainProb), PrefsFor("NightVentilation"));

            var hoursIncluded = Math.Max(slice.DaylightHoursCount, slice.NighttimeHoursCount);

            var envelopes = ComputeDayEnvelopes(consensus, slice, resolvedPreferences);

            days.Add(new DayScoreSet(
                slice.DayOffset, laundry, outdoor, running, cycling, bbq, irrigation, solar,
                nightVent, hoursIncluded,
                envelopes.Laundry, envelopes.Outdoor, envelopes.Running, envelopes.Cycling,
                envelopes.Bbq, envelopes.Irrigation, envelopes.Solar, envelopes.NightVentilation));
        }

        var frost = ComputeFrostFromConsensus(
            FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Temperature2m)),
            consensus.Hourly.CutoffHour);

        var d0FullMeans = slices.Count > 0 ? slices[0].FullDayMeans : new Dictionary<ParameterDef, double?>();
        var vpd = IndexScorer.VpdCategory(
            Get(d0FullMeans, parameters.Get(ParameterRegistry.Temperature2m)),
            Get(d0FullMeans, parameters.Get(ParameterRegistry.RelativeHumidity2m)));

        return new IndexResult(consensus.Location, days, frost, vpd);
    }

    private static double? Get(IReadOnlyDictionary<ParameterDef, double?> means, ParameterDef? param)
        => param is not null && means.TryGetValue(param, out var val) ? val : null;

    private double? ComputeSunshinePct(
        ConsensusSnapshot consensus, DaySlice slice)
    {
        var sunshineDurationParam = FindParam(consensus.Hourly.Parameters,
            parameters.Get(ParameterRegistry.SunshineDuration));
        var isDayParam = FindParam(consensus.Hourly.Parameters,
            parameters.Get(ParameterRegistry.IsDay));

        if (sunshineDurationParam is null || isDayParam is null)
            return null;

        var now = timeProvider.GetUtcNow();
        var todayMidnight = now.UtcDateTime.Date;
        var cutoffHour = consensus.Hourly.CutoffHour;

        var totalSunshine = 0.0;
        var totalDaylight = 0.0;

        for (var h = 0; h <= cutoffHour; h++)
        {
            var absoluteTime = now.AddHours(h).UtcDateTime;
            var dayOffset = (int)Math.Floor((absoluteTime - todayMidnight).TotalDays);
            if (dayOffset != slice.DayOffset)
                continue;

            var key = $"h{h}";
            var sunMedian = sunshineDurationParam.ByHorizon.GetValueOrDefault(key)?.Median;
            var dayMedian = isDayParam.ByHorizon.GetValueOrDefault(key)?.Median;
            if (sunMedian.HasValue && dayMedian is > 0.5)
            {
                totalSunshine += sunMedian.Value;
                totalDaylight += 3600.0;
            }
        }

        return totalDaylight > 0 ? Math.Round(totalSunshine / totalDaylight * 100, 1) : null;
    }

    private record struct EnvelopeSet(
        ScoreEnvelope? Laundry, ScoreEnvelope? Outdoor, ScoreEnvelope? Running, ScoreEnvelope? Cycling,
        ScoreEnvelope? Bbq, ScoreEnvelope? Irrigation, ScoreEnvelope? Solar, ScoreEnvelope? NightVentilation);

    private EnvelopeSet ComputeDayEnvelopes(
        ConsensusSnapshot consensus, DaySlice slice,
        IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> resolvedPreferences)
    {
        var tempParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Temperature2m));
        if (tempParam is null || !tempParam.ByHorizon.Values.Any(hc => hc.AvailableModels.Count >= 2))
            return default;

        var now = timeProvider.GetUtcNow();
        var todayMidnight = now.UtcDateTime.Date;
        var cutoffHour = consensus.Hourly.CutoffHour;
        var isDayParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.IsDay));

        var dayHours = new List<int>();
        var nightHours = new List<int>();
        var allHours = new List<int>();

        for (var h = 0; h <= cutoffHour; h++)
        {
            var absoluteTime = now.AddHours(h).UtcDateTime;
            var dayOffset = (int)Math.Floor((absoluteTime - todayMidnight).TotalDays);
            if (dayOffset != slice.DayOffset)
                continue;

            allHours.Add(h);
            var isDayValue = isDayParam?.ByHorizon.GetValueOrDefault($"h{h}")?.Median;
            if (isDayParam is null || isDayValue is > 0.5)
                dayHours.Add(h);
            else
                nightHours.Add(h);
        }

        var humidityParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.RelativeHumidity2m));
        var windParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.WindSpeed10m));
        var precipProbParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.PrecipitationProbability));
        var cloudParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.CloudCover));
        var radiationParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.ShortwaveRadiation));
        var etParam = FindParam(consensus.Hourly.Parameters, parameters.Get(ParameterRegistry.Et0FaoEvapotranspiration));

        var pessDayMeans = ComputeCiBoundMeans(
            tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam, etParam,
            dayHours, pessimistic: true);
        var optDayMeans = ComputeCiBoundMeans(
            tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam, etParam,
            dayHours, pessimistic: false);
        var pessNightMeans = ComputeCiBoundMeans(
            tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam, etParam,
            nightHours, pessimistic: true);
        var optNightMeans = ComputeCiBoundMeans(
            tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam, etParam,
            nightHours, pessimistic: false);
        var pessFullMeans = ComputeCiBoundMeans(
            tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam, etParam,
            allHours, pessimistic: true);
        var optFullMeans = ComputeCiBoundMeans(
            tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam, etParam,
            allHours, pessimistic: false);

        ResolvedPreferences PrefsFor(string score) =>
            resolvedPreferences.TryGetValue((consensus.Location, score), out var p)
                ? p
                : ResolvedPreferences.Default;

        var avgAgreement = ComputeAverageAgreement(
            tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam, allHours);

        var pessOutdoor = IndexScorer.OutdoorScore(pessDayMeans.Temp, pessDayMeans.Humidity, pessDayMeans.RainProb, pessDayMeans.Wind, pessDayMeans.Cloud, PrefsFor("Outdoor"));
        var optOutdoor = IndexScorer.OutdoorScore(optDayMeans.Temp, optDayMeans.Humidity, optDayMeans.RainProb, optDayMeans.Wind, optDayMeans.Cloud, PrefsFor("Outdoor"));
        var pessRunning = IndexScorer.RunningComfort(pessDayMeans.Temp, pessDayMeans.Humidity, pessDayMeans.Wind, pessDayMeans.RainProb, PrefsFor("Running"));
        var optRunning = IndexScorer.RunningComfort(optDayMeans.Temp, optDayMeans.Humidity, optDayMeans.Wind, optDayMeans.RainProb, PrefsFor("Running"));
        var pessCycling = IndexScorer.CyclingComfort(pessDayMeans.Temp, pessDayMeans.Humidity, pessDayMeans.Wind, pessDayMeans.RainProb, PrefsFor("Cycling"));
        var optCycling = IndexScorer.CyclingComfort(optDayMeans.Temp, optDayMeans.Humidity, optDayMeans.Wind, optDayMeans.RainProb, PrefsFor("Cycling"));
        var pessBbq = IndexScorer.BbqWeather(pessDayMeans.Temp, pessDayMeans.Humidity, pessDayMeans.Wind, pessDayMeans.RainProb, PrefsFor("Bbq"));
        var optBbq = IndexScorer.BbqWeather(optDayMeans.Temp, optDayMeans.Humidity, optDayMeans.Wind, optDayMeans.RainProb, PrefsFor("Bbq"));
        var pessSolar = IndexScorer.SolarYield(pessDayMeans.Radiation, pessDayMeans.Cloud, pessDayMeans.Temp, PrefsFor("Solar"));
        var optSolar = IndexScorer.SolarYield(optDayMeans.Radiation, optDayMeans.Cloud, optDayMeans.Temp, PrefsFor("Solar"));

        var pessLaundry = IndexScorer.LaundryDrying(pessFullMeans.Temp, pessFullMeans.Humidity, pessFullMeans.Wind, pessFullMeans.RainProb, null, PrefsFor("Laundry"));
        var optLaundry = IndexScorer.LaundryDrying(optFullMeans.Temp, optFullMeans.Humidity, optFullMeans.Wind, optFullMeans.RainProb, null, PrefsFor("Laundry"));
        var pessIrrigation = IndexScorer.IrrigationNeed(pessFullMeans.RainProb, pessFullMeans.Temp, pessFullMeans.Humidity, pessFullMeans.Et, PrefsFor("Irrigation"));
        var optIrrigation = IndexScorer.IrrigationNeed(optFullMeans.RainProb, optFullMeans.Temp, optFullMeans.Humidity, optFullMeans.Et, PrefsFor("Irrigation"));

        var pessNightVent = IndexScorer.NightVentilation(pessNightMeans.Temp, pessNightMeans.Humidity, pessNightMeans.Wind, pessNightMeans.RainProb, PrefsFor("NightVentilation"));
        var optNightVent = IndexScorer.NightVentilation(optNightMeans.Temp, optNightMeans.Humidity, optNightMeans.Wind, optNightMeans.RainProb, PrefsFor("NightVentilation"));

        return new EnvelopeSet(
            BuildEnvelopeFromBounds(pessLaundry, optLaundry, avgAgreement),
            BuildEnvelopeFromBounds(pessOutdoor, optOutdoor, avgAgreement),
            BuildEnvelopeFromBounds(pessRunning, optRunning, avgAgreement),
            BuildEnvelopeFromBounds(pessCycling, optCycling, avgAgreement),
            BuildEnvelopeFromBounds(pessBbq, optBbq, avgAgreement),
            BuildEnvelopeFromBounds(pessIrrigation, optIrrigation, avgAgreement),
            BuildEnvelopeFromBounds(pessSolar, optSolar, avgAgreement),
            BuildEnvelopeFromBounds(pessNightVent, optNightVent, avgAgreement));
    }

    private record struct MeanSet(
        double? Temp, double? Humidity, double? Wind, double? RainProb,
        double? Cloud, double? Radiation, double? Et);

    private static MeanSet ComputeCiBoundMeans(
        ParameterConsensus? tempParam, ParameterConsensus? humidityParam,
        ParameterConsensus? windParam, ParameterConsensus? precipProbParam,
        ParameterConsensus? cloudParam, ParameterConsensus? radiationParam,
        ParameterConsensus? etParam,
        List<int> hours, bool pessimistic)
    {
        return new MeanSet(
            MeanCiBound(tempParam, hours, lower: pessimistic),
            MeanCiBound(humidityParam, hours, lower: !pessimistic),
            MeanCiBound(windParam, hours, lower: !pessimistic),
            MeanCiBound(precipProbParam, hours, lower: !pessimistic),
            MeanCiBound(cloudParam, hours, lower: !pessimistic),
            MeanCiBound(radiationParam, hours, lower: pessimistic),
            MeanCiBound(etParam, hours, lower: pessimistic));
    }

    private static double? MeanCiBound(ParameterConsensus? param, List<int> hours, bool lower)
    {
        if (param is null || hours.Count == 0)
            return null;

        double sum = 0;
        var count = 0;

        foreach (var h in hours)
        {
            var hc = param.ByHorizon.GetValueOrDefault($"h{h}");
            if (hc is null)
                continue;

            double? val = null;
            if (hc.ConfidenceInterval is { } ci)
                val = lower ? ci.Lower : ci.Upper;
            else if (hc.Median.HasValue && hc.Spread.HasValue)
                val = lower ? hc.Median.Value - hc.Spread.Value / 2 : hc.Median.Value + hc.Spread.Value / 2;
            else
                val = hc.Median;

            if (val is { } v)
            {
                sum += v;
                count++;
            }
        }

        return count > 0 ? sum / count : null;
    }

    private static double ComputeAverageAgreement(
        ParameterConsensus? tempParam, ParameterConsensus? humidityParam,
        ParameterConsensus? windParam, ParameterConsensus? precipProbParam,
        ParameterConsensus? cloudParam, ParameterConsensus? radiationParam,
        List<int> hours)
    {
        var allParams = new[] { tempParam, humidityParam, windParam, precipProbParam, cloudParam, radiationParam };
        double totalAgreement = 0;
        var count = 0;

        foreach (var param in allParams)
        {
            if (param is null)
                continue;

            foreach (var h in hours)
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

    private static FrostProtectionInfo? ComputeFrostFromConsensus(
        ParameterConsensus? tempParam, int cutoffHour)
    {
        if (tempParam is null)
            return null;

        var maxHour = Math.Min(cutoffHour, 48);
        int? firstFrostHours = null;
        var hoursWithFrost = 0;
        var totalHours = 0;

        for (var h = 0; h <= maxHour; h++)
        {
            var hc = tempParam.ByHorizon.GetValueOrDefault($"h{h}");
            if (hc?.Median is not { } median)
                continue;

            totalHours++;
            if (median <= 0)
            {
                firstFrostHours ??= h;
                hoursWithFrost++;
            }
        }

        if (firstFrostHours is null)
            return null;

        var h3Hc = tempParam.ByHorizon.GetValueOrDefault("h3");
        var confidence = h3Hc?.Agreement ?? (totalHours > 0 ? 1.0 : 0.0);
        return new FrostProtectionInfo(firstFrostHours.Value, Math.Round(confidence, 2));
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

    private static ParameterConsensus? FindParam(IReadOnlyList<ParameterConsensus> parameters, ParameterDef? param)
        => param is null ? null : parameters.FirstOrDefault(p => p.Parameter == param);
}
