using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.Evapotranspiration;

public sealed class EvapotranspirationConfigValidator : IValidateOptions<EvapotranspirationConfig>
{
    public ValidateOptionsResult Validate(string? name, EvapotranspirationConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<EvapotranspirationConfig>());

        failures.Require(options.LowMm > 0, $"LowMm must be positive, got {options.LowMm}.");
        failures.Require(options.HighMm > 0, $"HighMm must be positive, got {options.HighMm}.");
        if (options.LowMm >= options.HighMm)
            failures.Add($"LowMm must be less than HighMm, got {options.LowMm} >= {options.HighMm}.");

        return failures.ToResult();
    }
}
