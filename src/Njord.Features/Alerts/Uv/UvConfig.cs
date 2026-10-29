using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Uv;

public sealed class UvConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Uv";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
}
