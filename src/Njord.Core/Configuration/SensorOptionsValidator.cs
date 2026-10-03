using Microsoft.Extensions.Options;

namespace Njord.Configuration;

public sealed class SensorOptionsValidator : IValidateOptions<NjordOptions>
{
    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        if (options.Sensors.StalenessSeconds <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"Sensors.StalenessSeconds must be positive, got {options.Sensors.StalenessSeconds}");
        }

        return ValidateOptionsResult.Success;
    }
}
