namespace Njord.Domain.Weather;

public sealed record ModelSnapshot
{
    public static readonly ModelSnapshot Empty = new(
        new Dictionary<(string Location, WeatherModel Model), ModelForecast>(), false);

    public IReadOnlyDictionary<(string Location, WeatherModel Model), ModelForecast> Entries { get; }
    public bool HasChanged { get; }

    private readonly Dictionary<(string Location, WeatherModel Model), ModelForecast> _entries;

    private ModelSnapshot(
        Dictionary<(string Location, WeatherModel Model), ModelForecast> entries,
        bool hasChanged)
    {
        _entries = entries;
        Entries = entries;
        HasChanged = hasChanged;
    }

    public ModelSnapshot Update(ModelForecast forecast)
    {
        var key = (forecast.Location, forecast.Model);

        if (_entries.TryGetValue(key, out var existing) && existing.Cycle == forecast.Cycle)
        {
            return new ModelSnapshot(_entries, false);
        }

        var copy = new Dictionary<(string Location, WeatherModel Model), ModelForecast>(_entries)
        {
            [key] = forecast,
        };
        return new ModelSnapshot(copy, true);
    }

    public IReadOnlyList<WeatherModel> ModelsFor(string location)
    {
        var models = new List<WeatherModel>();
        foreach (var (key, _) in _entries)
        {
            if (key.Location == location)
            {
                models.Add(key.Model);
            }
        }
        return models;
    }
}
