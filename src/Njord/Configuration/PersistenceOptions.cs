namespace Njord.Configuration;

public sealed class PersistenceOptions
{
    public PersistenceProvider Provider { get; set; } = PersistenceProvider.Sqlite;
    public string? ConnectionString { get; set; }
}
