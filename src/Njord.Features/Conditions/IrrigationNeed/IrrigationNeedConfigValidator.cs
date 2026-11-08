using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.IrrigationNeed;

public sealed class IrrigationNeedConfigValidator : IValidateOptions<IrrigationNeedConfig>
{
    public ValidateOptionsResult Validate(string? name, IrrigationNeedConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<IrrigationNeedConfig>());

        failures.Require(options.DryThresholdPercent is >= 0 and <= 100, $"DryThresholdPercent must be 0..100, got {options.DryThresholdPercent}.");
        failures.Require(options.WetThresholdPercent is >= 0 and <= 100, $"WetThresholdPercent must be 0..100, got {options.WetThresholdPercent}.");
        if (options.DryThresholdPercent >= options.WetThresholdPercent)
        {
            failures.Add($"DryThresholdPercent must be less than WetThresholdPercent, got {options.DryThresholdPercent} >= {options.WetThresholdPercent}.");
        }

        return failures.ToResult();
    }
}
