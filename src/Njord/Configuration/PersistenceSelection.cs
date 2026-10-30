
using Njord.Core;
using Njord.Core.Configuration;

namespace Njord.Configuration;

internal sealed record PersistenceSelection(string ConnectionString, string ProviderName)
{
    public static PersistenceSelection From(PersistenceOptions persistence, string persistencePath)
    {
        var connectionString = persistence.ConnectionString
            ?? (persistence.Provider == PersistenceProvider.Sqlite
                ? $"Data Source={Path.GetFullPath(persistencePath)}"
                : throw new InvalidOperationException(
                    "PostgreSQL persistence requires a connection string: set Njord:Persistence:ConnectionString."));

        var providerName = persistence.Provider switch
        {
            PersistenceProvider.Sqlite => LinqToDB.ProviderName.SQLiteMS,
            PersistenceProvider.PostgreSql => LinqToDB.ProviderName.PostgreSQL,
            _ => throw new InvalidOperationException($"Unsupported persistence provider: {persistence.Provider}"),
        };

        return new PersistenceSelection(connectionString, providerName);
    }
}
