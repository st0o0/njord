using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class SatelliteOptionsValidator : IValidateOptions<SatelliteOptions>
{
    public ValidateOptionsResult Validate(string? name, SatelliteOptions options) =>
        options.PastHours is < 1 or > 48
            ? ValidateOptionsResult.Fail("PastHours must be between 1 and 48.")
            : ValidateOptionsResult.Success;
}
