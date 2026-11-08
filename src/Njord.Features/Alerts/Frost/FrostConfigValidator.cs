using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Terp.Options;

namespace Njord.Features.Alerts.Frost;

public sealed class FrostConfigValidator : IValidateOptions<FrostConfig>
{
    public ValidateOptionsResult Validate(string? name, FrostConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<FrostConfig>());
        if (ThresholdValidation.Violation("Thresholds", options.Thresholds, ScanDirection.Min) is { } failure)
        {
            failures.Add(failure);
        }

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");

        return failures.ToResult();
    }
}
