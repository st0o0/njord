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
        failures.Require(options.RedSeaLevelM > options.OrangeSeaLevelM,
            $"RedSeaLevelM must be > OrangeSeaLevelM, got {options.RedSeaLevelM}, {options.OrangeSeaLevelM}.");
        failures.Require(options.OrangeSeaLevelM > 0, $"OrangeSeaLevelM must be positive, got {options.OrangeSeaLevelM}.");
        failures.Require(options.RedMinFactors >= 0, $"RedMinFactors must be >= 0, got {options.RedMinFactors}.");
        failures.Require(options.OrangeMinFactors >= 0, $"OrangeMinFactors must be >= 0, got {options.OrangeMinFactors}.");

        return failures.ToResult();
    }
}
