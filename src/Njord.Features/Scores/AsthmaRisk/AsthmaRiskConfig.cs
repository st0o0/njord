using Njord.Core.Configuration;

namespace Njord.Features.Scores.AsthmaRisk;

public sealed class AsthmaRiskConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:AsthmaRisk";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int Pm25Weight { get; set; } = 35;
    public int OzoneWeight { get; set; } = 25;
    public int PollenWeight { get; set; } = 20;
    public int HumidityWeight { get; set; } = 10;
    public int BoundaryLayerWeight { get; set; } = 10;
    public int MinWindowScore { get; set; } = 50;
    public int MinWindowHours { get; set; } = 2;
    public int MaxWindows { get; set; } = 3;
}
