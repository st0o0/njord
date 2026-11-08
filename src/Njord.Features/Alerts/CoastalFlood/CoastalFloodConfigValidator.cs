using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.CoastalFlood;

public sealed class CoastalFloodConfigValidator : IValidateOptions<CoastalFloodConfig>
{
    public ValidateOptionsResult Validate(string? name, CoastalFloodConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<CoastalFloodConfig>());

        failures.Require(options.SeaLevelThresholdM > 0, $"SeaLevelThresholdM must be positive, got {options.SeaLevelThresholdM}.");
        failures.Require(options.WaveHeightThresholdM > 0, $"WaveHeightThresholdM must be positive, got {options.WaveHeightThresholdM}.");
        failures.Require(options.WindSpeedThreshold > 0, $"WindSpeedThreshold must be positive, got {options.WindSpeedThreshold}.");

        return failures.ToResult();
    }
}
