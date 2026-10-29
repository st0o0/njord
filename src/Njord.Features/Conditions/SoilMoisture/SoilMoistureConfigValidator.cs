using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SoilMoisture;

public sealed class SoilMoistureConfigValidator : IValidateOptions<SoilMoistureConfig>
{
    public ValidateOptionsResult Validate(string? name, SoilMoistureConfig options)
    {
        var section = IConfigSection.SectionOf<SoilMoistureConfig>();
        var failures = new List<string>();

        if (options.DryThreshold <= 0)
        {
            failures.Add($"{section}:DryThreshold must be positive, got {options.DryThreshold}.");
        }

        if (options.WetThreshold <= 0)
        {
            failures.Add($"{section}:WetThreshold must be positive, got {options.WetThreshold}.");
        }

        if (options.DryThreshold >= options.WetThreshold)
        {
            failures.Add($"{section}:DryThreshold ({options.DryThreshold}) must be less than WetThreshold ({options.WetThreshold}).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
