using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.PressureDrop;

public sealed class PressureDropConfigValidator : IValidateOptions<PressureDropConfig>
{
    public ValidateOptionsResult Validate(string? name, PressureDropConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<PressureDropConfig>());

        if (options.Threshold >= options.SevereThreshold)
        {
            failures.Add($" Threshold must be less than SevereThreshold, got {options.Threshold} and {options.SevereThreshold}.");
        }

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");

        return failures.ToResult();
    }
}
