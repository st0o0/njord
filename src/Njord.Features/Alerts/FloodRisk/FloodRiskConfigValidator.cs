using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.FloodRisk;

public sealed class FloodRiskConfigValidator : IValidateOptions<FloodRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, FloodRiskConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<FloodRiskConfig>());

        if (!double.IsFinite(options.MedianFactor) || options.MedianFactor <= 1)
        {
            failures.Add($"MedianFactor must be greater than 1, got {options.MedianFactor}.");
        }

        failures.Require(options.ForecastDays is >= 1 and <= 210, $"ForecastDays must be between 1 and 210, got {options.ForecastDays}.");

        return failures.ToResult();
    }
}
