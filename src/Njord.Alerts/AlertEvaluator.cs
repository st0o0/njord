using Njord.Compute.Analysis;
using Njord.Compute.Configuration;
using Njord.Weather;

namespace Njord.Alerts;

public static class AlertEvaluator
{
    private const int MaxHorizonHours = 24;
    private const int MaxHorizonDays = 1;

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
    private static readonly ParameterDef Rain = ParameterRegistry.Rain;
    private static readonly ParameterDef SoilTemp0cm = ParameterRegistry.SoilTemperature0cm;
    private static readonly ParameterDef VisibilityParam = ParameterRegistry.Visibility;
    private static readonly ParameterDef IsDay = ParameterRegistry.IsDay;
    private static readonly ParameterDef DailyPrecipSum = ParameterRegistry.PrecipitationSum;
    private static readonly ParameterDef DailyUvMax = ParameterRegistry.UvIndexMax;
    private static readonly ParameterDef DailySnowfallSum = ParameterRegistry.SnowfallSum;

    public static AlertResult EvaluateAll(
        ConsensusSnapshot consensus, AlertOptions options, TimeProvider timeProvider)
    {
        var alerts = new List<Alert>
        {
            EvaluateFrost(consensus, options.FrostThresholds),
            EvaluateHeat(consensus, options.HeatThresholds),
            EvaluateStorm(consensus, options.StormGustThresholds),
            EvaluateHeavyRain(consensus, options.HeavyRainHourlyThreshold, options.HeavyRainDailyThreshold),
            EvaluateUv(consensus),
            EvaluateFog(consensus, options.FogPersistentHours),
            EvaluateSnow(consensus),
            EvaluatePressureDrop(consensus, options.PressureDropThreshold, options.PressureDropSevereThreshold),
            EvaluateThunderstorm(consensus, options.CapeThreshold, options.ThunderstormPrecipThreshold, options.ThunderstormGustThreshold),
            EvaluateIce(consensus, options.IceThreshold),
            EvaluateWindChill(consensus, options.WindChillThresholds),
            EvaluateVisibility(consensus, options.VisibilityThresholds),
            EvaluateTropicalNight(consensus, options.TropicalNightThresholds),
            EvaluateHumidity(consensus, options.HumidityThresholds),
        };
        return new AlertResult(consensus.Location, alerts);
    }

    public static Alert EvaluateFrost(
        ConsensusSnapshot consensus, double[] thresholds)
    {
        if (thresholds.Length < 3)
        {
            return Alert.None(AlertType.Frost);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, Temperature);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.Frost);
        }

        var scan = HorizonExtremeScanner.FindExtreme(
            paramConsensus.ByHorizon, MaxHorizonHours, ScanDirection.Min, v => v <= thresholds[0]);
        if (!scan.Found)
        {
            return Alert.None(AlertType.Frost);
        }

        var minMedian = scan.ExtremeValue;
        var earliestFrostHour = scan.EarliestQualifyingHour;
        var confidence = AverageRounded(scan.Agreements);

        var (severity, effectiveThreshold) = ThresholdClassifier.Classify(minMedian, thresholds, ScanDirection.Min);

        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var attrs = new Dictionary<string, object?>
        {
            ["expected_low"] = Math.Round(minMedian, 1),
            ["earliest_frost"] = earliestFrostHour.HasValue
                ? $"+{earliestFrostHour.Value}h"
                : null,
            ["models_agreeing"] = CountModelsAtPeak(paramConsensus, earliestFrostHour),
        };

        return new Alert(AlertType.Frost, severity,
            confidence, attrs,
            TriggerValue: Math.Round(minMedian, 1),
            Threshold: effectiveThreshold,
            HoursUntil: earliestFrostHour);
    }

    public static Alert EvaluateHeat(
        ConsensusSnapshot consensus, double[] thresholds)
    {
        if (thresholds.Length < 3)
        {
            return Alert.None(AlertType.Heat);
        }

        var apparentConsensus = FindParam(consensus.Hourly.Parameters, ApparentTemp);
        var tempConsensus = FindParam(consensus.Hourly.Parameters, Temperature);
        if (apparentConsensus is null && tempConsensus is null)
        {
            return Alert.None(AlertType.Heat);
        }

        var found = false;
        var maxMedian = double.MinValue;
        var peakMax = double.MinValue;
        var bestConsensus = apparentConsensus ?? tempConsensus!;

        foreach (var source in new[] { apparentConsensus, tempConsensus })
        {
            if (source is null)
            {
                continue;
            }

            var scan = HorizonExtremeScanner.FindExtreme(
                source.ByHorizon, MaxHorizonHours, ScanDirection.Max, peakSeed: peakMax);
            peakMax = scan.Peak;
            if (scan.Found && scan.ExtremeValue > maxMedian)
            {
                found = true;
                maxMedian = scan.ExtremeValue;
                bestConsensus = source;
            }
        }

        if (!found)
        {
            return Alert.None(AlertType.Heat);
        }

        var (severity, effectiveThreshold) = ThresholdClassifier.Classify(maxMedian, thresholds, ScanDirection.Max);
        if (severity == AlertSeverity.None)
        {
            return Alert.None(AlertType.Heat);
        }

        var agreementAtSeverity = AverageAgreementAbove(bestConsensus, effectiveThreshold, MaxHorizonHours);

        var medianRounded = Math.Round(maxMedian, 1);
        var peakRounded = peakMax > double.MinValue ? Math.Round(peakMax, 1) : medianRounded;

        var attrs = new Dictionary<string, object?>
        {
            ["expected_max"] = medianRounded,
            ["models_agreeing"] = CountModelsAtMax(bestConsensus, MaxHorizonHours),
        };

        return new Alert(AlertType.Heat, severity, Math.Round(agreementAtSeverity, 3), attrs,
            TriggerValue: medianRounded,
            Threshold: effectiveThreshold,
            PeakValue: peakRounded != medianRounded ? peakRounded : null);
    }

    public static Alert EvaluateStorm(
        ConsensusSnapshot consensus, double[] thresholds)
    {
        if (thresholds.Length < 3)
        {
            return Alert.None(AlertType.Storm);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, WindGusts);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.Storm);
        }

        var scan = HorizonExtremeScanner.FindExtreme(
            paramConsensus.ByHorizon, MaxHorizonHours, ScanDirection.Max, v => v >= thresholds[0]);
        if (!scan.Found)
        {
            return Alert.None(AlertType.Storm);
        }

        var maxMedian = scan.ExtremeValue;
        var peakMax = scan.Peak;
        var confidence = AverageRounded(scan.Agreements);

        var (severity, effectiveThreshold) = ThresholdClassifier.Classify(maxMedian, thresholds, ScanDirection.Max);

        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var medianRounded = Math.Round(maxMedian, 1);
        var peakRounded = peakMax > double.MinValue ? Math.Round(peakMax, 1) : medianRounded;

        var attrs = new Dictionary<string, object?>
        {
            ["expected_max_gust"] = medianRounded,
            ["models_agreeing"] = CountModelsAtMax(paramConsensus, MaxHorizonHours),
        };

        return new Alert(AlertType.Storm, severity,
            confidence, attrs,
            TriggerValue: medianRounded,
            Threshold: effectiveThreshold,
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

        var hourlyScan = HorizonExtremeScanner.FindExtreme(
            paramConsensus.ByHorizon, MaxHorizonHours, ScanDirection.Max, v => v >= hourlyThreshold);
        var maxHourly = Math.Max(0.0, hourlyScan.Found ? hourlyScan.ExtremeValue : 0.0);
        var hourlySum = hourlyScan.Sum;
        var hourlyExceedCount = hourlyScan.QualifyingCount;
        var hourlyAgreements = hourlyScan.Agreements;

        // Check daily consensus for daily sum
        var dailySum = Math.Max(hourlySum, DailyMax(consensus, DailyPrecipSum) ?? double.MinValue);

        var dailyExceeds = dailySum >= dailyThreshold;
        var hourlyExceeds = hourlyExceedCount > 0;

        AlertSeverity severity;
        if (hourlyExceeds && dailyExceeds)
        {
            severity = AlertSeverity.Red;
        }
        else if (dailyExceeds)
        {
            severity = AlertSeverity.Orange;
        }
        else if (hourlyExceeds)
        {
            severity = AlertSeverity.Yellow;
        }
        else
        {
            severity = AlertSeverity.None;
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
            var scan = HorizonExtremeScanner.FindExtreme(
                paramConsensus.ByHorizon, MaxHorizonHours, ScanDirection.Max);
            if (scan.Found)
            {
                maxUv = Math.Max(maxUv, scan.ExtremeValue);
            }
        }

        // Also check daily UV max
        maxUv = Math.Max(maxUv, DailyMax(consensus, DailyUvMax) ?? maxUv);

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

    public static Alert EvaluateFog(ConsensusSnapshot consensus, int fogPersistentHours = 4)
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

        for (var h = 0; h <= MaxHorizonHours; h++)
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

        var fogSeverity = fogHours switch
        {
            >= 1 when fogHours >= fogPersistentHours => AlertSeverity.Orange,
            >= 1 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        return new Alert(AlertType.Fog, fogSeverity,
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

        var freezingLevels = new List<double>();

        if (FreezingLevel is not null)
        {
            var flConsensus = FindParam(consensus.Hourly.Parameters, FreezingLevel);
            if (flConsensus is not null)
            {
                freezingLevels.AddRange(
                    HorizonExtremeScanner.WithMedian(flConsensus.ByHorizon, MaxHorizonHours)
                        .Select(e => e.Value));
            }
        }

        var snowScan = HorizonExtremeScanner.FindExtreme(
            paramConsensus.ByHorizon, MaxHorizonHours, ScanDirection.Max, v => v > 0);

        // Check daily snowfall sum
        var totalSum = Math.Max(snowScan.Sum, DailyMax(consensus, DailySnowfallSum) ?? double.MinValue);

        var confidence = AverageRounded(snowScan.Agreements);

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
            ["models_agreeing"] = CountModelsAtMax(paramConsensus, MaxHorizonHours),
        };

        return new Alert(AlertType.Snow, severity, confidence, attrs,
            TriggerValue: roundedSum,
            Threshold: snowThreshold);
    }

    public static Alert EvaluatePressureDrop(
        ConsensusSnapshot consensus, double dropThreshold, double severeThreshold = 10.0)
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
        foreach (var (hours, _, _, value) in HorizonExtremeScanner.WithMedian(paramConsensus.ByHorizon, MaxHorizonHours))
        {
            medianByHour[hours] = value;
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
            ? AverageAgreementAll(paramConsensus, MaxHorizonHours)
            : 0.0;

        var pressureSeverity = maxDrop switch
        {
            _ when maxDrop >= severeThreshold => AlertSeverity.Orange,
            _ when maxDrop >= dropThreshold => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        var medianDrop = Math.Round(maxDrop, 1);

        var attrs = new Dictionary<string, object?>
        {
            ["max_drop"] = medianDrop,
            ["models_agreeing"] = CountModelsAtMax(paramConsensus, MaxHorizonHours),
        };

        return new Alert(AlertType.PressureDrop, pressureSeverity,
            Math.Round(confidence, 3), attrs,
            TriggerValue: medianDrop,
            Threshold: exceeds ? (maxDrop >= severeThreshold ? severeThreshold : dropThreshold) : dropThreshold);
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

        for (var h = 0; h <= MaxHorizonHours; h++)
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
            ["models_agreeing"] = CountModelsAtMax(capeConsensus, MaxHorizonHours),
        };

        return new Alert(AlertType.Thunderstorm, severity, Math.Round(confidence, 3), attrs,
            TriggerValue: medianCape,
            Threshold: capeThreshold);
    }

    public static Alert EvaluateIce(
        ConsensusSnapshot consensus, double iceThreshold)
    {
        if (Rain is null)
        {
            return Alert.None(AlertType.Ice);
        }

        var rainConsensus = FindParam(consensus.Hourly.Parameters, Rain);
        var tempConsensus = FindParam(consensus.Hourly.Parameters, Temperature);
        if (rainConsensus is null || tempConsensus is null)
        {
            return Alert.None(AlertType.Ice);
        }

        var soilConsensus = SoilTemp0cm is not null
            ? FindParam(consensus.Hourly.Parameters, SoilTemp0cm)
            : null;

        var rainHours = 0;
        var minTemp = double.MaxValue;
        var iceAgreements = new List<double>();
        var soilFrozen = false;

        for (var h = 0; h <= MaxHorizonHours; h++)
        {
            var key = $"h{h}";
            if (!rainConsensus.ByHorizon.TryGetValue(key, out var rainHc)
                || !tempConsensus.ByHorizon.TryGetValue(key, out var tempHc))
            {
                continue;
            }

            if (rainHc.Median is { } rainVal && rainVal > 0
                && tempHc.Median is { } tempVal && tempVal <= iceThreshold)
            {
                rainHours++;
                if (tempVal < minTemp)
                {
                    minTemp = tempVal;
                }

                if (tempHc.Agreement is { } agreement)
                {
                    iceAgreements.Add(agreement);
                }

                if (soilConsensus?.ByHorizon.GetValueOrDefault(key)?.Median is { } soilVal && soilVal <= 0)
                {
                    soilFrozen = true;
                }
            }
        }

        if (rainHours == 0)
        {
            return Alert.None(AlertType.Ice);
        }

        var confidence = iceAgreements.Count > 0
            ? Math.Round(iceAgreements.Average(), 3)
            : 0.0;

        var severity = (minTemp, soilFrozen) switch
        {
            ( <= 0, true) => AlertSeverity.Red,
            ( <= 0, false) => AlertSeverity.Orange,
            _ => AlertSeverity.Yellow,
        };

        var attrs = new Dictionary<string, object?>
        {
            ["expected_low"] = Math.Round(minTemp, 1),
            ["rain_hours"] = rainHours,
            ["soil_frozen"] = soilFrozen,
        };

        return new Alert(AlertType.Ice, severity, confidence, attrs,
            TriggerValue: Math.Round(minTemp, 1),
            Threshold: iceThreshold);
    }

    public static Alert EvaluateWindChill(
        ConsensusSnapshot consensus, double[] thresholds)
    {
        if (thresholds.Length < 3)
        {
            return Alert.None(AlertType.WindChill);
        }

        var apparentConsensus = FindParam(consensus.Hourly.Parameters, ApparentTemp);
        var tempConsensus = FindParam(consensus.Hourly.Parameters, Temperature);
        if (apparentConsensus is null)
        {
            return Alert.None(AlertType.WindChill);
        }

        var scan = HorizonExtremeScanner.FindExtreme(
            apparentConsensus.ByHorizon, MaxHorizonHours, ScanDirection.Min, v => v <= thresholds[0]);
        if (!scan.Found)
        {
            return Alert.None(AlertType.WindChill);
        }

        var minApparent = scan.ExtremeValue;
        var windFactor = 0.0;
        if (scan.ExtremeHorizonKey is { } extremeKey
            && tempConsensus?.ByHorizon.GetValueOrDefault(extremeKey)?.Median is { } tempVal)
        {
            windFactor = Math.Round(tempVal - minApparent, 1);
        }

        var confidence = AverageRounded(scan.Agreements);

        var (severity, effectiveThreshold) = ThresholdClassifier.Classify(minApparent, thresholds, ScanDirection.Min);

        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var exposureRisk = severity switch
        {
            AlertSeverity.Red => "frostbite_10min",
            AlertSeverity.Orange => "frostbite_30min",
            _ => (string?)null,
        };

        var attrs = new Dictionary<string, object?>
        {
            ["felt_temperature"] = Math.Round(minApparent, 1),
            ["wind_factor"] = windFactor,
            ["exposure_risk"] = exposureRisk,
        };

        return new Alert(AlertType.WindChill, severity, confidence, attrs,
            TriggerValue: Math.Round(minApparent, 1),
            Threshold: effectiveThreshold);
    }

    public static Alert EvaluateVisibility(
        ConsensusSnapshot consensus, double[] thresholds)
    {
        if (thresholds.Length < 3 || VisibilityParam is null)
        {
            return Alert.None(AlertType.Visibility);
        }

        var paramConsensus = FindParam(consensus.Hourly.Parameters, VisibilityParam);
        if (paramConsensus is null)
        {
            return Alert.None(AlertType.Visibility);
        }

        var scan = HorizonExtremeScanner.FindExtreme(
            paramConsensus.ByHorizon, MaxHorizonHours, ScanDirection.Min, v => v < thresholds[0]);
        if (!scan.Found)
        {
            return Alert.None(AlertType.Visibility);
        }

        var minVis = scan.ExtremeValue;
        var hoursBelow1000 = scan.QualifyingCount;
        var totalHorizons = scan.Count;

        var confidence = totalHorizons > 0 && hoursBelow1000 > 0
            ? Math.Round((double)hoursBelow1000 / totalHorizons, 3)
            : 0.0;

        var (severity, effectiveThreshold) = ThresholdClassifier.Classify(
            minVis, thresholds, ScanDirection.Min, strict: true);

        var attrs = new Dictionary<string, object?>
        {
            ["min_visibility"] = Math.Round(minVis, 0),
            ["hours_below_1000m"] = hoursBelow1000,
        };

        return new Alert(AlertType.Visibility, severity, confidence, attrs,
            TriggerValue: Math.Round(minVis, 0),
            Threshold: effectiveThreshold);
    }

    public static Alert EvaluateTropicalNight(
        ConsensusSnapshot consensus, double[] thresholds)
    {
        if (thresholds.Length < 3)
        {
            return Alert.None(AlertType.TropicalNight);
        }

        var tempConsensus = FindParam(consensus.Hourly.Parameters, Temperature);
        var isDayConsensus = FindParam(consensus.Hourly.Parameters, IsDay);
        if (tempConsensus is null || isDayConsensus is null)
        {
            return Alert.None(AlertType.TropicalNight);
        }

        var nightMin = double.MaxValue;
        var hoursAbove20 = 0;
        var nightAgreements = new List<double>();
        var nightHoursCount = 0;

        for (var h = 0; h <= MaxHorizonHours; h++)
        {
            var key = $"h{h}";
            var isDayHc = isDayConsensus.ByHorizon.GetValueOrDefault(key);
            if (isDayHc?.Median is not { } isDayVal || isDayVal > 0.5)
            {
                continue;
            }

            nightHoursCount++;

            if (!tempConsensus.ByHorizon.TryGetValue(key, out var tempHc)
                || tempHc.Median is not { } tempVal)
            {
                continue;
            }

            if (tempVal < nightMin)
            {
                nightMin = tempVal;
            }

            if (tempVal > thresholds[0])
            {
                hoursAbove20++;
                if (tempHc.Agreement is { } agreement)
                {
                    nightAgreements.Add(agreement);
                }
            }
        }

        if (nightHoursCount == 0 || nightMin >= double.MaxValue)
        {
            return Alert.None(AlertType.TropicalNight);
        }

        var confidence = nightAgreements.Count > 0
            ? Math.Round(nightAgreements.Average(), 3)
            : 0.0;

        var (severity, effectiveThreshold) = ThresholdClassifier.Classify(
            nightMin, thresholds, ScanDirection.Max, strict: true);

        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var attrs = new Dictionary<string, object?>
        {
            ["night_minimum"] = Math.Round(nightMin, 1),
            ["hours_above_20"] = hoursAbove20,
        };

        return new Alert(AlertType.TropicalNight, severity, confidence, attrs,
            TriggerValue: Math.Round(nightMin, 1),
            Threshold: effectiveThreshold);
    }

    public static Alert EvaluateHumidity(
        ConsensusSnapshot consensus, double[] thresholds)
    {
        if (thresholds.Length < 3)
        {
            return Alert.None(AlertType.Humidity);
        }

        var dewConsensus = FindParam(consensus.Hourly.Parameters, Dewpoint);
        var isDayConsensus = FindParam(consensus.Hourly.Parameters, IsDay);
        if (dewConsensus is null || isDayConsensus is null)
        {
            return Alert.None(AlertType.Humidity);
        }

        var maxDewpoint = double.MinValue;
        var dayAgreements = new List<double>();
        var dayHoursCount = 0;

        for (var h = 0; h <= MaxHorizonHours; h++)
        {
            var key = $"h{h}";
            var isDayHc = isDayConsensus.ByHorizon.GetValueOrDefault(key);
            if (isDayHc?.Median is not { } isDayVal || isDayVal <= 0.5)
            {
                continue;
            }

            dayHoursCount++;

            if (!dewConsensus.ByHorizon.TryGetValue(key, out var dewHc)
                || dewHc.Median is not { } dewVal)
            {
                continue;
            }

            if (dewVal > maxDewpoint)
            {
                maxDewpoint = dewVal;
            }

            if (dewVal >= thresholds[0] && dewHc.Agreement is { } agreement)
            {
                dayAgreements.Add(agreement);
            }
        }

        if (dayHoursCount == 0 || maxDewpoint <= double.MinValue)
        {
            return Alert.None(AlertType.Humidity);
        }

        var confidence = dayAgreements.Count > 0
            ? Math.Round(dayAgreements.Average(), 3)
            : 0.0;

        var (severity, effectiveThreshold) = ThresholdClassifier.Classify(
            maxDewpoint, thresholds, ScanDirection.Max);
        var comfortLevel = severity switch
        {
            AlertSeverity.Red => "tropical",
            AlertSeverity.Orange => "oppressive",
            AlertSeverity.Yellow => "muggy",
            _ => (string?)null,
        };

        if (confidence == 0.0)
        {
            severity = AlertSeverity.None;
        }

        var attrs = new Dictionary<string, object?>
        {
            ["max_dewpoint"] = Math.Round(maxDewpoint, 1),
            ["comfort_level"] = comfortLevel,
        };

        return new Alert(AlertType.Humidity, severity, confidence, attrs,
            TriggerValue: Math.Round(maxDewpoint, 1),
            Threshold: effectiveThreshold);
    }

    internal static ParameterConsensus? FindParam(
        IReadOnlyList<ParameterConsensus> parameters, ParameterDef param)
        => parameters.FirstOrDefault(p => p.Parameter == param);


    private static double? DailyMax(ConsensusSnapshot consensus, ParameterDef? param)
    {
        if (param is null || FindParam(consensus.Daily.Parameters, param) is not { } daily)
        {
            return null;
        }

        var scan = HorizonExtremeScanner.FindExtreme(
            daily.ByHorizon, MaxHorizonDays, ScanDirection.Max, prefix: 'd');
        return scan.Found ? scan.ExtremeValue : null;
    }

    private static double AverageRounded(IReadOnlyList<double> agreements) =>
        agreements.Count > 0 ? Math.Round(agreements.Average(), 3) : 0.0;

    private static int CountModelsAtPeak(ParameterConsensus param, int? targetHour)
    {
        if (targetHour is null)
        {
            return 0;
        }

        var key = $"h{targetHour.Value}";
        return param.ByHorizon.TryGetValue(key, out var hc) ? hc.AvailableModels.Count : 0;
    }

    private static int CountModelsAtMax(ParameterConsensus param, int maxHours) =>
        HorizonExtremeScanner.InWindow(param.ByHorizon, maxHours)
            .Select(e => e.Consensus.AvailableModels.Count)
            .DefaultIfEmpty(0)
            .Max();

    private static int CountModelsAtHorizon(ParameterConsensus param, int hour)
    {
        var key = $"h{hour}";
        return param.ByHorizon.TryGetValue(key, out var hc) ? hc.AvailableModels.Count : 0;
    }

    private static double AverageAgreementAbove(ParameterConsensus param, double threshold, int maxHours) =>
        AverageOrZero(HorizonExtremeScanner.InWindow(param.ByHorizon, maxHours)
            .Where(e => e.Consensus.Median is { } value && value >= threshold)
            .Select(e => e.Consensus.Agreement));

    private static double AverageAgreementAll(ParameterConsensus param, int maxHours) =>
        AverageOrZero(HorizonExtremeScanner.InWindow(param.ByHorizon, maxHours)
            .Select(e => e.Consensus.Agreement));

    private static double AverageOrZero(IEnumerable<double?> agreements)
    {
        var present = agreements.OfType<double>().ToList();
        return present.Count > 0 ? present.Average() : 0.0;
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
