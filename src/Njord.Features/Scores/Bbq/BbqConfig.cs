using Njord.Core.Configuration;

namespace Njord.Features.Scores.Bbq;

public sealed class BbqConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Bbq";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
