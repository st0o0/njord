using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Thunderstorm;

public sealed class ThunderstormConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Thunderstorm";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] CapeThresholds { get; set; } = [];
    public static double[] DefaultCapeThresholds => [1000, 1500, 2500];
    public double PrecipThreshold { get; set; } = 5.0;
    public double GustThreshold { get; set; } = 20.0;
    public double CinThreshold { get; set; } = 200.0;

    public void ApplyDefaults()
    {
        if (CapeThresholds is { Length: 0 }) CapeThresholds = DefaultCapeThresholds;
    }
}
