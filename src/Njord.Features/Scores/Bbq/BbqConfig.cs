using Njord.Core.Configuration;

namespace Njord.Features.Scores.Bbq;

public sealed class BbqConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Lifestyle:Bbq";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
