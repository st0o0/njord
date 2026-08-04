namespace Njord.Domain.Analysis;

public sealed record ResolvedPreferences(
    double IdealTemp,
    double IdealTempLow,
    double IdealTempHigh,
    double MinTemp,
    double IdealWindLow,
    double IdealWindHigh,
    double IndoorTemp,
    double HeatSensitivity,
    double HumiditySensitivity,
    double WindSensitivity,
    double RainSensitivity)
{
    public static ResolvedPreferences Default { get; } = new(
        IdealTemp: 22.0,
        IdealTempLow: 5.0,
        IdealTempHigh: 20.0,
        MinTemp: 10.0,
        IdealWindLow: 1.0,
        IdealWindHigh: 3.0,
        IndoorTemp: 22.0,
        HeatSensitivity: 1.0,
        HumiditySensitivity: 1.0,
        WindSensitivity: 1.0,
        RainSensitivity: 1.0);
}
