using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Conditions.FrostDepth;

public sealed class FrostDepthConfigValidator : IValidateOptions<FrostDepthConfig>
{
    public ValidateOptionsResult Validate(string? name, FrostDepthConfig options)
    {
        var section = IConfigSection.SectionOf<FrostDepthConfig>();
        var failures = new List<string>();

        if (options.PipeDepthCm <= 0)
        {
            failures.Add($"{section}:PipeDepthCm must be positive, got {options.PipeDepthCm}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
