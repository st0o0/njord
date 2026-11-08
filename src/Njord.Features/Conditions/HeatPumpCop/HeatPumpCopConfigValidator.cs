using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.HeatPumpCop;

public sealed class HeatPumpCopConfigValidator : IValidateOptions<HeatPumpCopConfig>
{
    public ValidateOptionsResult Validate(string? name, HeatPumpCopConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<HeatPumpCopConfig>());

        failures.Require(options.ForecastHours >= 1, $"ForecastHours must be at least 1, got {options.ForecastHours}.");

        return failures.ToResult();
    }
}
