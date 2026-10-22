using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Dust;

public sealed class DustConfigValidator : IValidateOptions<DustConfig>
{
    public ValidateOptionsResult Validate(string? name, DustConfig options)
    {
        if (ThresholdValidation.Violation($"{IConfigSection.SectionOf<DustConfig>()}:Thresholds", options.Thresholds,
                ScanDirection.Max) is { } failure)
        {
            return ValidateOptionsResult.Fail(failure);
        }

        return ValidateOptionsResult.Success;
    }
}
