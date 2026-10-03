using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record ForecastRecord(
    DateTimeOffset Timestamp,
    string Location,
    IReadOnlyDictionary<WeatherModel, IReadOnlyDictionary<string, double?>> ModelValues,
    IReadOnlyDictionary<string, double?> ConsensusValues);
