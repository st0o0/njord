using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class HistoryOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var h = options.History;
        var errors = new List<string>();

        if (h.SnapshotInterval <= 0)
        {
            errors.Add($"History.SnapshotInterval must be positive, got {h.SnapshotInterval}");
        }

        if (h.RetentionDays <= 0)
        {
            errors.Add($"History.RetentionDays must be positive, got {h.RetentionDays}");
        }

        if (h.MinSampleSize <= 0)
        {
            errors.Add($"History.MinSampleSize must be positive, got {h.MinSampleSize}");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
