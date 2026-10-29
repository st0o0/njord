using Njord.Core.Configuration;

namespace Njord.Features.Conditions.ExerciseAdvisory;

public sealed class ExerciseAdvisoryConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Exercise";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
