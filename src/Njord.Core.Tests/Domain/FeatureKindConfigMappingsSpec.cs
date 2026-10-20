using Njord.Core.Domain;

namespace Njord.Core.Tests.Domain;

public sealed class FeatureKindConfigMappingsSpec
{
    [Theory]
    [InlineData(FeatureKind.FrostAlert, "FrostAlert")]
    [InlineData(FeatureKind.HeatAlert, "HeatAlert")]
    [InlineData(FeatureKind.StormAlert, "StormAlert")]
    [InlineData(FeatureKind.RainAlert, "RainAlert")]
    [InlineData(FeatureKind.UvAlert, "UvAlert")]
    [InlineData(FeatureKind.FogAlert, "FogAlert")]
    [InlineData(FeatureKind.SnowAlert, "SnowAlert")]
    [InlineData(FeatureKind.PressureDropAlert, "PressureDropAlert")]
    [InlineData(FeatureKind.ThunderstormAlert, "ThunderstormAlert")]
    [InlineData(FeatureKind.IceAlert, "IceAlert")]
    [InlineData(FeatureKind.WindChillAlert, "WindChillAlert")]
    [InlineData(FeatureKind.VisibilityAlert, "VisibilityAlert")]
    [InlineData(FeatureKind.TropicalNightAlert, "TropicalNightAlert")]
    [InlineData(FeatureKind.HumidityAlert, "HumidityAlert")]
    [InlineData(FeatureKind.PollenAlert, "PollenAlert")]
    [InlineData(FeatureKind.Pm25Alert, "Pm25Alert")]
    [InlineData(FeatureKind.CommuteSafety, "CommuteSafety")]
    [InlineData(FeatureKind.ExerciseAdvisory, "ExerciseAdvisory")]
    [InlineData(FeatureKind.LaundryScore, "LaundryScore")]
    [InlineData(FeatureKind.OutdoorScore, "OutdoorScore")]
    [InlineData(FeatureKind.BBQScore, "BBQScore")]
    [InlineData(FeatureKind.NightVentilation, "NightVentilation")]
    [InlineData(FeatureKind.VentilationWindow, "VentilationWindow")]
    [InlineData(FeatureKind.GardeningAdvisory, "GardeningAdvisory")]
    [InlineData(FeatureKind.ConfidenceTrend, "ConfidenceTrend")]
    [InlineData(FeatureKind.ApproachingFront, "ApproachingFront")]
    [InlineData(FeatureKind.SolarYield, "SolarYield")]
    [InlineData(FeatureKind.PollenTrend, "PollenTrend")]
    [InlineData(FeatureKind.FloodRisk, "FloodRisk")]
    [InlineData(FeatureKind.FloodRain, "FloodRain")]
    [InlineData(FeatureKind.FloodTimeline, "FloodTimeline")]
    [InlineData(FeatureKind.SwimmingConditions, "SwimmingConditions")]
    [InlineData(FeatureKind.SurfConditions, "SurfConditions")]
    public void Should_map_every_feature_kind_to_its_config_key(FeatureKind kind, string expected)
    {
        Assert.Equal(expected, kind.ToConfigKey());
    }

    [Fact]
    public void Should_round_trip_every_feature_kind_through_its_config_key()
    {
        foreach (var kind in Enum.GetValues<FeatureKind>())
        {
            Assert.True(FeatureKindConfigMappings.TryParseConfigKey(kind.ToConfigKey(), out var parsed));
            Assert.Equal(kind, parsed);
        }
    }

    [Fact]
    public void Should_reject_an_undefined_feature_kind()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((FeatureKind)999).ToConfigKey());

        Assert.Equal("kind", ex.ParamName);
    }

    [Fact]
    public void Should_not_parse_an_unknown_config_key()
    {
        Assert.False(FeatureKindConfigMappings.TryParseConfigKey("Lava", out _));
    }
}
