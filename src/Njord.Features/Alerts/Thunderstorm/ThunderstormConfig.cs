using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Thunderstorm;

public sealed class ThunderstormConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Weather:Thunderstorm";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double CapeThreshold { get; set; } = 1000.0;
    public double PrecipThreshold { get; set; } = 5.0;
    public double GustThreshold { get; set; } = 15.0;
    public double CinThreshold { get; set; } = -50.0;
    public double LiftedIndexThreshold { get; set; } = -2.0;
}
