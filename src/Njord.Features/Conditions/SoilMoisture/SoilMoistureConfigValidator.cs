using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.SoilMoisture;

public sealed class SoilMoistureConfigValidator : IValidateOptions<SoilMoistureConfig>
{
    public ValidateOptionsResult Validate(string? name, SoilMoistureConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<SoilMoistureConfig>());

        failures.Require(options.DryThreshold > 0, $"DryThreshold must be positive, got {options.DryThreshold}.");
        failures.Require(options.WetThreshold > 0, $"WetThreshold must be positive, got {options.WetThreshold}.");
        if (options.DryThreshold >= options.WetThreshold)
            failures.Add($"DryThreshold ({options.DryThreshold}) must be less than WetThreshold ({options.WetThreshold}).");

        return failures.ToResult();
    }
}
