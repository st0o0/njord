using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SolarYield;

public sealed class SolarYieldConfigValidator : IValidateOptions<SolarYieldConfig>
{
    public ValidateOptionsResult Validate(string? name, SolarYieldConfig options)
    {
        var section = IConfigSection.SectionOf<SolarYieldConfig>();
        List<string> failures = [];

        if (options.PeakKw <= 0)
        {
            failures.Add($"{section}: PeakKw must be positive, got {options.PeakKw}.");
        }

        if (options.SystemEfficiency is <= 0 or > 1)
        {
            failures.Add($"{section}: SystemEfficiency must be greater than 0 and at most 1, got {options.SystemEfficiency}.");
        }

        if (options.BatteryCapacityKwh < 0)
        {
            failures.Add($"{section}: BatteryCapacityKwh must not be negative, got {options.BatteryCapacityKwh}.");
        }

        if (options.SunnierFactor <= 1.0)
        {
            failures.Add($"{section}: SunnierFactor must be > 1.0, got {options.SunnierFactor}.");
        }

        if (options.CloudierFactor is <= 0 or >= 1.0)
        {
            failures.Add($"{section}: CloudierFactor must be in (0, 1), got {options.CloudierFactor}.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
