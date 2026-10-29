using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.PressureDrop;

public sealed class PressureDropConfigValidator : IValidateOptions<PressureDropConfig>
{
    public ValidateOptionsResult Validate(string? name, PressureDropConfig options)
    {
        var section = IConfigSection.SectionOf<PressureDropConfig>();
        var failures = new List<string>();
        if (options.Threshold >= options.SevereThreshold)
        {
            failures.Add($"{section}: Threshold must be less than SevereThreshold, got {options.Threshold} and {options.SevereThreshold}.");
        }

        if (options.WindowHours < 1)
        {
            failures.Add($"{section}:WindowHours must be at least 1, got {options.WindowHours}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
