using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Scores.Laundry;

public sealed class LaundryConfigValidator : IValidateOptions<LaundryConfig>
{
    public ValidateOptionsResult Validate(string? name, LaundryConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<LaundryConfig>());

        failures.Require(options.MinWindowScore >= 1, $"MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        failures.Require(options.MinWindowHours >= 1, $"MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        failures.Require(options.MaxWindows >= 1, $"MaxWindows must be at least 1, got {options.MaxWindows}.");
        failures.Require(options.GoodHourScore > 0, $"GoodHourScore must be positive, got {options.GoodHourScore}.");

        var sum = options.TemperatureWeight + options.HumidityWeight + options.WindWeight
                  + options.RainWeight + options.SunWeight;
        failures.Require(Math.Abs(sum - 1.0) < 0.001, $"weights must sum to 1.0, got {sum}.");

        failures.Require(options.TemperatureLow < options.TemperatureHigh,
            $"TemperatureLow must be less than TemperatureHigh, got {options.TemperatureLow} >= {options.TemperatureHigh}.");
        failures.Require(options.HumidityLow < options.HumidityHigh,
            $"HumidityLow must be less than HumidityHigh, got {options.HumidityLow} >= {options.HumidityHigh}.");
        failures.Require(options.WindLow < options.WindIdeal,
            $"WindLow must be less than WindIdeal, got {options.WindLow} >= {options.WindIdeal}.");
        failures.Require(options.WindIdeal < options.WindModerate,
            $"WindIdeal must be less than WindModerate, got {options.WindIdeal} >= {options.WindModerate}.");
        failures.Require(options.WindModerate < options.WindStrong,
            $"WindModerate must be less than WindStrong, got {options.WindModerate} >= {options.WindStrong}.");
        failures.Require(options.SunshineFull > 0, $"SunshineFull must be positive, got {options.SunshineFull}.");

        return failures.ToResult();
    }
}
