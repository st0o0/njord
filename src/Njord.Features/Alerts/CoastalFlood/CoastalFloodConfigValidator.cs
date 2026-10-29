using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.CoastalFlood;

public sealed class CoastalFloodConfigValidator : IValidateOptions<CoastalFloodConfig>
{
    public ValidateOptionsResult Validate(string? name, CoastalFloodConfig options)
    {
        var section = IConfigSection.SectionOf<CoastalFloodConfig>();
        var failures = new List<string>();

        if (options.SeaLevelThresholdM <= 0)
        {
            failures.Add($"{section}:SeaLevelThresholdM must be positive, got {options.SeaLevelThresholdM}.");
        }

        if (options.WaveHeightThresholdM <= 0)
        {
            failures.Add($"{section}:WaveHeightThresholdM must be positive, got {options.WaveHeightThresholdM}.");
        }

        if (options.WindSpeedThreshold <= 0)
        {
            failures.Add($"{section}:WindSpeedThreshold must be positive, got {options.WindSpeedThreshold}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
