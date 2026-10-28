using Njord.Core.Configuration;

namespace Njord.Features.Scores.SprayWindow;

public sealed class SprayWindowConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:SprayWindow";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double MaxWindSpeed { get; set; } = 3.0;
    public int RainFreeHours { get; set; } = 4;
    public double MinTemp { get; set; } = 10.0;
    public double MaxTemp { get; set; } = 25.0;
    public int MinWindowScore { get; set; } = 80;
    public int MinWindowHours { get; set; } = 2;
    public int MaxWindows { get; set; } = 3;
}
