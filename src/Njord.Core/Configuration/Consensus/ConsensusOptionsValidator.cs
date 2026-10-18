using Microsoft.Extensions.Options;
using Njord.Consensus.Settings;

namespace Njord.Core.Configuration.Consensus;

public sealed class ConsensusOptionsValidator : IValidateOptions<ConsensusOptions>
{
    public ValidateOptionsResult Validate(string? name, ConsensusOptions options)
    {
        if (!Enum.IsDefined(options.Method))
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.Method '{options.Method}' is invalid. Valid values: {string.Join(", ", Enum.GetNames<ConsensusMethod>())}");
        }

        if (options.Method is ConsensusMethod.TrimmedMean && options.TrimPercent is <= 0 or >= 0.5)
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.TrimPercent must be between 0 and 0.5 (exclusive) when Method is TrimmedMean, got {options.TrimPercent}");
        }

        if (options.MinQuorum < 1)
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.MinQuorum must be at least 1, got {options.MinQuorum}");
        }

        return ValidateOptionsResult.Success;
    }
}
