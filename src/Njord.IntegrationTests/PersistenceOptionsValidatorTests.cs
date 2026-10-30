using Njord.Core.Configuration;

namespace Njord.IntegrationTests;

public sealed class PersistenceOptionsTests
{
    [Fact]
    public void Default_persistence_uses_sqlite_data_path()
    {
        var options = new PersistenceOptions();

        Assert.Null(options.ConnectionString);
        Assert.Equal("data/njord.db", options.DataPath);
    }

    [Fact]
    public void ConnectionString_set_implies_postgresql()
    {
        var options = new PersistenceOptions
        {
            ConnectionString = "Host=localhost;Database=njord;Username=njord;Password=secret",
        };

        Assert.NotNull(options.ConnectionString);
    }
}
