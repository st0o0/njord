using Microsoft.Extensions.Options;
using Njord.Compute.Configuration;

namespace Njord.Core.Configuration;

public sealed class ConsensusOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var c = options.Consensus;

        if (!Enum.IsDefined(c.Method))
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.Method '{c.Method}' is invalid. Valid values: {string.Join(", ", Enum.GetNames<ConsensusMethod>())}");
        }

        if (c.Method is ConsensusMethod.TrimmedMean && c.TrimPercent is <= 0 or >= 0.5)
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.TrimPercent must be between 0 and 0.5 (exclusive) when Method is TrimmedMean, got {c.TrimPercent}");
        }

        return ValidateOptionsResult.Success;
    }
}
