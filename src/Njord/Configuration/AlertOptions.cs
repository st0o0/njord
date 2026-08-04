namespace Njord.Configuration;

public sealed class AlertOptions
{
    public bool Enabled { get; set; } = true;
    public double FrostThreshold { get; set; } = 0.0;
    public double[] HeatThresholds { get; set; } = [30, 35, 40];
    public double StormGustThreshold { get; set; } = 16.7;
    public double HeavyRainHourlyThreshold { get; set; } = 10.0;
    public double HeavyRainDailyThreshold { get; set; } = 25.0;
    public double PressureDropThreshold { get; set; } = 5.0;
    public double CapeThreshold { get; set; } = 1000.0;
    public double ThunderstormPrecipThreshold { get; set; } = 5.0;
    public double ThunderstormGustThreshold { get; set; } = 15.0;
}
