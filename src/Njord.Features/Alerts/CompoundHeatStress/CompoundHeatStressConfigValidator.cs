using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.CompoundHeatStress;

public sealed class CompoundHeatStressConfigValidator : IValidateOptions<CompoundHeatStressConfig>
{
    public ValidateOptionsResult Validate(string? name, CompoundHeatStressConfig options)
    {
        var section = IConfigSection.SectionOf<CompoundHeatStressConfig>();
        var failures = new List<string>();

        if (options.WetBulbWarning >= options.WetBulbSevere || options.WetBulbSevere >= options.WetBulbExtreme)
        {
            failures.Add($"{section}: WetBulbWarning < WetBulbSevere < WetBulbExtreme required, got {options.WetBulbWarning}, {options.WetBulbSevere}, {options.WetBulbExtreme}.");
        }

        if (options.UvThreshold <= 0)
        {
            failures.Add($"{section}: UvThreshold must be > 0, got {options.UvThreshold}.");
        }

        if (options.UvPm25Threshold <= 0)
        {
            failures.Add($"{section}: UvPm25Threshold must be > 0, got {options.UvPm25Threshold}.");
        }

        if (options.VeryHighApparentMargin <= 0)
        {
            failures.Add($"{section}: VeryHighApparentMargin must be > 0, got {options.VeryHighApparentMargin}.");
        }

        if (options.VeryHighPm25Factor <= 0)
        {
            failures.Add($"{section}: VeryHighPm25Factor must be > 0, got {options.VeryHighPm25Factor}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
