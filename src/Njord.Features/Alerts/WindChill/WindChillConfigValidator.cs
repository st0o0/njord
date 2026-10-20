using Microsoft.Extensions.Options;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.WindChill;

public sealed class WindChillConfigValidator : IValidateOptions<WindChillConfig>
{
    public ValidateOptionsResult Validate(string? name, WindChillConfig options) =>
        ThresholdValidation.Violation($"{WindChillConfig.SectionName}:Thresholds", options.Thresholds, ScanDirection.Min) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
