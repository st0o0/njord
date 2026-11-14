using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FireRisk;

public sealed class FireRiskConfigValidator : IValidateOptions<FireRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, FireRiskConfig options)
    {
        var section = IConfigSection.SectionOf<FireRiskConfig>();
        var failures = new List<string>();

        if (options.YellowThreshold >= options.OrangeThreshold || options.OrangeThreshold >= options.RedThreshold)
        {
            failures.Add($"{section}: YellowThreshold < OrangeThreshold < RedThreshold required, got {options.YellowThreshold}, {options.OrangeThreshold}, {options.RedThreshold}.");
        }

        if (options.ColdCutoff >= options.TemperatureThreshold)
        {
            failures.Add($"{section}: ColdCutoff must be < TemperatureThreshold, got {options.ColdCutoff}, {options.TemperatureThreshold}.");
        }

        if (options.HumidCutoff <= options.HumidityThreshold)
        {
            failures.Add($"{section}: HumidCutoff must be > HumidityThreshold, got {options.HumidCutoff}, {options.HumidityThreshold}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
