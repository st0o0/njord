using Microsoft.Extensions.Options;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.TropicalNight;

public sealed class TropicalNightConfigValidator : IValidateOptions<TropicalNightConfig>
{
    public ValidateOptionsResult Validate(string? name, TropicalNightConfig options) =>
        ThresholdValidation.Violation($"{TropicalNightConfig.SectionName}:Thresholds", options.Thresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
