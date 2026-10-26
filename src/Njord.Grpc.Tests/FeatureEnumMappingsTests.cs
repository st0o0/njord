using Njord.Grpc.Mapping;

namespace Njord.Grpc.Tests;

public sealed class FeatureEnumMappingsTests
{
    [Fact]
    public void ConfidenceLevel_Low_maps_to_its_proto_member() =>
        Assert.Equal(V2.ConfidenceLevel.Low, Messages.Alerts.ConfidenceLevel.Low.ToProto());

    [Fact]
    public void ConfidenceLevel_Medium_maps_to_its_proto_member() =>
        Assert.Equal(V2.ConfidenceLevel.Medium, Messages.Alerts.ConfidenceLevel.Medium.ToProto());

    [Fact]
    public void ConfidenceLevel_High_maps_to_its_proto_member() =>
        Assert.Equal(V2.ConfidenceLevel.High, Messages.Alerts.ConfidenceLevel.High.ToProto());

    [Fact]
    public void UvLevel_Moderate_maps_to_its_proto_member() =>
        Assert.Equal(V2.UvLevel.Moderate, Messages.Pipeline.UvLevel.Moderate.ToProto());

    [Fact]
    public void UvLevel_High_maps_to_its_proto_member() =>
        Assert.Equal(V2.UvLevel.High, Messages.Pipeline.UvLevel.High.ToProto());

    [Fact]
    public void UvLevel_VeryHigh_maps_to_its_proto_member() =>
        Assert.Equal(V2.UvLevel.VeryHigh, Messages.Pipeline.UvLevel.VeryHigh.ToProto());

    [Fact]
    public void UvLevel_Extreme_maps_to_its_proto_member() =>
        Assert.Equal(V2.UvLevel.Extreme, Messages.Pipeline.UvLevel.Extreme.ToProto());

    [Fact]
    public void ExposureRisk_Frostbite30Minutes_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExposureRisk.Frostbite30Minutes, Messages.Pipeline.ExposureRisk.Frostbite30Minutes.ToProto());

    [Fact]
    public void ExposureRisk_Frostbite10Minutes_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExposureRisk.Frostbite10Minutes, Messages.Pipeline.ExposureRisk.Frostbite10Minutes.ToProto());

    [Fact]
    public void ComfortLevel_Muggy_maps_to_its_proto_member() =>
        Assert.Equal(V2.ComfortLevel.Muggy, Messages.Pipeline.ComfortLevel.Muggy.ToProto());

    [Fact]
    public void ComfortLevel_Oppressive_maps_to_its_proto_member() =>
        Assert.Equal(V2.ComfortLevel.Oppressive, Messages.Pipeline.ComfortLevel.Oppressive.ToProto());

    [Fact]
    public void ComfortLevel_Tropical_maps_to_its_proto_member() =>
        Assert.Equal(V2.ComfortLevel.Tropical, Messages.Pipeline.ComfortLevel.Tropical.ToProto());

    [Fact]
    public void CommuteLevel_Safe_maps_to_its_proto_member() =>
        Assert.Equal(V2.CommuteLevel.Safe, Messages.Pipeline.CommuteLevel.Safe.ToProto());

    [Fact]
    public void CommuteLevel_Caution_maps_to_its_proto_member() =>
        Assert.Equal(V2.CommuteLevel.Caution, Messages.Pipeline.CommuteLevel.Caution.ToProto());

    [Fact]
    public void CommuteLevel_Dangerous_maps_to_its_proto_member() =>
        Assert.Equal(V2.CommuteLevel.Dangerous, Messages.Pipeline.CommuteLevel.Dangerous.ToProto());

    [Fact]
    public void CommuteHazard_Ice_maps_to_its_proto_member() =>
        Assert.Equal(V2.CommuteHazard.Ice, Messages.Pipeline.CommuteHazard.Ice.ToProto());

    [Fact]
    public void CommuteHazard_Fog_maps_to_its_proto_member() =>
        Assert.Equal(V2.CommuteHazard.Fog, Messages.Pipeline.CommuteHazard.Fog.ToProto());

    [Fact]
    public void CommuteHazard_HeavyRain_maps_to_its_proto_member() =>
        Assert.Equal(V2.CommuteHazard.HeavyRain, Messages.Pipeline.CommuteHazard.HeavyRain.ToProto());

    [Fact]
    public void CommuteHazard_StrongWind_maps_to_its_proto_member() =>
        Assert.Equal(V2.CommuteHazard.StrongWind, Messages.Pipeline.CommuteHazard.StrongWind.ToProto());

    [Fact]
    public void ExerciseLevel_Safe_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExerciseLevel.Safe, Messages.Pipeline.ExerciseLevel.Safe.ToProto());

    [Fact]
    public void ExerciseLevel_Caution_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExerciseLevel.Caution, Messages.Pipeline.ExerciseLevel.Caution.ToProto());

    [Fact]
    public void ExerciseLevel_Avoid_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExerciseLevel.Avoid, Messages.Pipeline.ExerciseLevel.Avoid.ToProto());

    [Fact]
    public void ExerciseFactor_Pm25_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExerciseFactor.Pm25, Messages.Pipeline.ExerciseFactor.Pm25.ToProto());

    [Fact]
    public void ExerciseFactor_Ozone_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExerciseFactor.Ozone, Messages.Pipeline.ExerciseFactor.Ozone.ToProto());

    [Fact]
    public void ExerciseFactor_Temperature_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExerciseFactor.Temperature, Messages.Pipeline.ExerciseFactor.Temperature.ToProto());

    [Fact]
    public void ExerciseFactor_Humidity_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExerciseFactor.Humidity, Messages.Pipeline.ExerciseFactor.Humidity.ToProto());

    [Fact]
    public void ExerciseFactor_Uv_maps_to_its_proto_member() =>
        Assert.Equal(V2.ExerciseFactor.Uv, Messages.Pipeline.ExerciseFactor.Uv.ToProto());

    [Fact]
    public void GardeningAdvice_Plant_maps_to_its_proto_member() =>
        Assert.Equal(V2.GardeningAdvice.Plant, Messages.Pipeline.GardeningAdvice.Plant.ToProto());

    [Fact]
    public void GardeningAdvice_Hold_maps_to_its_proto_member() =>
        Assert.Equal(V2.GardeningAdvice.Hold, Messages.Pipeline.GardeningAdvice.Hold.ToProto());

    [Fact]
    public void FloodStatus_Normal_maps_to_its_proto_member() =>
        Assert.Equal(V2.FloodStatus.Normal, Messages.Pipeline.FloodStatus.Normal.ToProto());

    [Fact]
    public void FloodStatus_AboveWarning_maps_to_its_proto_member() =>
        Assert.Equal(V2.FloodStatus.AboveWarning, Messages.Pipeline.FloodStatus.AboveWarning.ToProto());

    [Fact]
    public void ConfidenceTrendDirection_Converging_maps_to_its_proto_member() =>
        Assert.Equal(V2.ConfidenceTrendDirection.Converging, Messages.Pipeline.ConfidenceTrendDirection.Converging.ToProto());

    [Fact]
    public void ConfidenceTrendDirection_Stable_maps_to_its_proto_member() =>
        Assert.Equal(V2.ConfidenceTrendDirection.Stable, Messages.Pipeline.ConfidenceTrendDirection.Stable.ToProto());

    [Fact]
    public void ConfidenceTrendDirection_Diverging_maps_to_its_proto_member() =>
        Assert.Equal(V2.ConfidenceTrendDirection.Diverging, Messages.Pipeline.ConfidenceTrendDirection.Diverging.ToProto());

    [Fact]
    public void PollenType_Birch_maps_to_its_proto_member() =>
        Assert.Equal(V2.PollenType.Birch, Messages.Pipeline.PollenType.Birch.ToProto());

    [Fact]
    public void PollenType_Grass_maps_to_its_proto_member() =>
        Assert.Equal(V2.PollenType.Grass, Messages.Pipeline.PollenType.Grass.ToProto());

    [Fact]
    public void PollenType_Alder_maps_to_its_proto_member() =>
        Assert.Equal(V2.PollenType.Alder, Messages.Pipeline.PollenType.Alder.ToProto());

    [Fact]
    public void PollenType_Mugwort_maps_to_its_proto_member() =>
        Assert.Equal(V2.PollenType.Mugwort, Messages.Pipeline.PollenType.Mugwort.ToProto());

    [Fact]
    public void PollenTrendDirection_Stable_maps_to_its_proto_member() =>
        Assert.Equal(V2.PollenTrendDirection.Stable, Messages.Pipeline.PollenTrendDirection.Stable.ToProto());

    [Fact]
    public void PollenTrendDirection_Rising_maps_to_its_proto_member() =>
        Assert.Equal(V2.PollenTrendDirection.Rising, Messages.Pipeline.PollenTrendDirection.Rising.ToProto());

    [Fact]
    public void PollenTrendDirection_Falling_maps_to_its_proto_member() =>
        Assert.Equal(V2.PollenTrendDirection.Falling, Messages.Pipeline.PollenTrendDirection.Falling.ToProto());

    [Fact]
    public void AlertSeverity_None_maps_to_its_proto_member() =>
        Assert.Equal(V2.AlertSeverity.None, Messages.Alerts.AlertSeverity.None.ToProto());

    [Fact]
    public void AlertSeverity_Yellow_maps_to_its_proto_member() =>
        Assert.Equal(V2.AlertSeverity.Yellow, Messages.Alerts.AlertSeverity.Yellow.ToProto());

    [Fact]
    public void AlertSeverity_Orange_maps_to_its_proto_member() =>
        Assert.Equal(V2.AlertSeverity.Orange, Messages.Alerts.AlertSeverity.Orange.ToProto());

    [Fact]
    public void AlertSeverity_Red_maps_to_its_proto_member() =>
        Assert.Equal(V2.AlertSeverity.Red, Messages.Alerts.AlertSeverity.Red.ToProto());

    [Fact]
    public void Unknown_members_are_rejected()
    {
        Assert.Multiple(
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Alerts.ConfidenceLevel)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.UvLevel)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.ExposureRisk)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.ComfortLevel)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.CommuteLevel)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.CommuteHazard)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.ExerciseLevel)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.ExerciseFactor)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.GardeningAdvice)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.FloodStatus)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.ConfidenceTrendDirection)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.PollenType)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Pipeline.PollenTrendDirection)99).ToProto()),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => ((Messages.Alerts.AlertSeverity)99).ToProto()));
    }
}
