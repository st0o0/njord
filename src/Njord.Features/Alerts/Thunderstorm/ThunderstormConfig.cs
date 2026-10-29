using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Thunderstorm;

public sealed class ThunderstormConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:Thunderstorm";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[]? CapeThresholds { get; set; }
    public double[] EffectiveCapeThresholds => CapeThresholds ?? [1000, 1500, 2500];
    public double PrecipThreshold { get; set; } = 5.0;
    public double GustThreshold { get; set; } = 20.0;
    public double CinThreshold { get; set; } = 200.0;
}
