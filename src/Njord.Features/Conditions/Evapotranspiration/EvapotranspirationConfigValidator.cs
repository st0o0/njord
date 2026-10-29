using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Conditions.Evapotranspiration;

public sealed class EvapotranspirationConfigValidator : IValidateOptions<EvapotranspirationConfig>
{
    public ValidateOptionsResult Validate(string? name, EvapotranspirationConfig options)
    {
        var section = IConfigSection.SectionOf<EvapotranspirationConfig>();
        var failures = new List<string>();

        if (options.LowMm <= 0)
            failures.Add($"{section}:LowMm must be positive, got {options.LowMm}.");
        if (options.HighMm <= 0)
            failures.Add($"{section}:HighMm must be positive, got {options.HighMm}.");
        if (options.LowMm >= options.HighMm)
            failures.Add($"{section}:LowMm must be less than HighMm, got {options.LowMm} >= {options.HighMm}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
