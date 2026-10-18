using Njord.Weather;

namespace Njord.Messages.Alerts;

public abstract record AlertDetails;

public sealed record NoDetails : AlertDetails
{
    public static readonly NoDetails Instance = new();

    private NoDetails()
    {
    }
}

public sealed record FrostDetails(double ExpectedLow, Horizon? EarliestFrost, int ModelsAgreeing) : AlertDetails;

public sealed record HeatDetails(double ExpectedMax, int ModelsAgreeing) : AlertDetails;

public sealed record StormDetails(double ExpectedMaxGust, int ModelsAgreeing) : AlertDetails;

public sealed record HeavyRainDetails(int HourlyExceedModels, int DailyExceedModels) : AlertDetails;

public sealed record UvDetails(UvLevel Level, double UvIndex) : AlertDetails;

public sealed record FogDetails(int FogHours, int ModelsAgreeing) : AlertDetails;

public sealed record SnowDetails(double ExpectedAccumulation, double? FreezingLevel, int ModelsAgreeing) : AlertDetails;

public sealed record PressureDropDetails(double MaxDrop, int ModelsAgreeing) : AlertDetails;

public sealed record ThunderstormDetails(int ModelsAgreeing) : AlertDetails;

public sealed record IceDetails(double ExpectedLow, int RainHours, bool SoilFrozen) : AlertDetails;

public sealed record WindChillDetails(double FeltTemperature, double WindFactor, ExposureRisk? ExposureRisk) : AlertDetails;

public sealed record VisibilityDetails(double MinVisibility, int HoursBelow1000m) : AlertDetails;

public sealed record TropicalNightDetails(double NightMinimum, int HoursAbove20) : AlertDetails;

public sealed record HumidityDetails(double MaxDewpoint, HumidityComfort? ComfortLevel) : AlertDetails;

public enum UvLevel
{
    Low,
    Moderate,
    High,
    VeryHigh,
    Extreme,
}

public enum ExposureRisk
{
    Frostbite30Min,
    Frostbite10Min,
}

public enum HumidityComfort
{
    Muggy,
    Oppressive,
    Tropical,
}

public static class AlertDetailWireNames
{
    public static string ToWireName(this UvLevel level) => level switch
    {
        UvLevel.Low => "low",
        UvLevel.Moderate => "moderate",
        UvLevel.High => "high",
        UvLevel.VeryHigh => "very_high",
        UvLevel.Extreme => "extreme",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    public static string ToWireName(this ExposureRisk risk) => risk switch
    {
        ExposureRisk.Frostbite30Min => "frostbite_30min",
        ExposureRisk.Frostbite10Min => "frostbite_10min",
        _ => throw new ArgumentOutOfRangeException(nameof(risk), risk, null),
    };

    public static string ToWireName(this HumidityComfort comfort) => comfort switch
    {
        HumidityComfort.Muggy => "muggy",
        HumidityComfort.Oppressive => "oppressive",
        HumidityComfort.Tropical => "tropical",
        _ => throw new ArgumentOutOfRangeException(nameof(comfort), comfort, null),
    };
}
