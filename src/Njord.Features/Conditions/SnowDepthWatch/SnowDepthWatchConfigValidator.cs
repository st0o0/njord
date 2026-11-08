using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.SnowDepthWatch;

public sealed class SnowDepthWatchConfigValidator : IValidateOptions<SnowDepthWatchConfig>
{
    public ValidateOptionsResult Validate(string? name, SnowDepthWatchConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<SnowDepthWatchConfig>());

        failures.Require(options.LightThreshold > 0, $"LightThreshold must be positive, got {options.LightThreshold}.");
        failures.Require(options.ModerateThreshold > 0, $"ModerateThreshold must be positive, got {options.ModerateThreshold}.");
        failures.Require(options.HeavyThreshold > 0, $"HeavyThreshold must be positive, got {options.HeavyThreshold}.");
        failures.Require(options.ExtremeThreshold > 0, $"ExtremeThreshold must be positive, got {options.ExtremeThreshold}.");

        if (!(options.LightThreshold < options.ModerateThreshold
              && options.ModerateThreshold < options.HeavyThreshold
              && options.HeavyThreshold < options.ExtremeThreshold))
        {
            failures.Add(
                $" thresholds must be strictly ascending (Light < Moderate < Heavy < Extreme), got [{options.LightThreshold}, {options.ModerateThreshold}, {options.HeavyThreshold}, {options.ExtremeThreshold}].");
        }

        return failures.ToResult();
    }
}
