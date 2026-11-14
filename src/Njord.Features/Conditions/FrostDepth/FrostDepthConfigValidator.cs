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
        failures.Require(options.StefanAlpha > 0, $"StefanAlpha must be positive, got {options.StefanAlpha}.");
        failures.Require(options.PipeApproachFraction is > 0 and <= 1.0,
            $"PipeApproachFraction must be in (0, 1], got {options.PipeApproachFraction}.");
        failures.Require(options.SurfaceThresholdCm < options.ShallowThresholdCm
            && options.ShallowThresholdCm < options.ModerateThresholdCm,
            $"SurfaceThresholdCm < ShallowThresholdCm < ModerateThresholdCm required, got {options.SurfaceThresholdCm}, {options.ShallowThresholdCm}, {options.ModerateThresholdCm}.");

        return failures.ToResult();
    }
}
