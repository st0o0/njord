using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration.Persistence;

public sealed class StorageOptionsValidator(IConfiguration configuration) : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        var persistence = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>()
            ?? new PersistenceOptions();
        if (persistence is not { Provider: PersistenceProvider.Sqlite, ConnectionString: null })
        {
            return ValidateOptionsResult.Success;
        }

        var dir = Path.GetDirectoryName(options.PersistencePath);
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
