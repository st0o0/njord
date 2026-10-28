using Njord.Core.Configuration;

namespace Njord.Features.Scores.Laundry;

public sealed class LaundryConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:Laundry";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int MinWindowScore { get; set; } = 50;
    public int MinWindowHours { get; set; } = 2;
    public int MaxWindows { get; set; } = 3;
}
