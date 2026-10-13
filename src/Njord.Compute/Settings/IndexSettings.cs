using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Weather;

namespace Njord.Compute.Settings;

public sealed record IndexSettings(
    IReadOnlyDictionary<(LocationId Location, IndexKind Kind), ResolvedPreferences> Preferences);
