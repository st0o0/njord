using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Conditions.HeatPumpCop;

public sealed class HeatPumpCopConfigValidator : IValidateOptions<HeatPumpCopConfig>
{
    public ValidateOptionsResult Validate(string? name, HeatPumpCopConfig options)
    {
        var section = IConfigSection.SectionOf<HeatPumpCopConfig>();
        var failures = new List<string>();

        if (options.ForecastHours < 1)
        {
            failures.Add($"{section}:ForecastHours must be at least 1, got {options.ForecastHours}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
