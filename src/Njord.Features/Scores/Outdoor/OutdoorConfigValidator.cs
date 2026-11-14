using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Scores.Outdoor;

public sealed class OutdoorConfigValidator : IValidateOptions<OutdoorConfig>
{
    public ValidateOptionsResult Validate(string? name, OutdoorConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<OutdoorConfig>());

        failures.Require(options.GoodHourScore > 0, $"GoodHourScore must be positive, got {options.GoodHourScore}.");

        var sum = options.TemperatureWeight + options.RainWeight + options.WindWeight
                  + options.CloudWeight + options.UvWeight + options.AirQualityWeight;
        failures.Require(Math.Abs(sum - 1.0) < 0.001, $"weights must sum to 1.0, got {sum}.");

        failures.Require(options.TemperatureLow < options.TemperatureIdeal,
            $"TemperatureLow must be less than TemperatureIdeal, got {options.TemperatureLow} >= {options.TemperatureIdeal}.");
        failures.Require(options.TemperatureIdeal < options.TemperatureWarm,
            $"TemperatureIdeal must be less than TemperatureWarm, got {options.TemperatureIdeal} >= {options.TemperatureWarm}.");
        failures.Require(options.TemperatureWarm < options.TemperatureHot,
            $"TemperatureWarm must be less than TemperatureHot, got {options.TemperatureWarm} >= {options.TemperatureHot}.");

        failures.Require(options.WindLow < options.WindIdeal,
            $"WindLow must be less than WindIdeal, got {options.WindLow} >= {options.WindIdeal}.");
        failures.Require(options.WindIdeal < options.WindModerate,
            $"WindIdeal must be less than WindModerate, got {options.WindIdeal} >= {options.WindModerate}.");
        failures.Require(options.WindModerate < options.WindStrong,
            $"WindModerate must be less than WindStrong, got {options.WindModerate} >= {options.WindStrong}.");

        return failures.ToResult();
    }
}
