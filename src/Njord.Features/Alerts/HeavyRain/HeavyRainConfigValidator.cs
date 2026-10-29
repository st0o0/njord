using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.HeavyRain;

public sealed class HeavyRainConfigValidator : IValidateOptions<HeavyRainConfig>
{
    public ValidateOptionsResult Validate(string? name, HeavyRainConfig options)
    {
        var section = IConfigSection.SectionOf<HeavyRainConfig>();
        var failures = new List<string>();

        if (options.WindowHours < 1)
        {
            failures.Add($"{section}:WindowHours must be at least 1, got {options.WindowHours}.");
        }

        if (options.HourlyThreshold <= 0)
        {
            failures.Add($"{section}:HourlyThreshold must be positive, got {options.HourlyThreshold}.");
        }

        if (options.DailyThreshold <= options.HourlyThreshold)
        {
            failures.Add($"{section}:DailyThreshold ({options.DailyThreshold}) must be greater than HourlyThreshold ({options.HourlyThreshold}).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
