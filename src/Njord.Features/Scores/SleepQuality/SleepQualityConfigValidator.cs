using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Scores.SleepQuality;

public sealed class SleepQualityConfigValidator : IValidateOptions<SleepQualityConfig>
{
    public ValidateOptionsResult Validate(string? name, SleepQualityConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<SleepQualityConfig>());

        failures.Require(options.NightStartHour is >= 0 and <= 23, $"NightStartHour must be 0..23, got {options.NightStartHour}.");
        failures.Require(options.NightEndHour is >= 0 and <= 23, $"NightEndHour must be 0..23, got {options.NightEndHour}.");
        failures.Require(options.MinWindowScore >= 1, $"MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        failures.Require(options.MinWindowHours >= 1, $"MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        failures.Require(options.MaxWindows >= 1, $"MaxWindows must be at least 1, got {options.MaxWindows}.");

        return failures.ToResult();
    }
}
