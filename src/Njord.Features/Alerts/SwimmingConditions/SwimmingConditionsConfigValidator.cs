using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.SwimmingConditions;

public sealed class SwimmingConditionsConfigValidator : IValidateOptions<SwimmingConditionsConfig>
{
    public ValidateOptionsResult Validate(string? name, SwimmingConditionsConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<SwimmingConditionsConfig>());

        failures.Require(options.WindowHours >= 1, $"WindowHours must be at least 1, got {options.WindowHours}.");

        if (options.DangerousWaveHeightM <= options.ModerateWaveHeightM)
        {
            failures.Add($"DangerousWaveHeightM ({options.DangerousWaveHeightM}) must exceed ModerateWaveHeightM ({options.ModerateWaveHeightM}).");
        }

        if (options.DangerousWaterTempCelsius >= options.ModerateWaterTempCelsius)
        {
            failures.Add($"DangerousWaterTempCelsius ({options.DangerousWaterTempCelsius}) must be below ModerateWaterTempCelsius ({options.ModerateWaterTempCelsius}).");
        }

        return failures.ToResult();
    }
}
