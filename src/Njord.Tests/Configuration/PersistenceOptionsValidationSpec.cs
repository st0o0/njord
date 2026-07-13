using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class PersistenceOptionsValidationSpec
{
    private static NjordOptions ValidOptions() => new()
    {
        Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
        Models =
        [
            "icon_d2", "icon_eu", "icon_global", "ecmwf_ifs025",
            "gfs_seamless", "ukmo_global_deterministic_10km",
            "meteoswiss_icon_ch1", "meteoswiss_icon_ch2",
        ],
        Mqtt = new MqttOptions { Host = "broker.local" },
    };

    private static readonly NjordOptionsValidator Validator = new();

    [Fact(Timeout = 5000)]
    public void Sqlite_default_without_connection_string_passes()
    {
        var options = ValidOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void Sqlite_with_explicit_connection_string_passes()
    {
        var options = ValidOptions();
        options.Persistence = new PersistenceOptions
        {
            Provider = PersistenceProvider.Sqlite,
            ConnectionString = "Data Source=custom.db",
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void PostgreSql_with_connection_string_passes()
    {
        var options = ValidOptions();
        options.Persistence = new PersistenceOptions
        {
            Provider = PersistenceProvider.PostgreSql,
            ConnectionString = "Host=localhost;Database=njord;Username=njord;Password=secret",
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void PostgreSql_without_connection_string_fails()
    {
        var options = ValidOptions();
        options.Persistence = new PersistenceOptions
        {
            Provider = PersistenceProvider.PostgreSql,
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("PostgreSQL", result.FailureMessage);
        Assert.Contains("ConnectionString", result.FailureMessage);
    }
}
