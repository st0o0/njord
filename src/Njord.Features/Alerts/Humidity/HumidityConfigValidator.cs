using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Humidity;

public sealed class HumidityConfigValidator : IValidateOptions<HumidityConfig>
{
    public ValidateOptionsResult Validate(string? name, HumidityConfig options) =>
        ThresholdValidation.Violation($"{IConfigSection.SectionOf<HumidityConfig>()}:Thresholds", options.Thresholds, ScanDirection.Max) is { } failure
            ? ValidateOptionsResult.Fail(failure)
            : ValidateOptionsResult.Success;
}
