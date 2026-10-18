using Njord.Consensus;
using Njord.Weather;

namespace Njord.Messages.Indices;

public sealed record IndexSettings(
    IReadOnlyDictionary<(LocationId Location, IndexKind Kind), ResolvedPreferences> Preferences);
