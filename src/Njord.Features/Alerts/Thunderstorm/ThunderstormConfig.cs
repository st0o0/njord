namespace Njord.Features.Alerts.Thunderstorm;

public sealed class ThunderstormConfig
{
    public const string SectionName = "Njord:Features:Weather:Thunderstorm";
    public bool Enabled { get; set; } = true;
    public double CapeThreshold { get; set; } = 1000.0;
    public double PrecipThreshold { get; set; } = 5.0;
    public double GustThreshold { get; set; } = 15.0;
}
