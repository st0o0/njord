using Njord.Forecast;

namespace Njord.Messages.Pipeline;

public sealed record LifestyleUpdate(
    Location Location,
    LifestyleKind Kind,
    IReadOnlyDictionary<string, string> Data);
