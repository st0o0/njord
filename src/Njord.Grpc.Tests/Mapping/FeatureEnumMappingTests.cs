using Njord.Grpc.Mapping;
using Njord.Messages.Results.Conditions;

namespace Njord.Grpc.Tests.Mapping;

public sealed class FeatureEnumMappingTests
{
    [Theory]
    [InlineData(HeatPumpRecommendation.Ideal, V2.HeatPumpRecommendation.Ideal)]
    [InlineData(HeatPumpRecommendation.Normal, V2.HeatPumpRecommendation.Normal)]
    [InlineData(HeatPumpRecommendation.Inefficient, V2.HeatPumpRecommendation.Inefficient)]
    [InlineData(HeatPumpRecommendation.Avoid, V2.HeatPumpRecommendation.Avoid)]
    public void Heat_pump_recommendation_maps_to_proto(Njord.Messages.Results.Conditions.HeatPumpRecommendation value, V2.HeatPumpRecommendation expected)
    {
        Assert.Equal(expected, value.ToProto());
    }

    [Theory]
    [InlineData(BatteryStrategy.FeedIn, V2.BatteryStrategy.FeedIn)]
    [InlineData(BatteryStrategy.ChargeBattery, V2.BatteryStrategy.ChargeBattery)]
    [InlineData(BatteryStrategy.Conserve, V2.BatteryStrategy.Conserve)]
    [InlineData(BatteryStrategy.Normal, V2.BatteryStrategy.Normal)]
    public void Battery_strategy_maps_to_proto(Njord.Messages.Results.Conditions.BatteryStrategy value, V2.BatteryStrategy expected)
    {
        Assert.Equal(expected, value.ToProto());
    }

    [Theory]
    [InlineData(IrrigationAdvice.Water, V2.IrrigationAdvice.Water)]
    [InlineData(IrrigationAdvice.Skip, V2.IrrigationAdvice.Skip)]
    [InlineData(IrrigationAdvice.Reduce, V2.IrrigationAdvice.Reduce)]
    [InlineData(IrrigationAdvice.SoilWet, V2.IrrigationAdvice.SoilWet)]
    public void Irrigation_advice_maps_to_proto(Njord.Messages.Results.Conditions.IrrigationAdvice value, V2.IrrigationAdvice expected)
    {
        Assert.Equal(expected, value.ToProto());
    }

    [Theory]
    [InlineData(FrostDepthRisk.None, V2.FrostDepthRisk.None)]
    [InlineData(FrostDepthRisk.Surface, V2.FrostDepthRisk.Surface)]
    [InlineData(FrostDepthRisk.Shallow, V2.FrostDepthRisk.Shallow)]
    [InlineData(FrostDepthRisk.Moderate, V2.FrostDepthRisk.Moderate)]
    [InlineData(FrostDepthRisk.Deep, V2.FrostDepthRisk.Deep)]
    public void Frost_depth_risk_maps_to_proto(Njord.Messages.Results.Conditions.FrostDepthRisk value, V2.FrostDepthRisk expected)
    {
        Assert.Equal(expected, value.ToProto());
    }

    [Theory]
    [InlineData(InversionStrength.None, V2.InversionStrength.None)]
    [InlineData(InversionStrength.Mild, V2.InversionStrength.Mild)]
    [InlineData(InversionStrength.Strong, V2.InversionStrength.Strong)]
    [InlineData(InversionStrength.Extreme, V2.InversionStrength.Extreme)]
    public void Inversion_strength_maps_to_proto(Njord.Messages.Results.Conditions.InversionStrength value, V2.InversionStrength expected)
    {
        Assert.Equal(expected, value.ToProto());
    }

    [Theory]
    [InlineData(InversionType.Radiation, V2.InversionType.Radiation)]
    [InlineData(InversionType.Subsidence, V2.InversionType.Subsidence)]
    [InlineData(InversionType.Frontal, V2.InversionType.Frontal)]
    public void Inversion_type_maps_to_proto(Njord.Messages.Results.Conditions.InversionType value, V2.InversionType expected)
    {
        Assert.Equal(expected, value.ToProto());
    }

    [Theory]
    [InlineData(StabilityClass.Stable, V2.StabilityClass.Stable)]
    [InlineData(StabilityClass.Marginal, V2.StabilityClass.Marginal)]
    [InlineData(StabilityClass.Capped, V2.StabilityClass.Capped)]
    [InlineData(StabilityClass.Unstable, V2.StabilityClass.Unstable)]
    [InlineData(StabilityClass.Explosive, V2.StabilityClass.Explosive)]
    public void Stability_class_maps_to_proto(Njord.Messages.Results.Conditions.StabilityClass value, V2.StabilityClass expected)
    {
        Assert.Equal(expected, value.ToProto());
    }

    [Theory]
    [InlineData(CeilingCategory.VeryLow, V2.CeilingCategory.VeryLow)]
    [InlineData(CeilingCategory.Low, V2.CeilingCategory.Low)]
    [InlineData(CeilingCategory.Moderate, V2.CeilingCategory.Moderate)]
    [InlineData(CeilingCategory.High, V2.CeilingCategory.High)]
    [InlineData(CeilingCategory.VeryHigh, V2.CeilingCategory.VeryHigh)]
    [InlineData(CeilingCategory.Unlimited, V2.CeilingCategory.Unlimited)]
    public void Ceiling_category_maps_to_proto(Njord.Messages.Results.Conditions.CeilingCategory value, V2.CeilingCategory expected)
    {
        Assert.Equal(expected, value.ToProto());
    }
}
