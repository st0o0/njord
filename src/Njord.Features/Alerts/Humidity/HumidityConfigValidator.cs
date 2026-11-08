using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Terp.Options;

namespace Njord.Features.Alerts.Humidity;

public sealed class HumidityConfigValidator : IValidateOptions<HumidityConfig>
{
    public ValidateOptionsResult Validate(string? name, HumidityConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<HumidityConfig>());
        if (ThresholdValidation.Violation("Thresholds", options.Thresholds, ScanDirection.Max) is { } failure)
        {
            failures.Add(failure);
        }

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");

        return failures.ToResult();
    }
}
