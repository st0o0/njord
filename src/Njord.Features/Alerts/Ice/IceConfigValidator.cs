using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Ice;

public sealed class IceConfigValidator : IValidateOptions<IceConfig>
{
    public ValidateOptionsResult Validate(string? name, IceConfig options)
    {
        var section = IConfigSection.SectionOf<IceConfig>();
        var failures = new List<string>();

        if (options.WindowHours < 1)
        {
            failures.Add($"{section}:WindowHours must be at least 1, got {options.WindowHours}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
