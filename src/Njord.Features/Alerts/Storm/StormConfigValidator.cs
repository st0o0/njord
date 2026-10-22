using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Storm;

public sealed class StormConfigValidator : IValidateOptions<StormConfig>
{
    public ValidateOptionsResult Validate(string? name, StormConfig options) =>
        ThresholdValidation.Violation($"{IConfigSection.SectionOf<StormConfig>()}:GustThresholds", options.GustThresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
