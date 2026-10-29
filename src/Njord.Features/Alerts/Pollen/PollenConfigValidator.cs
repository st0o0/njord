using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Pollen;

public sealed class PollenConfigValidator : IValidateOptions<PollenConfig>
{
    public ValidateOptionsResult Validate(string? name, PollenConfig options) =>
        ThresholdValidation.Violation($"{IConfigSection.SectionOf<PollenConfig>()}:Thresholds", options.Thresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
