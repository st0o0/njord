using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class AlertResultMappings
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
}
