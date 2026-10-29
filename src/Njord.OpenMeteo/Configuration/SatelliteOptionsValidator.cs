using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SatelliteOptionsValidator : IValidateOptions<SatelliteOptions>
{
    public ValidateOptionsResult Validate(string? name, SatelliteOptions options)
    {
        List<string> failures = [];

        if (options.PastHours is < 1 or > 48)
            failures.Add($"Satellite.PastHours must be between 1 and 48, got {options.PastHours}");

        if (options.Tilt is < 0 or > 90)
            failures.Add($"Satellite.Tilt must be between 0 and 90, got {options.Tilt}");

        if (options.Azimuth is < 0 or > 360)
            failures.Add($"Satellite.Azimuth must be between 0 and 360, got {options.Azimuth}");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
