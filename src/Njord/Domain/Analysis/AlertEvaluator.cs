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
        ModelSnapshot snapshot, string location, AlertThresholdOptions options, TimeProvider timeProvider)
    {
        var alerts = new List<Alert>
        {
            EvaluateFrost(snapshot, location, options.FrostThreshold, timeProvider),
            EvaluateHeat(snapshot, location, options.HeatThresholds, timeProvider),
            EvaluateStorm(snapshot, location, options.StormGustThreshold, timeProvider),
            EvaluateHeavyRain(snapshot, location, options.HeavyRainHourlyThreshold, options.HeavyRainDailyThreshold, timeProvider),
            EvaluateUv(snapshot, location, timeProvider),
            EvaluateFog(snapshot, location, timeProvider),
            EvaluateSnow(snapshot, location, timeProvider),
            EvaluatePressureDrop(snapshot, location, options.PressureDropThreshold, timeProvider),
            EvaluateThunderstorm(snapshot, location, options.CapeThreshold, options.ThunderstormPrecipThreshold, options.ThunderstormGustThreshold, timeProvider),
        };
        return new AlertResult(location, alerts);
    }

    public static Alert EvaluateFrost(
        ModelSnapshot snapshot, string location, double threshold, TimeProvider timeProvider)
    {
        if (Temperature is null)
        {
            return Alert.None(AlertType.Frost);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var minima = new List<double>();
        DateTimeOffset? earliestFrost = null;

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            if (points.Count == 0)
            {
                continue;
            }

            var min = double.MaxValue;
            foreach (var p in points)
            {
                var v = p.Get(Temperature);
                if (v is not { } val)
                {
                    continue;
                }

                if (val < min)
                {
                    min = val;
                }

                if (val <= threshold && (earliestFrost is null || p.ValidAt < earliestFrost))
                {
                    earliestFrost = p.ValidAt;
                }
            }
            if (min < double.MaxValue)
            {
                minima.Add(min);
            }
        }

        if (minima.Count == 0)
        {
            return Alert.None(AlertType.Frost);
        }

        var agreeing = minima.Count(m => m <= threshold);
        var confidence = (double)agreeing / minima.Count;
        var median = Median(minima);

        var attrs = new Dictionary<string, object?>
        {
            ["expected_low"] = Math.Round(median, 1),
            ["earliest_frost"] = earliestFrost?.ToString("O"),
            ["models_agreeing"] = agreeing,
        };

        var hoursUntil = earliestFrost.HasValue
            ? (int?)Math.Max(0, (int)(earliestFrost.Value - now).TotalHours)
            : null;

        return new Alert(AlertType.Frost,
            confidence > 0 ? AlertSeverity.Yellow : AlertSeverity.None,
            Math.Round(confidence, 3), attrs,
            TriggerValue: Math.Round(median, 1),
            Threshold: threshold,
            HoursUntil: hoursUntil);
    }

    public static Alert EvaluateHeat(
        ModelSnapshot snapshot, string location, double[] thresholds, TimeProvider timeProvider)
    {
        if (ApparentTemp is null || thresholds.Length < 3)
        {
            return Alert.None(AlertType.Heat);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var maxima = new List<double>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            var max = double.MinValue;
            foreach (var p in points)
            {
                var v = p.Get(ApparentTemp);
                if (v is { } val && val > max)
                {
                    max = val;
                }
            }
            if (max > double.MinValue)
            {
                maxima.Add(max);
            }
        }

        if (maxima.Count == 0)
        {
            return Alert.None(AlertType.Heat);
        }

        var redCount = maxima.Count(m => m >= thresholds[2]);
        var orangeCount = maxima.Count(m => m >= thresholds[1]);
        var yellowCount = maxima.Count(m => m >= thresholds[0]);

        AlertSeverity severity;
        double confidence;
        if (redCount > 0) { severity = AlertSeverity.Red; confidence = (double)redCount / maxima.Count; }
        else if (orangeCount > 0) { severity = AlertSeverity.Orange; confidence = (double)orangeCount / maxima.Count; }
        else if (yellowCount > 0) { severity = AlertSeverity.Yellow; confidence = (double)yellowCount / maxima.Count; }
        else { return Alert.None(AlertType.Heat); }

        var medianMax = Math.Round(Median(maxima), 1);
        var effectiveThreshold = severity == AlertSeverity.Red ? thresholds[2]
            : severity == AlertSeverity.Orange ? thresholds[1] : thresholds[0];
        var peakMax = Math.Round(maxima.Max(), 1);

        var attrs = new Dictionary<string, object?>
        {
            ["expected_max"] = medianMax,
            ["models_agreeing"] = severity == AlertSeverity.Red ? redCount : severity == AlertSeverity.Orange ? orangeCount : yellowCount,
        };

        return new Alert(AlertType.Heat, severity, Math.Round(confidence, 3), attrs,
            TriggerValue: medianMax,
            Threshold: effectiveThreshold,
            PeakValue: peakMax != medianMax ? peakMax : null);
    }

    public static Alert EvaluateStorm(
        ModelSnapshot snapshot, string location, double gustThreshold, TimeProvider timeProvider)
    {
        if (WindGusts is null)
        {
            return Alert.None(AlertType.Storm);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var maxGusts = new List<double>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            var max = double.MinValue;
            foreach (var p in points)
            {
                var v = p.Get(WindGusts);
                if (v is { } val && val > max)
                {
                    max = val;
                }
            }
            if (max > double.MinValue)
            {
                maxGusts.Add(max);
            }
        }

        if (maxGusts.Count == 0)
        {
            return Alert.None(AlertType.Storm);
        }

        var agreeing = maxGusts.Count(g => g >= gustThreshold);
        var confidence = (double)agreeing / maxGusts.Count;

        var medianGust = Math.Round(Median(maxGusts), 1);
        var peakGust = Math.Round(maxGusts.Max(), 1);

        var attrs = new Dictionary<string, object?>
        {
            ["expected_max_gust"] = medianGust,
            ["models_agreeing"] = agreeing,
        };

        return new Alert(AlertType.Storm,
            agreeing > 0 ? AlertSeverity.Yellow : AlertSeverity.None,
            Math.Round(confidence, 3), attrs,
            TriggerValue: medianGust,
            Threshold: gustThreshold,
            PeakValue: peakGust != medianGust ? peakGust : null);
    }

    public static Alert EvaluateHeavyRain(
        ModelSnapshot snapshot, string location, double hourlyThreshold, double dailyThreshold, TimeProvider timeProvider)
    {
        if (Precipitation is null)
        {
            return Alert.None(AlertType.HeavyRain);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var tomorrow = today.AddDays(1);
        var hourlyExceed = 0;
        var dailyExceed = 0;
        var modelCount = 0;
        var allMaxHourly = new List<double>();
        var allDailySums = new List<double>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            if (points.Count == 0)
            {
                continue;
            }

            modelCount++;

            var maxHourly = 0.0;
            var dailySum = 0.0;
            foreach (var p in points)
            {
                var v = p.Get(Precipitation) ?? 0.0;
                if (v > maxHourly)
                {
                    maxHourly = v;
                }

                dailySum += v;
            }

            if (DailyPrecipSum is not null)
            {
                foreach (var dp in forecast.Daily.Points)
                {
                    if (dp.Date != today && dp.Date != tomorrow)
                        continue;
                    var dv = dp.GetNumeric(DailyPrecipSum);
                    if (dv is { } val && val > dailySum)
                        dailySum = val;
                }
            }

            allMaxHourly.Add(maxHourly);
            allDailySums.Add(dailySum);

            if (maxHourly >= hourlyThreshold)
            {
                hourlyExceed++;
            }

            if (dailySum >= dailyThreshold)
            {
                dailyExceed++;
            }
        }

        if (modelCount == 0)
        {
            return Alert.None(AlertType.HeavyRain);
        }

        var totalExceed = Math.Max(hourlyExceed, dailyExceed);
        var confidence = (double)totalExceed / modelCount;

        AlertSeverity severity;
        if (hourlyExceed > 0 && dailyExceed > 0)
        {
            severity = AlertSeverity.Red;
        }
        else if (dailyExceed > 0)
        {
            severity = AlertSeverity.Orange;
        }
        else if (hourlyExceed > 0)
        {
            severity = AlertSeverity.Yellow;
        }
        else
        {
            return Alert.None(AlertType.HeavyRain);
        }

        var triggerValue = hourlyExceed >= dailyExceed
            ? Math.Round(Median(allMaxHourly), 1)
            : Math.Round(Median(allDailySums), 1);
        var effectiveThreshold = hourlyExceed >= dailyExceed ? hourlyThreshold : dailyThreshold;

        var attrs = new Dictionary<string, object?>
        {
            ["hourly_exceed_models"] = hourlyExceed,
            ["daily_exceed_models"] = dailyExceed,
        };

        return new Alert(AlertType.HeavyRain, severity, Math.Round(confidence, 3), attrs,
            TriggerValue: triggerValue,
            Threshold: effectiveThreshold);
    }

    public static Alert EvaluateUv(
        ModelSnapshot snapshot, string location, TimeProvider timeProvider)
    {
        if (UvIndex is null)
        {
            return Alert.None(AlertType.Uv);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var tomorrow = today.AddDays(1);
        var maxUvs = new List<double>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            var max = 0.0;
            foreach (var p in points)
            {
                var v = p.Get(UvIndex);
                if (v is { } val && val > max)
                {
                    max = val;
                }
            }

            if (DailyUvMax is not null)
            {
                foreach (var dp in forecast.Daily.Points)
                {
                    if (dp.Date != today && dp.Date != tomorrow)
                        continue;
                    var dv = dp.GetNumeric(DailyUvMax);
                    if (dv is { } val && val > max)
                        max = val;
                }
            }

            if (max > 0)
            {
                maxUvs.Add(max);
            }
        }

        if (maxUvs.Count == 0)
        {
            return Alert.None(AlertType.Uv);
        }

        var median = Median(maxUvs);
        var (level, severity, uvThreshold) = median switch
        {
            >= 11 => ("extreme", AlertSeverity.Red, 11.0),
            >= 8 => ("very_high", AlertSeverity.Red, 8.0),
            >= 6 => ("high", AlertSeverity.Orange, 6.0),
            >= 3 => ("moderate", AlertSeverity.Yellow, 3.0),
            _ => ("low", AlertSeverity.None, 0.0),
        };

        var peakUv = Math.Round(maxUvs.Max(), 1);
        var medianRounded = Math.Round(median, 1);

        var attrs = new Dictionary<string, object?>
        {
            ["uv_level"] = level,
            ["uv_index"] = medianRounded,
        };

        return new Alert(AlertType.Uv, severity, 1.0, attrs,
            TriggerValue: medianRounded,
            Threshold: uvThreshold,
            PeakValue: peakUv != medianRounded ? peakUv : null);
    }

    public static Alert EvaluateFog(
        ModelSnapshot snapshot, string location, TimeProvider timeProvider)
    {
        if (Temperature is null || Dewpoint is null || WindSpeed is null || Humidity is null)
        {
            return Alert.None(AlertType.Fog);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var fogHourCounts = new List<int>();
        var modelCount = 0;

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            if (points.Count == 0)
            {
                continue;
            }

            modelCount++;

            var fogHours = 0;
            foreach (var p in points)
            {
                var temp = p.Get(Temperature);
                var dew = p.Get(Dewpoint);
                var wind = p.Get(WindSpeed);
                var hum = p.Get(Humidity);
                if (temp is { } t && dew is { } d && wind is { } w && hum is { } h
                    && (t - d) < 2.0 && w < 3.0 && h > 90.0)
                {
                    fogHours++;
                }
            }
            fogHourCounts.Add(fogHours);
        }

        if (modelCount == 0)
        {
            return Alert.None(AlertType.Fog);
        }

        var agreeing = fogHourCounts.Count(c => c > 0);
        var confidence = (double)agreeing / modelCount;

        var medianFogHours = fogHourCounts.Count > 0 ? (int)Median(fogHourCounts.Select(c => (double)c).ToList()) : 0;

        var attrs = new Dictionary<string, object?>
        {
            ["fog_hours"] = medianFogHours,
            ["models_agreeing"] = agreeing,
        };

        return new Alert(AlertType.Fog,
            agreeing > 0 ? AlertSeverity.Yellow : AlertSeverity.None,
            Math.Round(confidence, 3), attrs,
            TriggerValue: medianFogHours,
            Threshold: 1,
            DurationHours: medianFogHours > 0 ? medianFogHours : null);
    }

    public static Alert EvaluateSnow(
        ModelSnapshot snapshot, string location, TimeProvider timeProvider)
    {
        if (Snowfall is null)
        {
            return Alert.None(AlertType.Snow);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var tomorrow = today.AddDays(1);
        var sums = new List<double>();
        var freezingLevels = new List<double>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            if (points.Count == 0)
            {
                continue;
            }

            var sum = 0.0;
            foreach (var p in points)
            {
                sum += p.Get(Snowfall) ?? 0.0;
                if (FreezingLevel is not null && p.Get(FreezingLevel) is { } fl)
                {
                    freezingLevels.Add(fl);
                }
            }

            if (DailySnowfallSum is not null)
            {
                foreach (var dp in forecast.Daily.Points)
                {
                    if (dp.Date != today && dp.Date != tomorrow)
                        continue;
                    var dv = dp.GetNumeric(DailySnowfallSum);
                    if (dv is { } val && val > sum)
                        sum = val;
                }
            }

            sums.Add(sum);
        }

        if (sums.Count == 0)
        {
            return Alert.None(AlertType.Snow);
        }

        var agreeing = sums.Count(s => s > 0);
        var confidence = (double)agreeing / sums.Count;
        var medianSum = Median(sums);

        var (severity, snowThreshold) = medianSum switch
        {
            > 20 => (AlertSeverity.Red, 20.0),
            > 5 => (AlertSeverity.Orange, 5.0),
            > 0 => (AlertSeverity.Yellow, 0.0),
            _ => (AlertSeverity.None, 0.0),
        };
        if (agreeing == 0)
        {
            severity = AlertSeverity.None;
        }

        var roundedSum = Math.Round(medianSum, 1);
        var peakSum = Math.Round(sums.Max(), 1);

        var attrs = new Dictionary<string, object?>
        {
            ["expected_accumulation"] = roundedSum,
            ["freezing_level"] = freezingLevels.Count > 0 ? Math.Round(Median(freezingLevels), 0) : null,
            ["models_agreeing"] = agreeing,
        };

        return new Alert(AlertType.Snow, severity, Math.Round(confidence, 3), attrs,
            TriggerValue: roundedSum,
            Threshold: snowThreshold,
            PeakValue: peakSum != roundedSum ? peakSum : null);
    }

    public static Alert EvaluatePressureDrop(
        ModelSnapshot snapshot, string location, double dropThreshold, TimeProvider timeProvider)
    {
        if (PressureMsl is null)
        {
            return Alert.None(AlertType.PressureDrop);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var maxDrops = new List<double>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            if (points.Count < 4)
            {
                continue;
            }

            var maxDrop = 0.0;
            for (var i = 3; i < points.Count; i++)
            {
                var earlier = points[i - 3].Get(PressureMsl);
                var later = points[i].Get(PressureMsl);
                if (earlier is { } e && later is { } l)
                {
                    var drop = e - l;
                    if (drop > maxDrop)
                    {
                        maxDrop = drop;
                    }
                }
            }
            maxDrops.Add(maxDrop);
        }

        if (maxDrops.Count == 0)
        {
            return Alert.None(AlertType.PressureDrop);
        }

        var agreeing = maxDrops.Count(d => d >= dropThreshold);
        var confidence = (double)agreeing / maxDrops.Count;

        var medianDrop = Math.Round(Median(maxDrops), 1);

        var attrs = new Dictionary<string, object?>
        {
            ["max_drop"] = medianDrop,
            ["models_agreeing"] = agreeing,
        };

        return new Alert(AlertType.PressureDrop,
            agreeing > 0 ? AlertSeverity.Yellow : AlertSeverity.None,
            Math.Round(confidence, 3), attrs,
            TriggerValue: medianDrop,
            Threshold: dropThreshold);
    }

    public static Alert EvaluateThunderstorm(
        ModelSnapshot snapshot, string location,
        double capeThreshold, double precipThreshold, double gustThreshold,
        TimeProvider timeProvider)
    {
        if (Cape is null || Precipitation is null || WindGusts is null)
        {
            return Alert.None(AlertType.Thunderstorm);
        }

        var now = timeProvider.GetUtcNow();
        var end = now.AddHours(24);
        var modelCount = 0;
        var meetAll = 0;
        var maxCapes = new List<double>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var points = PointsInWindow(forecast.Hourly, now, end);
            if (points.Count == 0)
            {
                continue;
            }

            modelCount++;

            var met = false;
            var maxCape = 0.0;
            foreach (var p in points)
            {
                var c = p.Get(Cape);
                var pr = p.Get(Precipitation);
                var g = p.Get(WindGusts);
                if (c is { } cv)
                {
                    if (cv > maxCape) maxCape = cv;
                    if (pr is { } pv && g is { } gv
                        && cv > capeThreshold && pv > precipThreshold && gv > gustThreshold)
                    {
                        met = true;
                    }
                }
            }
            maxCapes.Add(maxCape);
            if (met)
            {
                meetAll++;
            }
        }

        if (modelCount == 0)
        {
            return Alert.None(AlertType.Thunderstorm);
        }

        var confidence = (double)meetAll / modelCount;
        var severity = confidence switch
        {
            > 0.75 => AlertSeverity.Red,
            >= 0.5 => AlertSeverity.Orange,
            > 0 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var medianCape = Math.Round(Median(maxCapes), 0);

        var attrs = new Dictionary<string, object?>
        {
            ["models_agreeing"] = meetAll,
        };

        return new Alert(AlertType.Thunderstorm, severity, Math.Round(confidence, 3), attrs,
            TriggerValue: medianCape,
            Threshold: capeThreshold);
    }

    private static List<ForecastPoint> PointsInWindow(
        ForecastSeries series, DateTimeOffset start, DateTimeOffset end)
        => series.Points.Where(p => p.ValidAt >= start && p.ValidAt <= end).ToList();

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        return ConsensusComputer.ComputeMedian(values.Select(v => (double?)v).ToList()) ?? 0;
    }
}
