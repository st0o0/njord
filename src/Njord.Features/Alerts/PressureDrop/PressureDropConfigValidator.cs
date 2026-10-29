using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.PressureDrop;

public sealed class PressureDropConfigValidator : IValidateOptions<PressureDropConfig>
{
    public ValidateOptionsResult Validate(string? name, PressureDropConfig options) =>
        options.Threshold < options.SevereThreshold
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{IConfigSection.SectionOf<PressureDropConfig>()}: Threshold must be less than SevereThreshold, got {options.Threshold} and {options.SevereThreshold}.");
}
