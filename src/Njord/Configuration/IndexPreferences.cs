namespace Njord.Configuration;

public sealed class IndexPreferences
{
    public double? IdealOutdoorTemp { get; set; }
    public double? RunningIdealTempLow { get; set; }
    public double? RunningIdealTempHigh { get; set; }
    public double? BbqMinTemp { get; set; }
    public double? BbqIdealWindLow { get; set; }
    public double? BbqIdealWindHigh { get; set; }
    public double? IndoorTemp { get; set; }
    public double? HeatSensitivity { get; set; }
    public double? HumiditySensitivity { get; set; }
    public double? WindSensitivity { get; set; }
    public double? RainSensitivity { get; set; }
}
