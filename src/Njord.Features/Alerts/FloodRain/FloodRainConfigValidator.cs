using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FloodRain;

public sealed class FloodRainConfigValidator : IValidateOptions<FloodRainConfig>
{
    public ValidateOptionsResult Validate(string? name, FloodRainConfig options)
    {
        var section = IConfigSection.SectionOf<FloodRainConfig>();
        var failures = new List<string>();
        if (!double.IsFinite(options.HeavyRainThreshold) || options.HeavyRainThreshold <= 0)
        {
            failures.Add($"{section}:HeavyRainThreshold must be greater than 0, got {options.HeavyRainThreshold}.");
        }

        if (options.ForecastDays is < 1 or > 210)
        {
            failures.Add($"{section}:ForecastDays must be between 1 and 210, got {options.ForecastDays}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
