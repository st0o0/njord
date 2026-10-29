using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Conditions.IrrigationNeed;

public sealed class IrrigationNeedConfigValidator : IValidateOptions<IrrigationNeedConfig>
{
    public ValidateOptionsResult Validate(string? name, IrrigationNeedConfig options)
    {
        var section = IConfigSection.SectionOf<IrrigationNeedConfig>();
        var failures = new List<string>();

        if (options.DryThresholdPercent is < 0 or > 100)
            failures.Add($"{section}:DryThresholdPercent must be 0..100, got {options.DryThresholdPercent}.");
        if (options.WetThresholdPercent is < 0 or > 100)
            failures.Add($"{section}:WetThresholdPercent must be 0..100, got {options.WetThresholdPercent}.");
        if (options.DryThresholdPercent >= options.WetThresholdPercent)
            failures.Add($"{section}:DryThresholdPercent must be less than WetThresholdPercent, got {options.DryThresholdPercent} >= {options.WetThresholdPercent}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
