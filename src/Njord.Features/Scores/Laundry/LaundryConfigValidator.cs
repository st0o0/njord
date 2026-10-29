using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Scores.Laundry;

public sealed class LaundryConfigValidator : IValidateOptions<LaundryConfig>
{
    public ValidateOptionsResult Validate(string? name, LaundryConfig options)
    {
        var section = IConfigSection.SectionOf<LaundryConfig>();
        var failures = new List<string>();

        if (options.MinWindowScore < 1)
            failures.Add($"{section}:MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        if (options.MinWindowHours < 1)
            failures.Add($"{section}:MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        if (options.MaxWindows < 1)
            failures.Add($"{section}:MaxWindows must be at least 1, got {options.MaxWindows}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
