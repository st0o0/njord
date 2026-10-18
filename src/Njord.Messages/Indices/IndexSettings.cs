using Njord.Model;

namespace Njord.Messages.Indices;

public sealed record IndexSettings(
    IReadOnlyDictionary<(LocationId Location, IndexKind Kind), ResolvedPreferences> Preferences);
