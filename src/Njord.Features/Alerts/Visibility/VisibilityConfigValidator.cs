using Microsoft.Extensions.Options;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Visibility;

public sealed class VisibilityConfigValidator : IValidateOptions<VisibilityConfig>
{
    public ValidateOptionsResult Validate(string? name, VisibilityConfig options) =>
        ThresholdValidation.Violation($"{VisibilityConfig.SectionName}:Thresholds", options.Thresholds, ScanDirection.Min) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
