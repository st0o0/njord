using Microsoft.Extensions.Options;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Dust;

public sealed class DustConfigValidator : IValidateOptions<DustConfig>
{
    public ValidateOptionsResult Validate(string? name, DustConfig options) =>
        ThresholdValidation.Violation($"{DustConfig.SectionName}:Thresholds", options.Thresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
