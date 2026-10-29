using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.MigraineRisk;

public sealed class MigraineRiskConfigValidator : IValidateOptions<MigraineRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, MigraineRiskConfig options)
    {
        var section = IConfigSection.SectionOf<MigraineRiskConfig>();
        var failures = new List<string>();

        if (options.PressureThreshold6h <= 0)
        {
            failures.Add($"{section}:PressureThreshold6h must be positive, got {options.PressureThreshold6h}.");
        }

        if (options.PressureThreshold24h <= 0)
        {
            failures.Add($"{section}:PressureThreshold24h must be positive, got {options.PressureThreshold24h}.");
        }

        if (options.TempSwingThreshold <= 0)
        {
            failures.Add($"{section}:TempSwingThreshold must be positive, got {options.TempSwingThreshold}.");
        }

        if (options.HumiditySwingThreshold <= 0)
        {
            failures.Add($"{section}:HumiditySwingThreshold must be positive, got {options.HumiditySwingThreshold}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
