using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.PollenBlast;

public sealed class PollenBlastConfigValidator : IValidateOptions<PollenBlastConfig>
{
    public ValidateOptionsResult Validate(string? name, PollenBlastConfig options)
    {
        var section = IConfigSection.SectionOf<PollenBlastConfig>();
        var failures = new List<string>();

        if (options.TemperatureThreshold <= 0)
        {
            failures.Add($"{section}:TemperatureThreshold must be positive, got {options.TemperatureThreshold}.");
        }

        if (options.WindSpeedThreshold <= 0)
        {
            failures.Add($"{section}:WindSpeedThreshold must be positive, got {options.WindSpeedThreshold}.");
        }

        if (options.PollenDelta <= 0)
        {
            failures.Add($"{section}:PollenDelta must be positive, got {options.PollenDelta}.");
        }

        if (options.PrecipitationThreshold <= 0)
        {
            failures.Add($"{section}:PrecipitationThreshold must be positive, got {options.PrecipitationThreshold}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
