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
        failures.Require(options.PreSleepHours >= 1, $"PreSleepHours must be at least 1, got {options.PreSleepHours}.");
        failures.Require(options.HighPollen > 0, $"HighPollen must be positive, got {options.HighPollen}.");
        failures.Require(options.Pm25Limit > 0, $"Pm25Limit must be positive, got {options.Pm25Limit}.");
        failures.Require(options.GustLimit > 0, $"GustLimit must be positive, got {options.GustLimit}.");
        failures.Require(options.CapeLimit > 0, $"CapeLimit must be positive, got {options.CapeLimit}.");
        failures.Require(options.ColdThreshold < options.WarmThreshold,
            $"ColdThreshold must be less than WarmThreshold, got {options.ColdThreshold} >= {options.WarmThreshold}.");
        failures.Require(options.WarmThreshold < options.HotThreshold,
            $"WarmThreshold must be less than HotThreshold, got {options.WarmThreshold} >= {options.HotThreshold}.");
        failures.Require(options.HotPenalty >= 0, $"HotPenalty must not be negative, got {options.HotPenalty}.");
        failures.Require(options.WarmPenalty >= 0, $"WarmPenalty must not be negative, got {options.WarmPenalty}.");
        failures.Require(options.ColdPenalty >= 0, $"ColdPenalty must not be negative, got {options.ColdPenalty}.");
        failures.Require(options.HumidityPenalty >= 0, $"HumidityPenalty must not be negative, got {options.HumidityPenalty}.");
        failures.Require(options.Pm25Penalty >= 0, $"Pm25Penalty must not be negative, got {options.Pm25Penalty}.");
        failures.Require(options.PollenPenalty >= 0, $"PollenPenalty must not be negative, got {options.PollenPenalty}.");
        failures.Require(options.PollenSensitivePenalty >= 0, $"PollenSensitivePenalty must not be negative, got {options.PollenSensitivePenalty}.");
        failures.Require(options.GustPenalty >= 0, $"GustPenalty must not be negative, got {options.GustPenalty}.");
        failures.Require(options.ThunderstormPenalty >= 0, $"ThunderstormPenalty must not be negative, got {options.ThunderstormPenalty}.");

        return failures.ToResult();
    }
}
