using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class FeatureResultMappingExtensions
{
    public static V2.ScoreData ToData(this ScoreFeatureResult r) => new()
    {
        Score = r.Score,
        BestStart = r.BestStart.ToTimestamp(),
        BestHours = r.BestHours,
    };

    public static V2.Bbq ToProto(this BbqScoreResult r)
    {
        return new V2.Bbq
        {
            Score = r.ToData(),
        };
    }

    public static V2.Laundry ToProto(this LaundryScoreResult r)
    {
        return new V2.Laundry
        {
            Score = r.ToData(),
        };
    }

    public static V2.Outdoor ToProto(this OutdoorScoreResult r)
    {
        return new V2.Outdoor
        {
            Score = r.ToData(),
        };
    }

    public static V2.SurfConditions ToProto(this SurfConditionsResult r)
    {
        return new V2.SurfConditions
        {
            Score = r.ToData(),
        };
    }

    public static V2.CommuteSafety ToProto(this CommuteSafetyResult r)
    {
        var proto = new V2.CommuteSafety
        {
            Condition = new V2.ConditionData(),
            Level = r.Level.ToProto(),
        };
        proto.Hazards.AddRange(r.Hazards.Select(item => item.ToProto()));
        return proto;
    }

    public static V2.VentilationWindow ToProto(this VentilationWindowResult r)
    {
        return new V2.VentilationWindow
        {
            Condition = new V2.ConditionData(),
            BestStart = r.BestStart.ToTimestamp(),
            BestEnd = r.BestEnd.ToTimestamp(),
        };
    }

    public static V2.NightVentilation ToProto(this NightVentilationResult r)
    {
        return new V2.NightVentilation
        {
            Condition = new V2.ConditionData(),
            OpenAt = r.OpenAt.ToTimestamp(),
            CloseAt = r.CloseAt.ToTimestamp(),
            LowTemperature = r.LowTemperature,
            TomorrowPeak = r.TomorrowPeak,
        };
    }

    public static V2.ExerciseAdvisory ToProto(this ExerciseAdvisoryResult r)
    {
        var proto = new V2.ExerciseAdvisory
        {
            Condition = new V2.ConditionData(),
            Level = r.Level.ToProto(),
            BestWindow = r.BestWindow.ToTimestamp(),
        };
        proto.Reasons.AddRange(r.Reasons.Select(item => item.ToProto()));
        return proto;
    }

    public static V2.Gardening ToProto(this GardeningResult r)
    {
        return new V2.Gardening
        {
            Condition = new V2.ConditionData(),
            Advice = r.Advice.ToProto(),
            PlantingSafe = r.PlantingSafe,
            WateringNeeded = r.WateringNeeded,
            FrostRisk = r.FrostRisk,
        };
    }

    public static V2.FloodTimeline ToProto(this FloodTimelineResult r)
    {
        return new V2.FloodTimeline
        {
            Condition = new V2.ConditionData(),
            Status = r.Status.ToProto(),
            PeakTime = r.PeakTime.ToTimestamp(),
            PeakDischarge = r.PeakDischarge,
            DaysAboveWarning = r.DaysAboveWarning,
        };
    }

    public static V2.SolarYield ToProto(this SolarYieldResult r)
    {
        return new V2.SolarYield
        {
            Condition = new V2.ConditionData(),
            KwhPerKwp = r.KwhPerKwp,
            PeakHour = r.PeakHour,
        };
    }

    public static V2.SatelliteRadiation ToProto(this SatelliteRadiationResult r)
    {
        var proto = new V2.SatelliteRadiation
        {
            Condition = new V2.ConditionData(),
            ObservedAt = r.ObservedAt.ToTimestamp(),
            Gti = r.Gti,
        };
        if (r.Ghi is { } ghi)
        {
            proto.Ghi = ghi;
        }
        if (r.BlendedKwhPerSquareMeter is { } blendedKwhPerSquareMeter)
        {
            proto.BlendedKwhPerSquareMeter = blendedKwhPerSquareMeter;
        }
        return proto;
    }

    public static V2.SnowDepthWatchCondition ToProto(this SnowDepthWatchResult r)
    {
        return new V2.SnowDepthWatchCondition
        {
            Condition = new V2.ConditionData(),
            Level = r.Level switch
            {
                SnowDepthLevel.Light => V2.SnowDepthLevel.Light,
                SnowDepthLevel.Moderate => V2.SnowDepthLevel.Moderate,
                SnowDepthLevel.Heavy => V2.SnowDepthLevel.Heavy,
                SnowDepthLevel.Extreme => V2.SnowDepthLevel.Extreme,
                _ => V2.SnowDepthLevel.Unspecified,
            },
            DepthCm = r.DepthCm,
        };
    }

    public static V2.SoilMoistureCondition ToProto(this SoilMoistureResult r)
    {
        return new V2.SoilMoistureCondition
        {
            Condition = new V2.ConditionData(),
            Level = r.Level switch
            {
                SoilMoistureLevel.Dry => V2.SoilMoistureLevel.Dry,
                SoilMoistureLevel.Optimal => V2.SoilMoistureLevel.Optimal,
                SoilMoistureLevel.Wet => V2.SoilMoistureLevel.Wet,
                _ => V2.SoilMoistureLevel.Unspecified,
            },
            Moisture = r.Moisture,
        };
    }

    public static V2.EvapotranspirationCondition ToProto(this EvapotranspirationResult r)
    {
        return new V2.EvapotranspirationCondition
        {
            Condition = new V2.ConditionData(),
            Level = r.Level switch
            {
                EvapotranspirationLevel.Low => V2.EvapotranspirationLevel.Low,
                EvapotranspirationLevel.Moderate => V2.EvapotranspirationLevel.Moderate,
                EvapotranspirationLevel.High => V2.EvapotranspirationLevel.High,
                _ => V2.EvapotranspirationLevel.Unspecified,
            },
            DailyMm = r.DailyMm,
        };
    }

    public static V2.RagweedPollenCondition ToProto(this RagweedPollenResult r)
    {
        return new V2.RagweedPollenCondition
        {
            Condition = new V2.ConditionData(),
            PeakValue = r.PeakValue,
        };
    }

    public static V2.GranularAqiCondition ToProto(this GranularAqiResult r)
    {
        var proto = new V2.GranularAqiCondition { Condition = new V2.ConditionData() };
        if (r.Pm25Aqi is { } pm25) { proto.Pm25Aqi = pm25; }
        if (r.Pm10Aqi is { } pm10) { proto.Pm10Aqi = pm10; }
        if (r.No2Aqi is { } no2) { proto.No2Aqi = no2; }
        if (r.OzoneAqi is { } ozone) { proto.OzoneAqi = ozone; }
        if (r.So2Aqi is { } so2) { proto.So2Aqi = so2; }
        return proto;
    }

    public static V2.OceanCurrentsCondition ToProto(this OceanCurrentsResult r)
    {
        var proto = new V2.OceanCurrentsCondition
        {
            Condition = new V2.ConditionData(),
            Strength = r.Strength switch
            {
                CurrentStrength.Weak => V2.CurrentStrength.Weak,
                CurrentStrength.Moderate => V2.CurrentStrength.Moderate,
                CurrentStrength.Strong => V2.CurrentStrength.Strong,
                CurrentStrength.VeryStrong => V2.CurrentStrength.VeryStrong,
                _ => V2.CurrentStrength.Unspecified,
            },
            PeakVelocityKmh = r.PeakVelocityKmh,
        };
        if (r.Direction is { } dir) { proto.Direction = dir; }
        return proto;
    }

    public static V2.ApproachingFront ToProto(this ApproachingFrontResult r)
    {
        return new V2.ApproachingFront
        {
            Condition = new V2.ConditionData(),
            EstimatedInHours = r.EstimatedInHours,
            PressureDrop = r.PressureDrop,
            TemperatureDrop = r.TemperatureDrop,
        };
    }

    public static V2.ConfidenceTrend ToProto(this ConfidenceTrendResult r)
    {
        return new V2.ConfidenceTrend
        {
            Trend = new V2.TrendData(),
            Direction = r.Direction.ToProto(),
            Magnitude = r.Magnitude,
            CurrentAvgStdDev = r.CurrentAvgStdDev,
            PreviousAvgStdDev = r.PreviousAvgStdDev,
        };
    }

    public static V2.PollenTrend ToProto(this PollenTrendResult r)
    {
        var proto = new V2.PollenTrend
        {
            Trend = new V2.TrendData(),
        };
        if (r.PeakType is { } peakType)
        {
            proto.PeakType = peakType.ToProto();
        }
        if (r.PeakTime is { } peakTime)
        {
            proto.PeakTime = peakTime.ToTimestamp();
        }
        if (r.Birch is { } birch)
        {
            proto.Birch = birch.ToProto();
        }
        if (r.Grass is { } grass)
        {
            proto.Grass = grass.ToProto();
        }
        if (r.Alder is { } alder)
        {
            proto.Alder = alder.ToProto();
        }
        if (r.Mugwort is { } mugwort)
        {
            proto.Mugwort = mugwort.ToProto();
        }
        return proto;
    }

    public static V2.SeasonalMonthEntry ToProto(this SeasonalMonth month) => new()
    {
        Year = month.Month.Year,
        Month = month.Month.Month,
        TemperatureAnomaly = month.TemperatureAnomaly,
        PrecipitationAnomalyPercent = month.PrecipitationAnomalyPercent,
        Confidence = month.Confidence.ToProto(),
    };

    public static V2.SeasonalOutlook ToProto(this SeasonalOutlookResult r)
    {
        var proto = new V2.SeasonalOutlook { Forecast = new V2.ForecastData() };
        proto.Months.AddRange(r.Months.Select(month => month.ToProto()));
        return proto;
    }

    public static V2.ModelDayValues ToProto(this ModelDayValues values)
    {
        var proto = new V2.ModelDayValues { Model = values.ModelId };
        if (values.MaxTemperature is { } max)
        {
            proto.MaxTemperature = max;
        }

        if (values.MinTemperature is { } min)
        {
            proto.MinTemperature = min;
        }

        if (values.Precipitation is { } precipitation)
        {
            proto.Precipitation = precipitation;
        }

        return proto;
    }

    public static V2.ModelComparisonDay ToProto(this ModelComparisonDay day)
    {
        var proto = new V2.ModelComparisonDay { Year = day.Date.Year, Month = day.Date.Month, Day = day.Date.Day };
        proto.Models.AddRange(day.Models.Select(m => m.ToProto()));
        return proto;
    }

    public static V2.ModelComparison ToProto(this ModelComparisonResult r)
    {
        var proto = new V2.ModelComparison { Forecast = new V2.ForecastData() };
        proto.Days.AddRange(r.Days.Select(day => day.ToProto()));
        return proto;
    }

    public static V2.GrowingDegreeDayEntry ToProto(this GrowingDegreeDay day) => new()
    {
        Year = day.Date.Year,
        Month = day.Date.Month,
        Day = day.Date.Day,
        Gdd = day.Gdd,
    };

    public static V2.GrowingDegreeForecast ToProto(this GrowingDegreeResult r)
    {
        var proto = new V2.GrowingDegreeForecast { Forecast = new V2.ForecastData(), CumulativeGdd = r.CumulativeGdd };
        proto.Days.AddRange(r.Days.Select(day => day.ToProto()));
        return proto;
    }
}
