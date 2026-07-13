namespace Njord.Configuration;

public enum PersistenceProvider
{
    Sqlite,
    PostgreSql,
}

public sealed record PersistenceOptions
{
    public PersistenceProvider Provider { get; set; } = PersistenceProvider.Sqlite;
    public string? ConnectionString { get; set; }
}
