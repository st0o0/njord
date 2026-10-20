using Njord.Core.Configuration;

namespace Njord.Features.Alerts.SwimmingConditions;

public sealed class SwimmingConditionsConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Marine:Swimming";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
