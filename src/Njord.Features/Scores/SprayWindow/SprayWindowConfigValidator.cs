using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Scores.SprayWindow;

public sealed class SprayWindowConfigValidator : IValidateOptions<SprayWindowConfig>
{
    public ValidateOptionsResult Validate(string? name, SprayWindowConfig options)
    {
        var section = IConfigSection.SectionOf<SprayWindowConfig>();
        var failures = new List<string>();

        if (options.MaxWindSpeed <= 0)
            failures.Add($"{section}:MaxWindSpeed must be positive, got {options.MaxWindSpeed}.");
        if (options.RainFreeHours < 1)
            failures.Add($"{section}:RainFreeHours must be at least 1, got {options.RainFreeHours}.");
        if (options.MinTemp >= options.MaxTemp)
            failures.Add($"{section}:MinTemp must be less than MaxTemp, got {options.MinTemp} >= {options.MaxTemp}.");
        if (options.MinWindowScore < 1)
            failures.Add($"{section}:MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        if (options.MinWindowHours < 1)
            failures.Add($"{section}:MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        if (options.MaxWindows < 1)
            failures.Add($"{section}:MaxWindows must be at least 1, got {options.MaxWindows}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
