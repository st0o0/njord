using Newtonsoft.Json;
using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public enum AlertType
{
    Frost,
    Heat,
    Storm,
    HeavyRain,
    Uv,
    Fog,
    Snow,
    PressureDrop,
    Thunderstorm,
}

public enum AlertSeverity
{
    None,
    Yellow,
    Orange,
    Red,
}

public sealed record Alert(
    [property: JsonProperty("type")] AlertType Type,
    [property: JsonProperty("severity")] AlertSeverity Severity,
    [property: JsonProperty("confidence")] double Confidence,
    [property: JsonProperty("attributes")] IReadOnlyDictionary<string, object?> Attributes,
    [property: JsonProperty("triggerValue")] double TriggerValue = 0.0,
    [property: JsonProperty("threshold")] double Threshold = 0.0,
    [property: JsonProperty("peakValue")] double? PeakValue = null,
    [property: JsonProperty("hoursUntil")] int? HoursUntil = null,
    [property: JsonProperty("durationHours")] int? DurationHours = null)
{
    public static Alert None(AlertType type) =>
        new(type, AlertSeverity.None, 0.0, new Dictionary<string, object?>());
}

public static class AlertTypeExtensions
{
    public static string ToTopicSegment(this AlertType type) => type switch
    {
        AlertType.Frost => "frost",
        AlertType.Heat => "heat",
        AlertType.Storm => "storm",
        AlertType.HeavyRain => "heavy-rain",
        AlertType.Uv => "uv",
        AlertType.Fog => "fog",
        AlertType.Snow => "snow",
        AlertType.PressureDrop => "pressure-drop",
        AlertType.Thunderstorm => "thunderstorm",
        _ => type.ToString().ToLowerInvariant(),
    };
}

public static class AlertEvaluator
{
    private static readonly ParameterDef Temperature = ParameterRegistry.Temperature2m;
    private static readonly ParameterDef ApparentTemp = ParameterRegistry.ApparentTemperature;
    private static readonly ParameterDef WindGusts = ParameterRegistry.WindGusts10m;
    private static readonly ParameterDef Precipitation = ParameterRegistry.Precipitation;
    private static readonly ParameterDef UvIndex = ParameterRegistry.UvIndex;
    private static readonly ParameterDef Dewpoint = ParameterRegistry.DewPoint2m;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.WindSpeed10m;
    private static readonly ParameterDef Humidity = ParameterRegistry.RelativeHumidity2m;
    private static readonly ParameterDef Snowfall = ParameterRegistry.Snowfall;
    private static readonly ParameterDef FreezingLevel = ParameterRegistry.FreezingLevelHeight;
    private static readonly ParameterDef PressureMsl = ParameterRegistry.PressureMsl;
    private static readonly ParameterDef Cape = ParameterRegistry.Cape;
    private static readonly ParameterDef? DailyPrecipSum = ParameterRegistry.GetByApiName("precipitation_sum");
    private static readonly ParameterDef? DailyUvMax = ParameterRegistry.GetByApiName("uv_index_max");
    private static readonly ParameterDef? DailySnowfallSum = ParameterRegistry.GetByApiName("snowfall_sum");

    public static AlertResult EvaluateAll(
        ConsensusSnapshot consensus, AlertThresholdOptions options, TimeProvider timeProvider)
    {
        var alerts = new List<Alert>
        {
            EvaluateFrost(consensus, options.FrostThreshold),
            EvaluateHeat(consensus, options.HeatThresholds),
            EvaluateStorm(consensus, options.StormGustThreshold),
            EvaluateHeavyRain(consensus, options.HeavyRainHourlyThreshold, options.HeavyRainDailyThreshold),
            EvaluateUv(consensus),
            EvaluateFog(consensus),
            EvaluateSnow(consensus),
            EvaluatePressureDrop(consensus, options.PressureDropThreshold),
            EvaluateThunderstorm(consensus, options.CapeThreshold, options.ThunderstormPrecipThreshold, options.ThunderstormGustThreshold),
        };
        return new AlertResult(consensus.Location, alerts);
    }

    public static Alert EvaluateFrost(
        ConsensusSnapshot consensus, double threshold)
    {
        if (Temperature is null)
        {
            return Alert.None(AlertType.Frost);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, Temperature);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.Frost);
        }

        var minMedian = double.MaxValue;
        int? earliestFrostHour = null;
        var frostAgreements = new List<double>();

        foreach (var (horizonKey, hc) in paramConsensus.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > 24)
            {
                continue;
            }

            if (hc.Median is not { } value)
            {
                continue;
            }

            if (value < minMedian)
            {
                minMedian = value;
            }

            if (value <= threshold)
            {
                if (earliestFrostHour is null || hours < earliestFrostHour)
                {
                    earliestFrostHour = hours;
                }

                if (hc.Agreement is { } agreement)
                {
                    frostAgreements.Add(agreement);
                }
            }
        }

        if (minMedian >= double.MaxValue)
        {
            return Alert.None(AlertType.Frost);
        }

        var confidence = frostAgreements.Count > 0
            ? Math.Round(frostAgreements.Average(), 3)
            : 0.0;

        var attrs = new Dictionary<string, object?>
        {
            ["expected_low"] = Math.Round(minMedian, 1),
            ["earliest_frost"] = earliestFrostHour.HasValue
                ? $"+{earliestFrostHour.Value}h"
                : null,
            ["models_agreeing"] = CountModelsAtPeak(paramConsensus, earliestFrostHour),
        };

        return new Alert(AlertType.Frost,
            confidence > 0 ? AlertSeverity.Yellow : AlertSeverity.None,
            confidence, attrs,
            TriggerValue: Math.Round(minMedian, 1),
            Threshold: threshold,
            HoursUntil: earliestFrostHour);
    }

    public static Alert EvaluateHeat(
        ConsensusSnapshot consensus, double[] thresholds)
    {
        if (ApparentTemp is null || thresholds.Length < 3)
        {
            return Alert.None(AlertType.Heat);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, ApparentTemp);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.Heat);
        }

        var maxMedian = double.MinValue;
        var peakMax = double.MinValue;

        foreach (var (horizonKey, hc) in paramConsensus.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > 24)
            {
                continue;
            }

            if (hc.Median is not { } value)
            {
                continue;
            }

            if (value > maxMedian)
            {
                maxMedian = value;
            }

            if (hc.ConfidenceInterval is { Upper: var upper } && upper > peakMax)
            {
                peakMax = upper;
            }
            else if (value > peakMax)
            {
                peakMax = value;
            }
        }

        if (maxMedian <= double.MinValue)
        {
            return Alert.None(AlertType.Heat);
        }

        var (severity, effectiveThreshold, agreementAtSeverity) = maxMedian switch
        {
            _ when maxMedian >= thresholds[2] => (AlertSeverity.Red, thresholds[2],
                AverageAgreementAbove(paramConsensus, thresholds[2], 24)),
            _ when maxMedian >= thresholds[1] => (AlertSeverity.Orange, thresholds[1],
                AverageAgreementAbove(paramConsensus, thresholds[1], 24)),
            _ when maxMedian >= thresholds[0] => (AlertSeverity.Yellow, thresholds[0],
                AverageAgreementAbove(paramConsensus, thresholds[0], 24)),
            _ => (AlertSeverity.None, 0.0, 0.0),
        };

        if (severity == AlertSeverity.None)
        {
            return Alert.None(AlertType.Heat);
        }

        var medianRounded = Math.Round(maxMedian, 1);
        var peakRounded = peakMax > double.MinValue ? Math.Round(peakMax, 1) : medianRounded;

        var attrs = new Dictionary<string, object?>
        {
            ["expected_max"] = medianRounded,
            ["models_agreeing"] = CountModelsAtMax(paramConsensus, 24),
        };

        return new Alert(AlertType.Heat, severity, Math.Round(agreementAtSeverity, 3), attrs,
            TriggerValue: medianRounded,
            Threshold: effectiveThreshold,
            PeakValue: peakRounded != medianRounded ? peakRounded : null);
    }

    public static Alert EvaluateStorm(
        ConsensusSnapshot consensus, double gustThreshold)
    {
        if (WindGusts is null)
        {
            return Alert.None(AlertType.Storm);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, WindGusts);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.Storm);
        }

        var maxMedian = double.MinValue;
        var peakMax = double.MinValue;
        var exceedAgreements = new List<double>();

        foreach (var (horizonKey, hc) in paramConsensus.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > 24)
            {
                continue;
            }

            if (hc.Median is not { } value)
            {
                continue;
            }

            if (value > maxMedian)
            {
                maxMedian = value;
            }

            if (hc.ConfidenceInterval is { Upper: var upper } && upper > peakMax)
            {
                peakMax = upper;
            }
            else if (value > peakMax)
            {
                peakMax = value;
            }

            if (value >= gustThreshold && hc.Agreement is { } agreement)
            {
                exceedAgreements.Add(agreement);
            }
        }

        if (maxMedian <= double.MinValue)
        {
            return Alert.None(AlertType.Storm);
        }

        var confidence = exceedAgreements.Count > 0
            ? Math.Round(exceedAgreements.Average(), 3)
            : 0.0;

        var medianRounded = Math.Round(maxMedian, 1);
        var peakRounded = peakMax > double.MinValue ? Math.Round(peakMax, 1) : medianRounded;

        var attrs = new Dictionary<string, object?>
        {
            ["expected_max_gust"] = medianRounded,
            ["models_agreeing"] = CountModelsAtMax(paramConsensus, 24),
        };

        return new Alert(AlertType.Storm,
            confidence > 0 ? AlertSeverity.Yellow : AlertSeverity.None,
            confidence, attrs,
            TriggerValue: medianRounded,
            Threshold: gustThreshold,
            PeakValue: peakRounded != medianRounded ? peakRounded : null);
    }

    public static Alert EvaluateHeavyRain(
        ConsensusSnapshot consensus, double hourlyThreshold, double dailyThreshold)
    {
        if (Precipitation is null)
        {
            return Alert.None(AlertType.HeavyRain);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, Precipitation);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.HeavyRain);
        }

        var maxHourly = 0.0;
        var hourlySum = 0.0;
        var hourlyExceedCount = 0;
        var hourlyAgreements = new List<double>();

        foreach (var (horizonKey, hc) in paramConsensus.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > 24)
            {
                continue;
            }

            if (hc.Median is not { } value)
            {
                continue;
            }

            if (value > maxHourly)
            {
                maxHourly = value;
            }

            hourlySum += value;

            if (value >= hourlyThreshold)
            {
                hourlyExceedCount++;
                if (hc.Agreement is { } agreement)
                {
                    hourlyAgreements.Add(agreement);
                }
            }
        }

        // Check daily consensus for daily sum
        var dailySum = hourlySum;
        if (DailyPrecipSum is not null)
        {
            var dailyParam = FindParam(consensus.Daily.Parameters, DailyPrecipSum);
            if (dailyParam is not null)
            {
                foreach (var (horizonKey, hc) in dailyParam.ByHorizon)
                {
                    var day = ParseHorizonDay(horizonKey);
                    if (day is null || day > 1)
                    {
                        continue;
                    }

                    if (hc.Median is { } value && value > dailySum)
                    {
                        dailySum = value;
                    }
                }
            }
        }

        var dailyExceeds = dailySum >= dailyThreshold;
        var hourlyExceeds = hourlyExceedCount > 0;

        if (!hourlyExceeds && !dailyExceeds)
        {
            return Alert.None(AlertType.HeavyRain);
        }

        AlertSeverity severity;
        if (hourlyExceeds && dailyExceeds)
        {
            severity = AlertSeverity.Red;
        }
        else if (dailyExceeds)
        {
            severity = AlertSeverity.Orange;
        }
        else
        {
            severity = AlertSeverity.Yellow;
        }

        var confidence = hourlyAgreements.Count > 0
            ? Math.Round(hourlyAgreements.Average(), 3)
            : (dailyExceeds ? 1.0 : 0.0);

        var triggerValue = hourlyExceeds
            ? Math.Round(maxHourly, 1)
            : Math.Round(dailySum, 1);
        var effectiveThreshold = hourlyExceeds ? hourlyThreshold : dailyThreshold;

        var attrs = new Dictionary<string, object?>
        {
            ["hourly_exceed_models"] = hourlyExceedCount,
            ["daily_exceed_models"] = dailyExceeds ? 1 : 0,
        };

        return new Alert(AlertType.HeavyRain, severity, confidence, attrs,
            TriggerValue: triggerValue,
            Threshold: effectiveThreshold);
    }

    public static Alert EvaluateUv(ConsensusSnapshot consensus)
    {
        if (UvIndex is null)
        {
            return Alert.None(AlertType.Uv);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, UvIndex);
        var maxUv = 0.0;

        if (paramConsensus is not null)
        {
            foreach (var (horizonKey, hc) in paramConsensus.ByHorizon)
            {
                var hours = ParseHorizonHours(horizonKey);
                if (hours is null || hours > 24)
                {
                    continue;
                }

                if (hc.Median is { } value && value > maxUv)
                {
                    maxUv = value;
                }
            }
        }

        // Also check daily UV max
        if (DailyUvMax is not null)
        {
            var dailyParam = FindParam(consensus.Daily.Parameters, DailyUvMax);
            if (dailyParam is not null)
            {
                foreach (var (horizonKey, hc) in dailyParam.ByHorizon)
                {
                    var day = ParseHorizonDay(horizonKey);
                    if (day is null || day > 1)
                    {
                        continue;
                    }

                    if (hc.Median is { } value && value > maxUv)
                    {
                        maxUv = value;
                    }
                }
            }
        }

        if (maxUv <= 0)
        {
            return Alert.None(AlertType.Uv);
        }

        var (level, severity, uvThreshold) = maxUv switch
        {
            >= 11 => ("extreme", AlertSeverity.Red, 11.0),
            >= 8 => ("very_high", AlertSeverity.Red, 8.0),
            >= 6 => ("high", AlertSeverity.Orange, 6.0),
            >= 3 => ("moderate", AlertSeverity.Yellow, 3.0),
            _ => ("low", AlertSeverity.None, 0.0),
        };

        var medianRounded = Math.Round(maxUv, 1);

        var attrs = new Dictionary<string, object?>
        {
            ["uv_level"] = level,
            ["uv_index"] = medianRounded,
        };

        return new Alert(AlertType.Uv, severity, 1.0, attrs,
            TriggerValue: medianRounded,
            Threshold: uvThreshold);
    }

    public static Alert EvaluateFog(ConsensusSnapshot consensus)
    {
        if (Temperature is null || Dewpoint is null || WindSpeed is null || Humidity is null)
        {
            return Alert.None(AlertType.Fog);
        }

        var tempConsensus = FindParam(consensus.Hourly.Parameters, Temperature);
        var dewConsensus = FindParam(consensus.Hourly.Parameters, Dewpoint);
        var windConsensus = FindParam(consensus.Hourly.Parameters, WindSpeed);
        var humConsensus = FindParam(consensus.Hourly.Parameters, Humidity);

        if (tempConsensus is null || dewConsensus is null || windConsensus is null || humConsensus is null)
        {
            return Alert.None(AlertType.Fog);
        }

        var fogHours = 0;
        var fogAgreements = new List<double>();

        for (var h = 0; h <= 24; h++)
        {
            var key = $"h{h}";
            if (!tempConsensus.ByHorizon.TryGetValue(key, out var tempHc)
                || !dewConsensus.ByHorizon.TryGetValue(key, out var dewHc)
                || !windConsensus.ByHorizon.TryGetValue(key, out var windHc)
                || !humConsensus.ByHorizon.TryGetValue(key, out var humHc))
            {
                continue;
            }

            if (tempHc.Median is { } t && dewHc.Median is { } d
                && windHc.Median is { } w && humHc.Median is { } hum
                && (t - d) < 2.0 && w < 3.0 && hum > 90.0)
            {
                fogHours++;
                // Average agreement across the four parameters at this horizon
                var agreements = new List<double>();
                if (tempHc.Agreement is { } ta)
                {
                    agreements.Add(ta);
                }

                if (dewHc.Agreement is { } da)
                {
                    agreements.Add(da);
                }

                if (windHc.Agreement is { } wa)
                {
                    agreements.Add(wa);
                }

                if (humHc.Agreement is { } ha)
                {
                    agreements.Add(ha);
                }

                if (agreements.Count > 0)
                {
                    fogAgreements.Add(agreements.Average());
                }
            }
        }

        var confidence = fogAgreements.Count > 0
            ? Math.Round(fogAgreements.Average(), 3)
            : 0.0;

        var attrs = new Dictionary<string, object?>
        {
            ["fog_hours"] = fogHours,
            ["models_agreeing"] = CountModelsAtHorizon(tempConsensus, 0),
        };

        return new Alert(AlertType.Fog,
            fogHours > 0 ? AlertSeverity.Yellow : AlertSeverity.None,
            confidence, attrs,
            TriggerValue: fogHours,
            Threshold: 1,
            DurationHours: fogHours > 0 ? fogHours : null);
    }

    public static Alert EvaluateSnow(ConsensusSnapshot consensus)
    {
        if (Snowfall is null)
        {
            return Alert.None(AlertType.Snow);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, Snowfall);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.Snow);
        }

        var hourlySum = 0.0;
        var snowAgreements = new List<double>();
        var freezingLevels = new List<double>();

        if (FreezingLevel is not null)
        {
            var flConsensus = FindParam(consensus.Hourly.Parameters, FreezingLevel);
            if (flConsensus is not null)
            {
                foreach (var (horizonKey, hc) in flConsensus.ByHorizon)
                {
                    var hours = ParseHorizonHours(horizonKey);
                    if (hours is null || hours > 24)
                    {
                        continue;
                    }

                    if (hc.Median is { } value)
                    {
                        freezingLevels.Add(value);
                    }
                }
            }
        }

        foreach (var (horizonKey, hc) in paramConsensus.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > 24)
            {
                continue;
            }

            if (hc.Median is not { } value)
            {
                continue;
            }

            hourlySum += value;

            if (value > 0 && hc.Agreement is { } agreement)
            {
                snowAgreements.Add(agreement);
            }
        }

        // Check daily snowfall sum
        var totalSum = hourlySum;
        if (DailySnowfallSum is not null)
        {
            var dailyParam = FindParam(consensus.Daily.Parameters, DailySnowfallSum);
            if (dailyParam is not null)
            {
                foreach (var (horizonKey, hc) in dailyParam.ByHorizon)
                {
                    var day = ParseHorizonDay(horizonKey);
                    if (day is null || day > 1)
                    {
                        continue;
                    }

                    if (hc.Median is { } value && value > totalSum)
                    {
                        totalSum = value;
                    }
                }
            }
        }

        var confidence = snowAgreements.Count > 0
            ? Math.Round(snowAgreements.Average(), 3)
            : 0.0;

        var (severity, snowThreshold) = totalSum switch
        {
            > 20 => (AlertSeverity.Red, 20.0),
            > 5 => (AlertSeverity.Orange, 5.0),
            > 0 => (AlertSeverity.Yellow, 0.0),
            _ => (AlertSeverity.None, 0.0),
        };
        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var roundedSum = Math.Round(totalSum, 1);

        var attrs = new Dictionary<string, object?>
        {
            ["expected_accumulation"] = roundedSum,
            ["freezing_level"] = freezingLevels.Count > 0 ? Math.Round(Median(freezingLevels), 0) : null,
            ["models_agreeing"] = CountModelsAtMax(paramConsensus, 24),
        };

        return new Alert(AlertType.Snow, severity, confidence, attrs,
            TriggerValue: roundedSum,
            Threshold: snowThreshold);
    }

    public static Alert EvaluatePressureDrop(
        ConsensusSnapshot consensus, double dropThreshold)
    {
        if (PressureMsl is null)
        {
            return Alert.None(AlertType.PressureDrop);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, PressureMsl);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.PressureDrop);
        }

        // Collect median values by hour for 3-hour drop detection
        var medianByHour = new SortedDictionary<int, double>();
        foreach (var (horizonKey, hc) in paramConsensus.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > 24)
            {
                continue;
            }

            if (hc.Median is { } value)
            {
                medianByHour[hours.Value] = value;
            }
        }

        if (medianByHour.Count < 4)
        {
            return Alert.None(AlertType.PressureDrop);
        }

        var maxDrop = 0.0;
        var hourKeys = medianByHour.Keys.ToList();
        for (var i = 0; i < hourKeys.Count; i++)
        {
            var laterHour = hourKeys[i];
            // Find an hour ~3 hours earlier
            for (var j = 0; j < i; j++)
            {
                var earlierHour = hourKeys[j];
                if (laterHour - earlierHour >= 3)
                {
                    var drop = medianByHour[earlierHour] - medianByHour[laterHour];
                    if (drop > maxDrop)
                    {
                        maxDrop = drop;
                    }
                }
            }
        }

        var exceeds = maxDrop >= dropThreshold;
        var confidence = exceeds
            ? AverageAgreementAll(paramConsensus, 24)
            : 0.0;

        var medianDrop = Math.Round(maxDrop, 1);

        var attrs = new Dictionary<string, object?>
        {
            ["max_drop"] = medianDrop,
            ["models_agreeing"] = CountModelsAtMax(paramConsensus, 24),
        };

        return new Alert(AlertType.PressureDrop,
            exceeds ? AlertSeverity.Yellow : AlertSeverity.None,
            Math.Round(confidence, 3), attrs,
            TriggerValue: medianDrop,
            Threshold: dropThreshold);
    }

    public static Alert EvaluateThunderstorm(
        ConsensusSnapshot consensus,
        double capeThreshold, double precipThreshold, double gustThreshold)
    {
        if (Cape is null || Precipitation is null || WindGusts is null)
        {
            return Alert.None(AlertType.Thunderstorm);
        }

        var capeConsensus = FindParam(consensus.Hourly.Parameters, Cape);
        var precipConsensus = FindParam(consensus.Hourly.Parameters, Precipitation);
        var gustConsensus = FindParam(consensus.Hourly.Parameters, WindGusts);

        if (capeConsensus is null || precipConsensus is null || gustConsensus is null)
        {
            return Alert.None(AlertType.Thunderstorm);
        }

        var meetAllCount = 0;
        var totalHorizons = 0;
        var maxCape = 0.0;

        for (var h = 0; h <= 24; h++)
        {
            var key = $"h{h}";
            if (!capeConsensus.ByHorizon.TryGetValue(key, out var capeHc)
                || !precipConsensus.ByHorizon.TryGetValue(key, out var precipHc)
                || !gustConsensus.ByHorizon.TryGetValue(key, out var gustHc))
            {
                continue;
            }

            totalHorizons++;

            if (capeHc.Median is { } cv)
            {
                if (cv > maxCape)
                {
                    maxCape = cv;
                }

                if (precipHc.Median is { } pv && gustHc.Median is { } gv
                    && cv > capeThreshold && pv > precipThreshold && gv > gustThreshold)
                {
                    meetAllCount++;
                }
            }
        }

        if (totalHorizons == 0)
        {
            return Alert.None(AlertType.Thunderstorm);
        }

        var confidence = totalHorizons > 0 ? (double)meetAllCount / totalHorizons : 0.0;
        var severity = confidence switch
        {
            > 0.75 => AlertSeverity.Red,
            >= 0.5 => AlertSeverity.Orange,
            > 0 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var medianCape = Math.Round(maxCape, 0);

        var attrs = new Dictionary<string, object?>
        {
            ["models_agreeing"] = CountModelsAtMax(capeConsensus, 24),
        };

        return new Alert(AlertType.Thunderstorm, severity, Math.Round(confidence, 3), attrs,
            TriggerValue: medianCape,
            Threshold: capeThreshold);
    }

    internal static ParameterConsensus? FindParam(
        IReadOnlyList<ParameterConsensus> parameters, ParameterDef param)
        => parameters.FirstOrDefault(p => p.Parameter == param);

    private static int? ParseHorizonHours(string horizonKey)
    {
        if (horizonKey.StartsWith('h') && int.TryParse(horizonKey.AsSpan(1), out var hours))
        {
            return hours;
        }

        return null;
    }

    private static int? ParseHorizonDay(string horizonKey)
    {
        if (horizonKey.StartsWith('d') && int.TryParse(horizonKey.AsSpan(1), out var day))
        {
            return day;
        }

        return null;
    }

    private static int CountModelsAtPeak(ParameterConsensus param, int? targetHour)
    {
        if (targetHour is null)
        {
            return 0;
        }

        var key = $"h{targetHour.Value}";
        return param.ByHorizon.TryGetValue(key, out var hc) ? hc.AvailableModels.Count : 0;
    }

    private static int CountModelsAtMax(ParameterConsensus param, int maxHours)
    {
        var max = 0;
        foreach (var (horizonKey, hc) in param.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > maxHours)
            {
                continue;
            }

            if (hc.AvailableModels.Count > max)
            {
                max = hc.AvailableModels.Count;
            }
        }

        return max;
    }

    private static int CountModelsAtHorizon(ParameterConsensus param, int hour)
    {
        var key = $"h{hour}";
        return param.ByHorizon.TryGetValue(key, out var hc) ? hc.AvailableModels.Count : 0;
    }

    private static double AverageAgreementAbove(ParameterConsensus param, double threshold, int maxHours)
    {
        var agreements = new List<double>();
        foreach (var (horizonKey, hc) in param.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > maxHours)
            {
                continue;
            }

            if (hc.Median is { } value && value >= threshold && hc.Agreement is { } agreement)
            {
                agreements.Add(agreement);
            }
        }

        return agreements.Count > 0 ? agreements.Average() : 0.0;
    }

    private static double AverageAgreementAll(ParameterConsensus param, int maxHours)
    {
        var agreements = new List<double>();
        foreach (var (horizonKey, hc) in param.ByHorizon)
        {
            var hours = ParseHorizonHours(horizonKey);
            if (hours is null || hours > maxHours)
            {
                continue;
            }

            if (hc.Agreement is { } agreement)
            {
                agreements.Add(agreement);
            }
        }

        return agreements.Count > 0 ? agreements.Average() : 0.0;
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        return ConsensusComputer.ComputeMedian(values.Select(v => (double?)v).ToList()) ?? 0;
    }
}
