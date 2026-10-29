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
            failures.Add($"{section}: PeakKw must be positive, got {options.PeakKw}.");

        if (options.SystemEfficiency is <= 0 or > 1)
            failures.Add($"{section}: SystemEfficiency must be greater than 0 and at most 1, got {options.SystemEfficiency}.");

        if (options.BatteryCapacityKwh < 0)
            failures.Add($"{section}: BatteryCapacityKwh must not be negative, got {options.BatteryCapacityKwh}.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
