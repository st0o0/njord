namespace Njord.Core.Configuration;

public sealed class PersistenceOptions : IConfigSection
{
    private const string SectionName = "Njord:Persistence";
    static string IConfigSection.SectionName => SectionName;

    public string? ConnectionString { get; set; }
    public string DataPath { get; set; } = "data/njord.db";
}
