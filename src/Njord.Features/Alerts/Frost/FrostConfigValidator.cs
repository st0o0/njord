using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Frost;

public sealed class FrostConfigValidator : IValidateOptions<FrostConfig>
{
    public ValidateOptionsResult Validate(string? name, FrostConfig options) =>
        ThresholdValidation.Violation($"{IConfigSection.SectionOf<FrostConfig>()}:Thresholds", options.Thresholds, ScanDirection.Min) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
