using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Snow;

public sealed class SnowConfigValidator : IValidateOptions<SnowConfig>
{
    public ValidateOptionsResult Validate(string? name, SnowConfig options) =>
        ThresholdValidation.Violation($"{IConfigSection.SectionOf<SnowConfig>()}:Thresholds", options.Thresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
