using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Thunderstorm;

public sealed class ThunderstormConfigValidator : IValidateOptions<ThunderstormConfig>
{
    public ValidateOptionsResult Validate(string? name, ThunderstormConfig options) =>
        ThresholdValidation.Violation($"{IConfigSection.SectionOf<ThunderstormConfig>()}:CapeThresholds", options.CapeThresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
