namespace Njord.Messages.Alerts;

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
