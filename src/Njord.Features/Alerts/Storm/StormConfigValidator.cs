using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Terp.Options;

namespace Njord.Features.Alerts.Storm;

public sealed class StormConfigValidator : IValidateOptions<StormConfig>
{
    public ValidateOptionsResult Validate(string? name, StormConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<StormConfig>());
        if (ThresholdValidation.Violation("GustThresholds", options.GustThresholds, ScanDirection.Max) is { } failure)
        {
            failures.Add(failure);
        }

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");

        return failures.ToResult();
    }
}
