using LinqToDB;
using Njord.Configuration;
using Njord.Core.Configuration.Persistence;
using Njord.Tests.Shared;

namespace Njord.Tests.Configuration;

public sealed class PersistenceSelectionSpec
{
    [Fact]
    public void Sqlite_without_connection_string_uses_the_full_path_of_the_persistence_file()
    {
        var persistence = AppSettingsDefaults.Options<PersistenceOptions>(o =>
        {
            o.Provider = PersistenceProvider.Sqlite;
            o.ConnectionString = null;
        });

        var selection = PersistenceSelection.From(persistence, "data/njord.db");

        Assert.Multiple(
            () => Assert.Equal($"Data Source={Path.GetFullPath("data/njord.db")}", selection.ConnectionString),
            () => Assert.Equal(ProviderName.SQLiteMS, selection.ProviderName));
    }

    [Fact]
    public void An_explicit_connection_string_wins_over_the_persistence_path()
    {
        var persistence = AppSettingsDefaults.Options<PersistenceOptions>(o =>
        {
            o.Provider = PersistenceProvider.Sqlite;
            o.ConnectionString = "Data Source=elsewhere.db";
        });

        var selection = PersistenceSelection.From(persistence, "data/njord.db");

        Assert.Equal("Data Source=elsewhere.db", selection.ConnectionString);
    }

    [Fact]
    public void PostgreSql_with_connection_string_selects_the_postgresql_provider()
    {
        var persistence = AppSettingsDefaults.Options<PersistenceOptions>(o =>
        {
            o.Provider = PersistenceProvider.PostgreSql;
            o.ConnectionString = "Host=db;Database=njord";
        });

        var selection = PersistenceSelection.From(persistence, "data/njord.db");

        Assert.Multiple(
            () => Assert.Equal("Host=db;Database=njord", selection.ConnectionString),
            () => Assert.Equal(ProviderName.PostgreSQL, selection.ProviderName));
    }

    [Fact]
    public void PostgreSql_without_connection_string_fails_naming_the_option()
    {
        var persistence = AppSettingsDefaults.Options<PersistenceOptions>(o =>
        {
            o.Provider = PersistenceProvider.PostgreSql;
            o.ConnectionString = null;
        });

        var ex = Assert.Throws<InvalidOperationException>(() => PersistenceSelection.From(persistence, "data/njord.db"));

        Assert.Equal(
            "PostgreSQL persistence requires a connection string: set Njord:Persistence:ConnectionString.",
            ex.Message);
    }

    [Fact]
    public void An_undefined_provider_fails_naming_the_value()
    {
        var persistence = AppSettingsDefaults.Options<PersistenceOptions>(o =>
        {
            o.Provider = (PersistenceProvider)99;
            o.ConnectionString = "x";
        });

        var ex = Assert.Throws<InvalidOperationException>(() => PersistenceSelection.From(persistence, "data/njord.db"));

        Assert.Equal("Unsupported persistence provider: 99", ex.Message);
    }
}
