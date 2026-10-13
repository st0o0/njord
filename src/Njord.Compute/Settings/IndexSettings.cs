using Njord.Compute.Analysis;
using Njord.Weather;

namespace Njord.Compute.Settings;

public sealed record IndexSettings(
    IReadOnlyDictionary<(LocationId Location, IndexKind Kind), ResolvedPreferences> Preferences);
