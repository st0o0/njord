using Njord.Configuration;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests;

public sealed class PersistenceOptionsValidatorTests
{
    [Fact]
    public void Should_reject_postgres_persistence_without_a_connection_string()
    {
        var options = new PersistenceOptions { Provider = PersistenceProvider.PostgreSql };

        var result = new PersistenceOptionsValidator().Validate(null, options);

        Assert.Contains("requires a connection string", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_an_unwritable_sqlite_data_directory()
    {
        var blocker = Path.GetTempFileName();
        try
        {
            var options = new PersistenceOptions { DataPath = Path.Combine(blocker, "njord.db") };

            var result = new PersistenceOptionsValidator().Validate(null, options);

            Assert.Contains("is not writable", ValidationAssert.SingleFailure(result));
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}
