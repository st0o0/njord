using Njord.Core.Configuration.Persistence;

namespace Njord.Core.Tests.Configuration.Persistence;

public sealed class PersistenceOptionsValidatorSpec
{
    private static readonly PersistenceOptionsValidator Validator = new();

    [Fact]
    public void Sqlite_default_without_connection_string_passes()
    {
        var result = Validator.Validate(null, new PersistenceOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Sqlite_with_explicit_connection_string_passes()
    {
        var options = new PersistenceOptions
        {
            Provider = PersistenceProvider.Sqlite,
            ConnectionString = "Data Source=custom.db",
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void PostgreSql_with_connection_string_passes()
    {
        var options = new PersistenceOptions
        {
            Provider = PersistenceProvider.PostgreSql,
            ConnectionString = "Host=localhost;Database=njord;Username=njord;Password=secret",
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void PostgreSql_without_connection_string_fails()
    {
        var result = Validator.Validate(null, new PersistenceOptions { Provider = PersistenceProvider.PostgreSql });

        Assert.True(result.Failed);
        Assert.Contains("PostgreSQL", result.FailureMessage);
        Assert.Contains("ConnectionString", result.FailureMessage);
    }
}
