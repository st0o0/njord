using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Conditions.OceanCurrents;

public sealed class OceanCurrentsConfigValidator : IValidateOptions<OceanCurrentsConfig>
{
    public ValidateOptionsResult Validate(string? name, OceanCurrentsConfig options)
    {
        var section = IConfigSection.SectionOf<OceanCurrentsConfig>();
        var failures = new List<string>();

        if (options.StrongCurrentThresholdKmh <= 0)
        {
            failures.Add($"{section}:StrongCurrentThresholdKmh must be positive, got {options.StrongCurrentThresholdKmh}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
