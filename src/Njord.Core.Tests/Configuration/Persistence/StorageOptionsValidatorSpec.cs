using Microsoft.Extensions.Configuration;
using Njord.Core.Configuration.Persistence;

namespace Njord.Core.Tests.Configuration.Persistence;

public sealed class StorageOptionsValidatorSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-storage-{Guid.NewGuid():N}");

    private static StorageOptionsValidator ValidatorWith(Dictionary<string, string?> entries) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(entries).Build());

    [Fact]
    public void A_writable_sqlite_directory_passes_and_is_created()
    {
        var options = new StorageOptions { PersistencePath = Path.Combine(_tempDir, "journal.db") };

        var result = ValidatorWith([]).Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public void An_explicit_connection_string_skips_the_directory_probe()
    {
        var options = new StorageOptions { PersistencePath = Path.Combine(_tempDir, "journal.db") };

        var result = ValidatorWith(new() { ["Njord:Persistence:ConnectionString"] = "Data Source=custom.db" })
            .Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.False(Directory.Exists(_tempDir));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
