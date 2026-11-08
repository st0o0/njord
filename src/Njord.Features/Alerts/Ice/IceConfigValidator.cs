using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.Ice;

public sealed class IceConfigValidator : IValidateOptions<IceConfig>
{
    public ValidateOptionsResult Validate(string? name, IceConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<IceConfig>());

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");

        return failures.ToResult();
    }
}
