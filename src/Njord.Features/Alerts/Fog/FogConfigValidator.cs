using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Fog;

public sealed class FogConfigValidator : IValidateOptions<FogConfig>
{
    public ValidateOptionsResult Validate(string? name, FogConfig options)
    {
        var section = IConfigSection.SectionOf<FogConfig>();
        var failures = new List<string>();

        if (options.WindowHours < 1)
        {
            failures.Add($"{section}:WindowHours must be at least 1, got {options.WindowHours}.");
        }

        if (options.PersistentHours < 1)
        {
            failures.Add($"{section}:PersistentHours must be at least 1, got {options.PersistentHours}.");
        }

        if (options.PersistentHours > options.WindowHours)
        {
            failures.Add($"{section}:PersistentHours ({options.PersistentHours}) must not exceed WindowHours ({options.WindowHours}).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
