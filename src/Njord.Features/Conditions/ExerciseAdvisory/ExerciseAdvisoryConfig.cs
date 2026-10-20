using Njord.Core.Configuration;

namespace Njord.Features.Conditions.ExerciseAdvisory;

public sealed class ExerciseAdvisoryConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Lifestyle:Exercise";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
