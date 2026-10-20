using Njord.Forecast;

namespace Njord.Core.Domain;

public readonly record struct FeatureContext<TInput>(
    Location Location,
    TInput Current,
    IReadOnlyList<TInput> History,
    DateTimeOffset Now) where TInput : struct;
