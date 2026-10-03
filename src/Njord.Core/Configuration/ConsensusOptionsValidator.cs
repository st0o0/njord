using Microsoft.Extensions.Options;

namespace Njord.Configuration;

public sealed class ConsensusOptionsValidator : IValidateOptions<NjordOptions>
{
    private static readonly HashSet<string> ValidMethods = new(StringComparer.OrdinalIgnoreCase)
        { "Mean", "Median", "TrimmedMean" };

    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        var c = options.Enrichment.Consensus;

        if (!ValidMethods.Contains(c.Method))
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.Method '{c.Method}' is invalid. Valid values: {string.Join(", ", ValidMethods)}");
        }

        if (string.Equals(c.Method, "TrimmedMean", StringComparison.OrdinalIgnoreCase)
            && c.TrimPercent is <= 0 or >= 0.5)
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.TrimPercent must be between 0 and 0.5 (exclusive) when Method is TrimmedMean, got {c.TrimPercent}");
        }

        return ValidateOptionsResult.Success;
    }
}
