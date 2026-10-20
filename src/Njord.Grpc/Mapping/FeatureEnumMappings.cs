using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class FeatureEnumMappings
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
}
