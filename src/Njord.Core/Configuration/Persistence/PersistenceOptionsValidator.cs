using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration.Persistence;

public sealed class PersistenceOptionsValidator : IValidateOptions<PersistenceOptions>
{
    public ValidateOptionsResult Validate(string? name, PersistenceOptions options)
    {
        if (options.Provider == PersistenceProvider.PostgreSql && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail(
                "PostgreSQL persistence requires a connection string: set Njord:Persistence:ConnectionString.");
        }

        return ValidateOptionsResult.Success;
    }
}
