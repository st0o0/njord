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
        failures.Require(options.FoehnDirectionMin >= 0 && options.FoehnDirectionMin <= 360,
            $"FoehnDirectionMin must be in 0..360, got {options.FoehnDirectionMin}.");
        failures.Require(options.FoehnDirectionMax >= 0 && options.FoehnDirectionMax <= 360,
            $"FoehnDirectionMax must be in 0..360, got {options.FoehnDirectionMax}.");
        failures.Require(options.FoehnDirectionMin < options.FoehnDirectionMax,
            $"FoehnDirectionMin must be < FoehnDirectionMax, got {options.FoehnDirectionMin}, {options.FoehnDirectionMax}.");
        failures.Require(options.ShortLookbackHours >= 1, $"ShortLookbackHours must be >= 1, got {options.ShortLookbackHours}.");
        failures.Require(options.LongLookbackHours >= 1, $"LongLookbackHours must be >= 1, got {options.LongLookbackHours}.");
        failures.Require(options.FoehnLookbackHours >= 1, $"FoehnLookbackHours must be >= 1, got {options.FoehnLookbackHours}.");

        return failures.ToResult();
    }
}
