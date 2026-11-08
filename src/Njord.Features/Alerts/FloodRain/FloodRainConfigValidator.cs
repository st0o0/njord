using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.FloodRain;

public sealed class FloodRainConfigValidator : IValidateOptions<FloodRainConfig>
{
    public ValidateOptionsResult Validate(string? name, FloodRainConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<FloodRainConfig>());

        if (!double.IsFinite(options.HeavyRainThreshold) || options.HeavyRainThreshold <= 0)
        {
            failures.Add($"HeavyRainThreshold must be greater than 0, got {options.HeavyRainThreshold}.");
        }

        failures.Require(options.ForecastDays is >= 1 and <= 210, $"ForecastDays must be between 1 and 210, got {options.ForecastDays}.");

        return failures.ToResult();
    }
}
