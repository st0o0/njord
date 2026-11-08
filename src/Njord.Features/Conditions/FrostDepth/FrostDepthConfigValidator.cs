using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Conditions.FrostDepth;

public sealed class FrostDepthConfigValidator : IValidateOptions<FrostDepthConfig>
{
    public ValidateOptionsResult Validate(string? name, FrostDepthConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<FrostDepthConfig>());

        failures.Require(options.PipeDepthCm > 0, $"PipeDepthCm must be positive, got {options.PipeDepthCm}.");

        return failures.ToResult();
    }
}
