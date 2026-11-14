using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.ExerciseAdvisory;

public sealed class ExerciseAdvisoryConfigValidator : IValidateOptions<ExerciseAdvisoryConfig>
{
    public ValidateOptionsResult Validate(string? name, ExerciseAdvisoryConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<ExerciseAdvisoryConfig>());

        failures.Require(options.SafeScore > 0, $"SafeScore must be positive, got {options.SafeScore}.");
        failures.Require(options.CautionScore > 0, $"CautionScore must be positive, got {options.CautionScore}.");
        failures.Require(options.SafeScore > options.CautionScore,
            $"SafeScore must be greater than CautionScore, got {options.SafeScore} <= {options.CautionScore}.");

        var sum = options.Pm25Weight + options.OzoneWeight + options.TemperatureWeight
                  + options.HumidityWeight + options.UvWeight;
        failures.Require(Math.Abs(sum - 1.0) < 0.001, $"weights must sum to 1.0, got {sum}.");

        failures.Require(options.TemperatureCold < options.TemperatureCool,
            $"TemperatureCold must be less than TemperatureCool, got {options.TemperatureCold} >= {options.TemperatureCool}.");
        failures.Require(options.TemperatureCool < options.TemperatureWarm,
            $"TemperatureCool must be less than TemperatureWarm, got {options.TemperatureCool} >= {options.TemperatureWarm}.");
        failures.Require(options.TemperatureWarm < options.TemperatureHot,
            $"TemperatureWarm must be less than TemperatureHot, got {options.TemperatureWarm} >= {options.TemperatureHot}.");

        return failures.ToResult();
    }
}
