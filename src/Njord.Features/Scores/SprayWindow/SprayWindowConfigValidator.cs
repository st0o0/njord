using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Scores.SprayWindow;

public sealed class SprayWindowConfigValidator : IValidateOptions<SprayWindowConfig>
{
    public ValidateOptionsResult Validate(string? name, SprayWindowConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<SprayWindowConfig>());

        failures.Require(options.MaxWindSpeed > 0, $"MaxWindSpeed must be positive, got {options.MaxWindSpeed}.");
        failures.Require(options.RainFreeHours >= 1, $"RainFreeHours must be at least 1, got {options.RainFreeHours}.");
        failures.Require(options.MinTemp < options.MaxTemp, $"MinTemp must be less than MaxTemp, got {options.MinTemp} >= {options.MaxTemp}.");
        failures.Require(options.MinWindowScore >= 1, $"MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        failures.Require(options.MinWindowHours >= 1, $"MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        failures.Require(options.MaxWindows >= 1, $"MaxWindows must be at least 1, got {options.MaxWindows}.");
        failures.Require(options.RainProbabilityLimit is >= 0 and <= 100,
            $"RainProbabilityLimit must be 0..100, got {options.RainProbabilityLimit}.");
        failures.Require(options.TemperatureFalloff > 0, $"TemperatureFalloff must be positive, got {options.TemperatureFalloff}.");
        failures.Require(options.HumidityMin < options.HumidityMax,
            $"HumidityMin must be less than HumidityMax, got {options.HumidityMin} >= {options.HumidityMax}.");
        failures.Require(options.HumidityFalloff > 0, $"HumidityFalloff must be positive, got {options.HumidityFalloff}.");

        return failures.ToResult();
    }
}
