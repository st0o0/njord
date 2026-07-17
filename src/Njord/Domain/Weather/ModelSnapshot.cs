using System.Collections.Immutable;

namespace Njord.Domain.Weather;

public sealed record ModelSnapshot
{
    public static readonly ModelSnapshot Empty = new(
        ImmutableDictionary<(string Location, WeatherModel Model), ModelForecast>.Empty, false);

    public IReadOnlyDictionary<(string Location, WeatherModel Model), ModelForecast> Entries { get; }
    public bool HasChanged { get; }

    private readonly ImmutableDictionary<(string Location, WeatherModel Model), ModelForecast> _entries;

    private ModelSnapshot(
        ImmutableDictionary<(string Location, WeatherModel Model), ModelForecast> entries,
        bool hasChanged)
    {
        _entries = entries;
        Entries = entries;
        HasChanged = hasChanged;
    }

    public ModelSnapshot Update(ModelForecast forecast)
    {
        var key = (forecast.Location, forecast.Model);

        if (Entries.TryGetValue(key, out var existing) && existing.Cycle == forecast.Cycle)
        {
            return new ModelSnapshot(_entries, false);
        }

        return new ModelSnapshot(_entries.SetItem(key, forecast), true);
    }

    public IReadOnlyList<WeatherModel> ModelsFor(string location)
    {
        var models = new List<WeatherModel>();
        foreach (var (key, _) in Entries)
        {
            if (key.Location == location)
            {
                models.Add(key.Model);
            }
        }
        return models;
    }
}
