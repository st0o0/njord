namespace Njord.Messages.Pipeline;

public enum UvLevel
{
    Moderate,
    High,
    VeryHigh,
    Extreme,
}

public enum ExposureRisk
{
    Frostbite30Minutes,
    Frostbite10Minutes,
}

public enum ComfortLevel
{
    Muggy,
    Oppressive,
    Tropical,
}

public enum CommuteLevel
{
    Safe,
    Caution,
    Dangerous,
}

public enum CommuteHazard
{
    Ice,
    Fog,
    HeavyRain,
    StrongWind,
}

public enum ExerciseLevel
{
    Safe,
    Caution,
    Avoid,
}

public enum ExerciseFactor
{
    Pm25,
    Ozone,
    Temperature,
    Humidity,
    Uv,
}

public enum GardeningAdvice
{
    Plant,
    Hold,
}

public enum FloodStatus
{
    Normal,
    AboveWarning,
}

public enum ConfidenceTrendDirection
{
    Converging,
    Stable,
    Diverging,
}

public enum PollenType
{
    Birch,
    Grass,
    Alder,
    Mugwort,
}

public enum PollenTrendDirection
{
    Stable,
    Rising,
    Falling,
}

public enum HeatPumpRecommendation
{
    Ideal,
    Normal,
    Inefficient,
    Avoid,
}

public enum BatteryStrategy
{
    FeedIn,
    ChargeBattery,
    Conserve,
    Normal,
}

public enum IrrigationAdvice
{
    Water,
    Skip,
    Reduce,
    SoilWet,
}

public enum FrostDepthRisk
{
    None,
    Surface,
    Shallow,
    Moderate,
    Deep,
}

public enum InversionStrength
{
    None,
    Mild,
    Strong,
    Extreme,
}

public enum InversionType
{
    Radiation,
    Subsidence,
    Frontal,
}

public enum StabilityClass
{
    Stable,
    Marginal,
    Capped,
    Unstable,
    Explosive,
}

public enum CeilingCategory
{
    VeryLow,
    Low,
    Moderate,
    High,
    VeryHigh,
    Unlimited,
}
