using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.ClearSkyUv;

public sealed class ClearSkyUvConfigValidator : IValidateOptions<ClearSkyUvConfig>
{
    public ValidateOptionsResult Validate(string? name, ClearSkyUvConfig options)
    {
        var section = IConfigSection.SectionOf<ClearSkyUvConfig>();
        var failures = new List<string>();
        if (ThresholdValidation.Violation($"{section}:Thresholds", options.Thresholds, ScanDirection.Max) is { } failure)
        {
            failures.Add(failure);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
