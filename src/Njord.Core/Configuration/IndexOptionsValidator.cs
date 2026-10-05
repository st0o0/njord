using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Analysis;

namespace Njord.Configuration;

public sealed class IndexOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    private static readonly HashSet<string> ValidScoreNames =
        new(PreferenceResolver.ScoreNames, StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<IndexOptionsValidator> _logger;

    public IndexOptionsValidator(ILogger<IndexOptionsValidator> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var idx = options.Indices;
        var errors = new List<string>();

        ValidatePreferences(idx.Preferences, "Indices.Preferences", errors);

        foreach (var (scoreName, prefs) in idx.ScoreOverrides)
        {
            if (!ValidScoreNames.Contains(scoreName))
            {
                _logger.LogWarning("Indices.ScoreOverrides contains unknown score name '{ScoreName}', ignoring", scoreName);
            }

            ValidatePreferences(prefs, $"Indices.ScoreOverrides[{scoreName}]", errors);
        }

        foreach (var loc in idx.LocationOverrides)
        {
            ValidatePreferences(loc.Preferences, $"Indices.LocationOverrides[{loc.Location}]", errors);
            foreach (var (scoreName, prefs) in loc.ScoreOverrides)
            {
                if (!ValidScoreNames.Contains(scoreName))
                {
                    _logger.LogWarning(
                        "Indices.LocationOverrides[{Location}].ScoreOverrides contains unknown score name '{ScoreName}', ignoring",
                        loc.Location, scoreName);
                }

                ValidatePreferences(prefs, $"Indices.LocationOverrides[{loc.Location}].ScoreOverrides[{scoreName}]", errors);
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidatePreferences(IndexPreferences prefs, string path, List<string> errors)
    {
        if (prefs.HeatSensitivity is { } hs && hs is < 0.0 or > 5.0)
        {
            errors.Add($"{path}.HeatSensitivity must be between 0.0 and 5.0, got {hs}");
        }

        if (prefs.HumiditySensitivity is { } hms && hms is < 0.0 or > 5.0)
        {
            errors.Add($"{path}.HumiditySensitivity must be between 0.0 and 5.0, got {hms}");
        }

        if (prefs.WindSensitivity is { } ws && ws is < 0.0 or > 5.0)
        {
            errors.Add($"{path}.WindSensitivity must be between 0.0 and 5.0, got {ws}");
        }

        if (prefs.RainSensitivity is { } rs && rs is < 0.0 or > 5.0)
        {
            errors.Add($"{path}.RainSensitivity must be between 0.0 and 5.0, got {rs}");
        }
    }
}
