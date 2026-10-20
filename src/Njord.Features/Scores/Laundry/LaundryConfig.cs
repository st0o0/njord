using Njord.Core.Configuration;

namespace Njord.Features.Scores.Laundry;

public sealed class LaundryConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Lifestyle:Laundry";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
