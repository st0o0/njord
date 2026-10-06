namespace Njord.Configuration;

public sealed class AlertOptions
{
    public bool Enabled { get; set; } = true;
    public double[] FrostThresholds { get; set; } = [0, -5, -15];
    public double[] HeatThresholds { get; set; } = [30, 35, 40];
    public double[] StormGustThresholds { get; set; } = [17, 25, 33];
    public double HeavyRainHourlyThreshold { get; set; } = 10.0;
    public double HeavyRainDailyThreshold { get; set; } = 25.0;
    public double PressureDropThreshold { get; set; } = 5.0;
    public double PressureDropSevereThreshold { get; set; } = 10.0;
    public int FogPersistentHours { get; set; } = 4;
    public double CapeThreshold { get; set; } = 1000.0;
    public double ThunderstormPrecipThreshold { get; set; } = 5.0;
    public double ThunderstormGustThreshold { get; set; } = 15.0;
    public double IceThreshold { get; set; } = 2.0;
    public double[] WindChillThresholds { get; set; } = [-10, -20, -30];
    public double[] VisibilityThresholds { get; set; } = [1000, 200, 50];
    public double[] TropicalNightThresholds { get; set; } = [20, 23, 25];
    public double[] HumidityThresholds { get; set; } = [16, 21, 24];
}
