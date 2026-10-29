using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;


namespace Njord.Grpc.Mapping;

internal static class AlertResultMappingExtensions
{
    public static V2.AlertData ToData(this AlertFeatureResult r)
    {
        var proto = new V2.AlertData
        {
            Severity = r.Severity.ToProto(),
            Confidence = r.Confidence,
            TriggerValue = r.TriggerValue,
            Threshold = r.Threshold,
            ConfidenceLevel = r.Level.ToProto(),
        };
        if (r.PeakValue is { } peakValue)
        {
            proto.PeakValue = peakValue;
        }

        if (r.HoursUntil is { } hoursUntil)
        {
            proto.HoursUntil = hoursUntil;
        }

        if (r.DurationHours is { } durationHours)
        {
            proto.DurationHours = durationHours;
        }

        return proto;
    }

    public static V2.FrostAlert ToProto(this FrostAlertResult r)
    {
        var proto = new V2.FrostAlert
        {
            Alert = r.ToData(),
            ExpectedLow = r.ExpectedLow,
        };
        if (r.EarliestFrostHours is { } earliestFrostHours)
        {
            proto.EarliestFrostHours = earliestFrostHours;
        }
        return proto;
    }

    public static V2.HeatAlert ToProto(this HeatAlertResult r)
    {
        return new V2.HeatAlert
        {
            Alert = r.ToData(),
            ExpectedMax = r.ExpectedMax,
        };
    }

    public static V2.StormAlert ToProto(this StormAlertResult r)
    {
        return new V2.StormAlert
        {
            Alert = r.ToData(),
            ExpectedMaxGust = r.ExpectedMaxGust,
        };
    }

    public static V2.HeavyRainAlert ToProto(this HeavyRainAlertResult r)
    {
        return new V2.HeavyRainAlert
        {
            Alert = r.ToData(),
            HourlyExceedModels = r.HourlyExceedModels,
            DailyExceedModels = r.DailyExceedModels,
        };
    }

    public static V2.UvAlert ToProto(this UvAlertResult r)
    {
        return new V2.UvAlert
        {
            Alert = r.ToData(),
            UvLevel = r.UvLevel.ToProto(),
            UvIndex = r.UvIndex,
        };
    }

    public static V2.FogAlert ToProto(this FogAlertResult r)
    {
        return new V2.FogAlert
        {
            Alert = r.ToData(),
            FogHours = r.FogHours,
        };
    }

    public static V2.SnowAlert ToProto(this SnowAlertResult r)
    {
        var proto = new V2.SnowAlert
        {
            Alert = r.ToData(),
            ExpectedAccumulation = r.ExpectedAccumulation,
        };
        if (r.FreezingLevel is { } freezingLevel)
        {
            proto.FreezingLevel = freezingLevel;
        }
        return proto;
    }

    public static V2.PressureDropAlert ToProto(this PressureDropAlertResult r)
    {
        return new V2.PressureDropAlert
        {
            Alert = r.ToData(),
            MaxDrop = r.MaxDrop,
        };
    }

    public static V2.ThunderstormAlert ToProto(this ThunderstormAlertResult r)
    {
        return new V2.ThunderstormAlert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.IceAlert ToProto(this IceAlertResult r)
    {
        return new V2.IceAlert
        {
            Alert = r.ToData(),
            ExpectedLow = r.ExpectedLow,
            RainHours = r.RainHours,
            SoilFrozen = r.SoilFrozen,
        };
    }

    public static V2.WindChillAlert ToProto(this WindChillAlertResult r)
    {
        var proto = new V2.WindChillAlert
        {
            Alert = r.ToData(),
            FeltTemperature = r.FeltTemperature,
            WindFactor = r.WindFactor,
        };
        if (r.ExposureRisk is { } exposureRisk)
        {
            proto.ExposureRisk = exposureRisk.ToProto();
        }
        return proto;
    }

    public static V2.VisibilityAlert ToProto(this VisibilityAlertResult r)
    {
        return new V2.VisibilityAlert
        {
            Alert = r.ToData(),
            MinVisibility = r.MinVisibility,
            HoursBelow1000M = r.HoursBelow1000m,
        };
    }

    public static V2.TropicalNightAlert ToProto(this TropicalNightAlertResult r)
    {
        return new V2.TropicalNightAlert
        {
            Alert = r.ToData(),
            NightMinimum = r.NightMinimum,
            HoursAbove20 = r.HoursAbove20,
        };
    }

    public static V2.HumidityAlert ToProto(this HumidityAlertResult r)
    {
        var proto = new V2.HumidityAlert
        {
            Alert = r.ToData(),
            MaxDewpoint = r.MaxDewpoint,
        };
        if (r.ComfortLevel is { } comfortLevel)
        {
            proto.ComfortLevel = comfortLevel.ToProto();
        }
        return proto;
    }

    public static V2.PollenAlert ToProto(this PollenAlertResult r)
    {
        return new V2.PollenAlert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.Pm25Alert ToProto(this Pm25AlertResult r)
    {
        return new V2.Pm25Alert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.DustAlert ToProto(this DustAlertResult r)
    {
        return new V2.DustAlert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.FloodRiskAlert ToProto(this FloodRiskAlertResult r)
    {
        return new V2.FloodRiskAlert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.FloodRainAlert ToProto(this FloodRainAlertResult r)
    {
        return new V2.FloodRainAlert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.SwimmingConditionsAlert ToProto(this SwimmingConditionsAlertResult r)
    {
        return new V2.SwimmingConditionsAlert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.RainProbabilityAlert ToProto(this RainProbabilityAlertResult r)
    {
        return new V2.RainProbabilityAlert
        {
            Alert = r.ToData(),
            PeakProbability = r.PeakProbability,
        };
    }

    public static V2.WetBulbDangerAlert ToProto(this WetBulbDangerAlertResult r)
    {
        return new V2.WetBulbDangerAlert
        {
            Alert = r.ToData(),
            PeakWetBulb = r.PeakWetBulb,
        };
    }

    public static V2.ClearSkyUvAlert ToProto(this ClearSkyUvAlertResult r)
    {
        return new V2.ClearSkyUvAlert
        {
            Alert = r.ToData(),
            PeakClearSkyUv = r.PeakClearSkyUv,
        };
    }

    public static V2.ShowerAlert ToProto(this ShowerAlertResult r)
    {
        return new V2.ShowerAlert
        {
            Alert = r.ToData(),
            PeakIntensity = r.PeakIntensity,
        };
    }

    public static V2.CompoundHeatStressAlert ToProto(this CompoundHeatStressAlertResult r)
    {
        return new V2.CompoundHeatStressAlert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.MigraineRiskAlert ToProto(this MigraineRiskAlertResult r)
    {
        return new V2.MigraineRiskAlert
        {
            Alert = r.ToData(),
        };
    }

    public static V2.SmogRiskAlert ToProto(this SmogRiskAlertResult r)
    {
        return new V2.SmogRiskAlert
        {
            Alert = r.ToData(),
            BoundaryLayerHeight = r.BoundaryLayerHeight,
            Pm25 = r.Pm25,
        };
    }

    public static V2.FireRiskAlert ToProto(this FireRiskAlertResult r)
    {
        return new V2.FireRiskAlert
        {
            Alert = r.ToData(),
            RiskScore = r.RiskScore,
        };
    }

    public static V2.PollenBlastAlert ToProto(this PollenBlastAlertResult r)
    {
        return new V2.PollenBlastAlert
        {
            Alert = r.ToData(),
            PeakPollen = r.PeakPollen,
        };
    }

    public static V2.CoastalFloodAlert ToProto(this CoastalFloodAlertResult r)
    {
        var proto = new V2.CoastalFloodAlert
        {
            Alert = r.ToData(),
            SeaLevelDeviation = r.SeaLevelDeviation,
        };
        if (r.MaxWaveHeight is { } wh)
        {
            proto.MaxWaveHeight = wh;
        }

        return proto;
    }
}
