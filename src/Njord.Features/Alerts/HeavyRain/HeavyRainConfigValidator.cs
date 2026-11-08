using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.HeavyRain;

public sealed class HeavyRainConfigValidator : IValidateOptions<HeavyRainConfig>
{
    public ValidateOptionsResult Validate(string? name, HeavyRainConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<HeavyRainConfig>());

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");
        failures.Require(options.HourlyThreshold > 0, $"HourlyThreshold must be positive, got {options.HourlyThreshold}.");

        if (options.DailyThreshold <= options.HourlyThreshold)
        {
            failures.Add($"DailyThreshold ({options.DailyThreshold}) must be greater than HourlyThreshold ({options.HourlyThreshold}).");
        }

        return failures.ToResult();
    }
}
