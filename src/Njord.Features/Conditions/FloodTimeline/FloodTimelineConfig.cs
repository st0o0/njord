using Njord.Core.Configuration;

namespace Njord.Features.Conditions.FloodTimeline;

public sealed class FloodTimelineConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Flood:FloodTimeline";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
