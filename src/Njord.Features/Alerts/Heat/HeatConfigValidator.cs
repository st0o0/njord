using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Heat;

public sealed class HeatConfigValidator : IValidateOptions<HeatConfig>
{
    public ValidateOptionsResult Validate(string? name, HeatConfig options) =>
        ThresholdValidation.Violation($"{IConfigSection.SectionOf<HeatConfig>()}:Thresholds", options.Thresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
