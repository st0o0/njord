using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FloodRisk;

public sealed class FloodRiskConfigValidator : IValidateOptions<FloodRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, FloodRiskConfig options)
    {
        var section = IConfigSection.SectionOf<FloodRiskConfig>();
        var failures = new List<string>();
        if (!double.IsFinite(options.MedianFactor) || options.MedianFactor <= 1)
        {
            failures.Add($"{section}:MedianFactor must be greater than 1, got {options.MedianFactor}.");
        }

        if (options.ForecastDays is < 1 or > 210)
        {
            failures.Add($"{section}:ForecastDays must be between 1 and 210, got {options.ForecastDays}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
