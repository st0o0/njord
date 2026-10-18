namespace Njord.Core.Configuration.Alerts;

public sealed class AlertOptions
{
    public const string SectionName = "Njord:Enrichment:Alerts";

    public bool Enabled { get; set; }
    public double[] FrostThresholds { get; set; } = [0, -5, -15];
    public double[] HeatThresholds { get; set; } = [30, 35, 40];
    public double[] StormGustThresholds { get; set; } = [17, 25, 33];
    public double HeavyRainHourlyThreshold { get; set; }
    public double HeavyRainDailyThreshold { get; set; }
    public double PressureDropThreshold { get; set; }
    public double PressureDropSevereThreshold { get; set; }
    public int FogPersistentHours { get; set; }
    public double CapeThreshold { get; set; }
    public double ThunderstormPrecipThreshold { get; set; }
    public double ThunderstormGustThreshold { get; set; }
    public double IceThreshold { get; set; }
    public double[] WindChillThresholds { get; set; } = [-10, -20, -30];
    public double[] VisibilityThresholds { get; set; } = [1000, 200, 50];
    public double[] TropicalNightThresholds { get; set; } = [20, 23, 25];
    public double[] HumidityThresholds { get; set; } = [16, 21, 24];
}
