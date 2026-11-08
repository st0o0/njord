using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.OceanCurrents;

public sealed class OceanCurrentsConfigValidator : IValidateOptions<OceanCurrentsConfig>
{
    public ValidateOptionsResult Validate(string? name, OceanCurrentsConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<OceanCurrentsConfig>());

        failures.Require(options.StrongCurrentThresholdKmh > 0, $"StrongCurrentThresholdKmh must be positive, got {options.StrongCurrentThresholdKmh}.");

        return failures.ToResult();
    }
}
