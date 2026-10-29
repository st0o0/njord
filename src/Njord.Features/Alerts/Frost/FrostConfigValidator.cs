using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Frost;

public sealed class FrostConfigValidator : IValidateOptions<FrostConfig>
{
    public ValidateOptionsResult Validate(string? name, FrostConfig options)
    {
        var section = IConfigSection.SectionOf<FrostConfig>();
        var failures = new List<string>();
        if (ThresholdValidation.Violation($"{section}:Thresholds", options.Thresholds, ScanDirection.Min) is { } failure)
        {
            failures.Add(failure);
        }

        if (options.WindowHours < 1)
        {
            failures.Add($"{section}:WindowHours must be at least 1, got {options.WindowHours}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
