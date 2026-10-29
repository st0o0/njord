using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Scores.SleepQuality;

public sealed class SleepQualityConfigValidator : IValidateOptions<SleepQualityConfig>
{
    public ValidateOptionsResult Validate(string? name, SleepQualityConfig options)
    {
        var section = IConfigSection.SectionOf<SleepQualityConfig>();
        var failures = new List<string>();

        if (options.NightStartHour is < 0 or > 23)
            failures.Add($"{section}:NightStartHour must be 0..23, got {options.NightStartHour}.");
        if (options.NightEndHour is < 0 or > 23)
            failures.Add($"{section}:NightEndHour must be 0..23, got {options.NightEndHour}.");
        if (options.MinWindowScore < 1)
            failures.Add($"{section}:MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        if (options.MinWindowHours < 1)
            failures.Add($"{section}:MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        if (options.MaxWindows < 1)
            failures.Add($"{section}:MaxWindows must be at least 1, got {options.MaxWindows}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
