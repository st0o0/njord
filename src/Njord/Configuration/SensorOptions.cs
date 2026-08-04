using Microsoft.Extensions.Options;

namespace Njord.Configuration;

public sealed class SensorOptions
{
    public bool Enabled { get; set; } = true;
    public int StalenessSeconds { get; set; } = 7200;
}

public sealed class SensorOptionsValidator : IValidateOptions<SensorOptions>
{
    public ValidateOptionsResult Validate(string? name, SensorOptions options)
    {
        if (options.StalenessSeconds <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"Sensors.StalenessSeconds must be positive, got {options.StalenessSeconds}");
        }

        return ValidateOptionsResult.Success;
    }
}
