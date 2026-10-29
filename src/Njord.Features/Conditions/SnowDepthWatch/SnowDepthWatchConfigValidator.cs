using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SnowDepthWatch;

public sealed class SnowDepthWatchConfigValidator : IValidateOptions<SnowDepthWatchConfig>
{
    public ValidateOptionsResult Validate(string? name, SnowDepthWatchConfig options)
    {
        var section = IConfigSection.SectionOf<SnowDepthWatchConfig>();
        var failures = new List<string>();

        if (options.LightThreshold <= 0)
            failures.Add($"{section}:LightThreshold must be positive, got {options.LightThreshold}.");
        if (options.ModerateThreshold <= 0)
            failures.Add($"{section}:ModerateThreshold must be positive, got {options.ModerateThreshold}.");
        if (options.HeavyThreshold <= 0)
            failures.Add($"{section}:HeavyThreshold must be positive, got {options.HeavyThreshold}.");
        if (options.ExtremeThreshold <= 0)
            failures.Add($"{section}:ExtremeThreshold must be positive, got {options.ExtremeThreshold}.");

        if (!(options.LightThreshold < options.ModerateThreshold
              && options.ModerateThreshold < options.HeavyThreshold
              && options.HeavyThreshold < options.ExtremeThreshold))
        {
            failures.Add(
                $"{section}: thresholds must be strictly ascending (Light < Moderate < Heavy < Extreme), got [{options.LightThreshold}, {options.ModerateThreshold}, {options.HeavyThreshold}, {options.ExtremeThreshold}].");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
