using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class FeatureEnumMappingExtensions
{
    public static V2.ConfidenceLevel ToProto(this ConfidenceLevel confidenceLevel) => confidenceLevel switch
    {
        ConfidenceLevel.Low => V2.ConfidenceLevel.Low,
        ConfidenceLevel.Medium => V2.ConfidenceLevel.Medium,
        ConfidenceLevel.High => V2.ConfidenceLevel.High,
        _ => throw new ArgumentOutOfRangeException(nameof(confidenceLevel), confidenceLevel, null),
    };

    public static V2.UvLevel ToProto(this UvLevel uvLevel) => uvLevel switch
    {
        UvLevel.Moderate => V2.UvLevel.Moderate,
        UvLevel.High => V2.UvLevel.High,
        UvLevel.VeryHigh => V2.UvLevel.VeryHigh,
        UvLevel.Extreme => V2.UvLevel.Extreme,
        _ => throw new ArgumentOutOfRangeException(nameof(uvLevel), uvLevel, null),
    };

    public static V2.ExposureRisk ToProto(this ExposureRisk exposureRisk) => exposureRisk switch
    {
        ExposureRisk.Frostbite30Minutes => V2.ExposureRisk.Frostbite30Minutes,
        ExposureRisk.Frostbite10Minutes => V2.ExposureRisk.Frostbite10Minutes,
        _ => throw new ArgumentOutOfRangeException(nameof(exposureRisk), exposureRisk, null),
    };

    public static V2.ComfortLevel ToProto(this ComfortLevel comfortLevel) => comfortLevel switch
    {
        ComfortLevel.Muggy => V2.ComfortLevel.Muggy,
        ComfortLevel.Oppressive => V2.ComfortLevel.Oppressive,
        ComfortLevel.Tropical => V2.ComfortLevel.Tropical,
        _ => throw new ArgumentOutOfRangeException(nameof(comfortLevel), comfortLevel, null),
    };

    public static V2.CommuteLevel ToProto(this CommuteLevel commuteLevel) => commuteLevel switch
    {
        CommuteLevel.Safe => V2.CommuteLevel.Safe,
        CommuteLevel.Caution => V2.CommuteLevel.Caution,
        CommuteLevel.Dangerous => V2.CommuteLevel.Dangerous,
        _ => throw new ArgumentOutOfRangeException(nameof(commuteLevel), commuteLevel, null),
    };

    public static V2.CommuteHazard ToProto(this CommuteHazard commuteHazard) => commuteHazard switch
    {
        CommuteHazard.Ice => V2.CommuteHazard.Ice,
        CommuteHazard.Fog => V2.CommuteHazard.Fog,
        CommuteHazard.HeavyRain => V2.CommuteHazard.HeavyRain,
        CommuteHazard.StrongWind => V2.CommuteHazard.StrongWind,
        _ => throw new ArgumentOutOfRangeException(nameof(commuteHazard), commuteHazard, null),
    };

    public static V2.ExerciseLevel ToProto(this ExerciseLevel exerciseLevel) => exerciseLevel switch
    {
        ExerciseLevel.Safe => V2.ExerciseLevel.Safe,
        ExerciseLevel.Caution => V2.ExerciseLevel.Caution,
        ExerciseLevel.Avoid => V2.ExerciseLevel.Avoid,
        _ => throw new ArgumentOutOfRangeException(nameof(exerciseLevel), exerciseLevel, null),
    };

    public static V2.ExerciseFactor ToProto(this ExerciseFactor exerciseFactor) => exerciseFactor switch
    {
        ExerciseFactor.Pm25 => V2.ExerciseFactor.Pm25,
        ExerciseFactor.Ozone => V2.ExerciseFactor.Ozone,
        ExerciseFactor.Temperature => V2.ExerciseFactor.Temperature,
        ExerciseFactor.Humidity => V2.ExerciseFactor.Humidity,
        ExerciseFactor.Uv => V2.ExerciseFactor.Uv,
        _ => throw new ArgumentOutOfRangeException(nameof(exerciseFactor), exerciseFactor, null),
    };

    public static V2.GardeningAdvice ToProto(this GardeningAdvice gardeningAdvice) => gardeningAdvice switch
    {
        GardeningAdvice.Plant => V2.GardeningAdvice.Plant,
        GardeningAdvice.Hold => V2.GardeningAdvice.Hold,
        _ => throw new ArgumentOutOfRangeException(nameof(gardeningAdvice), gardeningAdvice, null),
    };

    public static V2.FloodStatus ToProto(this FloodStatus floodStatus) => floodStatus switch
    {
        FloodStatus.Normal => V2.FloodStatus.Normal,
        FloodStatus.AboveWarning => V2.FloodStatus.AboveWarning,
        _ => throw new ArgumentOutOfRangeException(nameof(floodStatus), floodStatus, null),
    };

    public static V2.ConfidenceTrendDirection ToProto(this ConfidenceTrendDirection confidenceTrendDirection) => confidenceTrendDirection switch
    {
        ConfidenceTrendDirection.Converging => V2.ConfidenceTrendDirection.Converging,
        ConfidenceTrendDirection.Stable => V2.ConfidenceTrendDirection.Stable,
        ConfidenceTrendDirection.Diverging => V2.ConfidenceTrendDirection.Diverging,
        _ => throw new ArgumentOutOfRangeException(nameof(confidenceTrendDirection), confidenceTrendDirection, null),
    };

    public static V2.PollenType ToProto(this PollenType pollenType) => pollenType switch
    {
        PollenType.Birch => V2.PollenType.Birch,
        PollenType.Grass => V2.PollenType.Grass,
        PollenType.Alder => V2.PollenType.Alder,
        PollenType.Mugwort => V2.PollenType.Mugwort,
        _ => throw new ArgumentOutOfRangeException(nameof(pollenType), pollenType, null),
    };

    public static V2.PollenTrendDirection ToProto(this PollenTrendDirection pollenTrendDirection) => pollenTrendDirection switch
    {
        PollenTrendDirection.Stable => V2.PollenTrendDirection.Stable,
        PollenTrendDirection.Rising => V2.PollenTrendDirection.Rising,
        PollenTrendDirection.Falling => V2.PollenTrendDirection.Falling,
        _ => throw new ArgumentOutOfRangeException(nameof(pollenTrendDirection), pollenTrendDirection, null),
    };

    public static V2.HeatPumpRecommendation ToProto(this HeatPumpRecommendation value) => value switch
    {
        HeatPumpRecommendation.Ideal => V2.HeatPumpRecommendation.Ideal,
        HeatPumpRecommendation.Normal => V2.HeatPumpRecommendation.Normal,
        HeatPumpRecommendation.Inefficient => V2.HeatPumpRecommendation.Inefficient,
        HeatPumpRecommendation.Avoid => V2.HeatPumpRecommendation.Avoid,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static V2.BatteryStrategy ToProto(this BatteryStrategy value) => value switch
    {
        BatteryStrategy.FeedIn => V2.BatteryStrategy.FeedIn,
        BatteryStrategy.ChargeBattery => V2.BatteryStrategy.ChargeBattery,
        BatteryStrategy.Conserve => V2.BatteryStrategy.Conserve,
        BatteryStrategy.Normal => V2.BatteryStrategy.Normal,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static V2.IrrigationAdvice ToProto(this IrrigationAdvice value) => value switch
    {
        IrrigationAdvice.Water => V2.IrrigationAdvice.Water,
        IrrigationAdvice.Skip => V2.IrrigationAdvice.Skip,
        IrrigationAdvice.Reduce => V2.IrrigationAdvice.Reduce,
        IrrigationAdvice.SoilWet => V2.IrrigationAdvice.SoilWet,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static V2.FrostDepthRisk ToProto(this FrostDepthRisk value) => value switch
    {
        FrostDepthRisk.None => V2.FrostDepthRisk.None,
        FrostDepthRisk.Surface => V2.FrostDepthRisk.Surface,
        FrostDepthRisk.Shallow => V2.FrostDepthRisk.Shallow,
        FrostDepthRisk.Moderate => V2.FrostDepthRisk.Moderate,
        FrostDepthRisk.Deep => V2.FrostDepthRisk.Deep,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static V2.InversionStrength ToProto(this InversionStrength value) => value switch
    {
        InversionStrength.None => V2.InversionStrength.None,
        InversionStrength.Mild => V2.InversionStrength.Mild,
        InversionStrength.Strong => V2.InversionStrength.Strong,
        InversionStrength.Extreme => V2.InversionStrength.Extreme,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static V2.InversionType ToProto(this InversionType value) => value switch
    {
        InversionType.Radiation => V2.InversionType.Radiation,
        InversionType.Subsidence => V2.InversionType.Subsidence,
        InversionType.Frontal => V2.InversionType.Frontal,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static V2.StabilityClass ToProto(this StabilityClass value) => value switch
    {
        StabilityClass.Stable => V2.StabilityClass.Stable,
        StabilityClass.Marginal => V2.StabilityClass.Marginal,
        StabilityClass.Capped => V2.StabilityClass.Capped,
        StabilityClass.Unstable => V2.StabilityClass.Unstable,
        StabilityClass.Explosive => V2.StabilityClass.Explosive,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static V2.CeilingCategory ToProto(this CeilingCategory value) => value switch
    {
        CeilingCategory.VeryLow => V2.CeilingCategory.VeryLow,
        CeilingCategory.Low => V2.CeilingCategory.Low,
        CeilingCategory.Moderate => V2.CeilingCategory.Moderate,
        CeilingCategory.High => V2.CeilingCategory.High,
        CeilingCategory.VeryHigh => V2.CeilingCategory.VeryHigh,
        CeilingCategory.Unlimited => V2.CeilingCategory.Unlimited,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
