using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Pm25;

public sealed class Pm25ConfigValidator : IValidateOptions<Pm25Config>
{
    public ValidateOptionsResult Validate(string? name, Pm25Config options) =>
        ThresholdValidation.Violation($"{IConfigSection.SectionOf<Pm25Config>()}:Thresholds", options.Thresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
