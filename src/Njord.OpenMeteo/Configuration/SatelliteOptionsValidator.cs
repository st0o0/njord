using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SatelliteOptionsValidator : IValidateOptions<SatelliteOptions>
{
    public ValidateOptionsResult Validate(string? name, SatelliteOptions options) =>
        options.PastHours is < 1 or > 48
            ? ValidateOptionsResult.Fail($"Satellite.PastHours must be between 1 and 48, got {options.PastHours}")
            : ValidateOptionsResult.Success;
}
