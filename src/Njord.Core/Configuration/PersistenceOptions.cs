namespace Njord.Core.Configuration;

public sealed class PersistenceOptions
{
    public const string SectionName = "Njord:Persistence";

    public PersistenceProvider Provider { get; set; } = PersistenceProvider.Sqlite;
    public string? ConnectionString { get; set; }
}
