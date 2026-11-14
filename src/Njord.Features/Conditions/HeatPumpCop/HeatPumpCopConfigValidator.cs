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
        failures.Require(options.CopSlope > 0, $"CopSlope must be positive, got {options.CopSlope}.");
        failures.Require(options.IdealThreshold > options.NormalThreshold
            && options.NormalThreshold > options.InefficientThreshold
            && options.InefficientThreshold > 0,
            $"IdealThreshold > NormalThreshold > InefficientThreshold > 0 required, got {options.IdealThreshold}, {options.NormalThreshold}, {options.InefficientThreshold}.");

        return failures.ToResult();
    }
}
