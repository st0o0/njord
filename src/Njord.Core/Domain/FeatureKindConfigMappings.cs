namespace Njord.Core.Domain;

public static class FeatureKindConfigMappings
{
    public static string ToConfigKey(this FeatureKind kind) => kind switch
    {
        FeatureKind.FrostAlert => "FrostAlert",
        FeatureKind.HeatAlert => "HeatAlert",
        FeatureKind.StormAlert => "StormAlert",
        FeatureKind.RainAlert => "RainAlert",
        FeatureKind.UvAlert => "UvAlert",
        FeatureKind.FogAlert => "FogAlert",
        FeatureKind.SnowAlert => "SnowAlert",
        FeatureKind.PressureDropAlert => "PressureDropAlert",
        FeatureKind.ThunderstormAlert => "ThunderstormAlert",
        FeatureKind.IceAlert => "IceAlert",
        FeatureKind.WindChillAlert => "WindChillAlert",
        FeatureKind.VisibilityAlert => "VisibilityAlert",
        FeatureKind.TropicalNightAlert => "TropicalNightAlert",
        FeatureKind.HumidityAlert => "HumidityAlert",
        FeatureKind.PollenAlert => "PollenAlert",
        FeatureKind.Pm25Alert => "Pm25Alert",
        FeatureKind.CommuteSafety => "CommuteSafety",
        FeatureKind.ExerciseAdvisory => "ExerciseAdvisory",
        FeatureKind.LaundryScore => "LaundryScore",
        FeatureKind.OutdoorScore => "OutdoorScore",
        FeatureKind.BBQScore => "BBQScore",
        FeatureKind.NightVentilation => "NightVentilation",
        FeatureKind.VentilationWindow => "VentilationWindow",
        FeatureKind.GardeningAdvisory => "GardeningAdvisory",
        FeatureKind.ConfidenceTrend => "ConfidenceTrend",
        FeatureKind.ApproachingFront => "ApproachingFront",
        FeatureKind.SolarYield => "SolarYield",
        FeatureKind.PollenTrend => "PollenTrend",
        FeatureKind.FloodRisk => "FloodRisk",
        FeatureKind.FloodRain => "FloodRain",
        FeatureKind.FloodTimeline => "FloodTimeline",
        FeatureKind.SwimmingConditions => "SwimmingConditions",
        FeatureKind.SurfConditions => "SurfConditions",
        FeatureKind.SeasonalOutlook => "SeasonalOutlook",
        FeatureKind.SatelliteRadiation => "SatelliteRadiation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown feature kind."),
    };

    public static bool TryParseConfigKey(string key, out FeatureKind kind)
    {
        foreach (var candidate in Enum.GetValues<FeatureKind>())
        {
            if (string.Equals(candidate.ToConfigKey(), key, StringComparison.Ordinal))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }
}
