using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FireRisk;

public sealed class FireRiskConfigValidator : IValidateOptions<FireRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, FireRiskConfig options) =>
        options.YellowThreshold < options.OrangeThreshold && options.OrangeThreshold < options.RedThreshold
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{IConfigSection.SectionOf<FireRiskConfig>()}: YellowThreshold < OrangeThreshold < RedThreshold required, got {options.YellowThreshold}, {options.OrangeThreshold}, {options.RedThreshold}.");
}
