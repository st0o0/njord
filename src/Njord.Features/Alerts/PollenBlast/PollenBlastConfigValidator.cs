using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.PollenBlast;

public sealed class PollenBlastConfigValidator : IValidateOptions<PollenBlastConfig>
{
    public ValidateOptionsResult Validate(string? name, PollenBlastConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<PollenBlastConfig>());

        failures.Require(options.TemperatureThreshold > 0, $"TemperatureThreshold must be positive, got {options.TemperatureThreshold}.");
        failures.Require(options.WindSpeedThreshold > 0, $"WindSpeedThreshold must be positive, got {options.WindSpeedThreshold}.");
        failures.Require(options.PollenDelta > 0, $"PollenDelta must be positive, got {options.PollenDelta}.");
        failures.Require(options.PrecipitationThreshold > 0, $"PrecipitationThreshold must be positive, got {options.PrecipitationThreshold}.");

        return failures.ToResult();
    }
}
