using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.MigraineRisk;

public sealed class MigraineRiskConfigValidator : IValidateOptions<MigraineRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, MigraineRiskConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<MigraineRiskConfig>());

        failures.Require(options.PressureThreshold6H > 0, $"PressureThreshold6h must be positive, got {options.PressureThreshold6H}.");
        failures.Require(options.PressureThreshold24H > 0, $"PressureThreshold24h must be positive, got {options.PressureThreshold24H}.");
        failures.Require(options.TempSwingThreshold > 0, $"TempSwingThreshold must be positive, got {options.TempSwingThreshold}.");
        failures.Require(options.HumiditySwingThreshold > 0, $"HumiditySwingThreshold must be positive, got {options.HumiditySwingThreshold}.");

        return failures.ToResult();
    }
}
