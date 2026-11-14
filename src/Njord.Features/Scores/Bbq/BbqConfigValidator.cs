using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Scores.Bbq;

public sealed class BbqConfigValidator : IValidateOptions<BbqConfig>
{
    public ValidateOptionsResult Validate(string? name, BbqConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<BbqConfig>());

        failures.Require(options.GoodHourScore > 0, $"GoodHourScore must be positive, got {options.GoodHourScore}.");

        var sum = options.TemperatureWeight + options.WindWeight + options.RainWeight + options.CloudWeight;
        failures.Require(Math.Abs(sum - 1.0) < 0.001, $"weights must sum to 1.0, got {sum}.");

        failures.Require(options.TemperatureLow < options.TemperatureIdeal,
            $"TemperatureLow must be less than TemperatureIdeal, got {options.TemperatureLow} >= {options.TemperatureIdeal}.");
        failures.Require(options.TemperatureIdeal < options.TemperatureWarm,
            $"TemperatureIdeal must be less than TemperatureWarm, got {options.TemperatureIdeal} >= {options.TemperatureWarm}.");
        failures.Require(options.TemperatureWarm < options.TemperatureHot,
            $"TemperatureWarm must be less than TemperatureHot, got {options.TemperatureWarm} >= {options.TemperatureHot}.");

        failures.Require(options.WindModerate < options.WindStrong,
            $"WindModerate must be less than WindStrong, got {options.WindModerate} >= {options.WindStrong}.");

        return failures.ToResult();
    }
}
