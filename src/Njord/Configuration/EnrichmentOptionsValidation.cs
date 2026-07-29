using Microsoft.Extensions.Options;

namespace Njord.Configuration;

public sealed class ConsensusOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    private static readonly HashSet<string> ValidMethods = new(StringComparer.OrdinalIgnoreCase)
        { "Mean", "Median", "TrimmedMean" };

    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var c = options.Consensus;

        if (!ValidMethods.Contains(c.Method))
            return ValidateOptionsResult.Fail(
                $"Consensus.Method '{c.Method}' is invalid. Valid values: {string.Join(", ", ValidMethods)}");

        if (string.Equals(c.Method, "TrimmedMean", StringComparison.OrdinalIgnoreCase)
            && c.TrimPercent is <= 0 or >= 0.5)
            return ValidateOptionsResult.Fail(
                $"Consensus.TrimPercent must be between 0 and 0.5 (exclusive) when Method is TrimmedMean, got {c.TrimPercent}");

        return ValidateOptionsResult.Success;
    }
}

public sealed class EnergyOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var e = options.Energy;

        if (e.CarnotEfficiency is <= 0 or >= 1)
            return ValidateOptionsResult.Fail(
                $"Energy.CarnotEfficiency must be between 0 and 1 (exclusive), got {e.CarnotEfficiency}");

        if (e.FlowTemp <= 0)
            return ValidateOptionsResult.Fail(
                $"Energy.FlowTemp must be positive, got {e.FlowTemp}");

        return ValidateOptionsResult.Success;
    }
}

public sealed class HistoryOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var h = options.History;
        var errors = new List<string>();

        if (h.SnapshotInterval <= 0)
            errors.Add($"History.SnapshotInterval must be positive, got {h.SnapshotInterval}");
        if (h.RetentionDays <= 0)
            errors.Add($"History.RetentionDays must be positive, got {h.RetentionDays}");
        if (h.MinSampleSize <= 0)
            errors.Add($"History.MinSampleSize must be positive, got {h.MinSampleSize}");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
