using Microsoft.Extensions.Options;

namespace Njord.Configuration;

public sealed class PersistenceOptionsValidator : IValidateOptions<PersistenceOptions>
{
    public ValidateOptionsResult Validate(string? name, PersistenceOptions options)
    {
        if (options.Provider == PersistenceProvider.PostgreSql && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail(
                "PostgreSQL persistence requires a connection string: set Njord:Persistence:ConnectionString.");
        }

        if (options is not { Provider: PersistenceProvider.Sqlite, ConnectionString: null })
        {
            return ValidateOptionsResult.Success;
        }

        var dir = Path.GetDirectoryName(options.DataPath);
        if (string.IsNullOrEmpty(dir))
        {
            return ValidateOptionsResult.Success;
        }

        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".njord-write-probe");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            var fullDir = Path.GetFullPath(dir);
            return ValidateOptionsResult.Fail(
                $"Persistence directory '{fullDir}' is not writable: {ex.Message}. " +
                "Ensure the volume is mounted and the container user has write access.");
        }
    }
}
