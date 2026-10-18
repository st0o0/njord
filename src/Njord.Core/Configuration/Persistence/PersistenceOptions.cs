namespace Njord.Core.Configuration.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Njord:Persistence";

    public PersistenceProvider Provider { get; set; }
    public string? ConnectionString { get; set; }
}
