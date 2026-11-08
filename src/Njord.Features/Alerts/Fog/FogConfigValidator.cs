using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.Fog;

public sealed class FogConfigValidator : IValidateOptions<FogConfig>
{
    public ValidateOptionsResult Validate(string? name, FogConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<FogConfig>());

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");
        failures.Require(options.PersistentHours >= 1, $"PersistentHours must be at least 1, got {options.PersistentHours}.");

        if (options.PersistentHours > options.WindowHours)
        {
            failures.Add($"PersistentHours ({options.PersistentHours}) must not exceed WindowHours ({options.WindowHours}).");
        }

        return failures.ToResult();
    }
}
