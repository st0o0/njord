using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Terp.Options;

namespace Njord.Features.Alerts.Snow;

public sealed class SnowConfigValidator : IValidateOptions<SnowConfig>
{
    public ValidateOptionsResult Validate(string? name, SnowConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<SnowConfig>());
        if (ThresholdValidation.Violation("Thresholds", options.Thresholds, ScanDirection.Max) is { } failure)
        {
            failures.Add(failure);
        }

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");

        return failures.ToResult();
    }
}
